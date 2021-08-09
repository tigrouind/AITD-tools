using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Shared;

namespace LifeDISA
{
	class Program
	{
		static int pos;
		static byte[] allBytes;

		static readonly Dictionary<int, string> objectsByIndex = new Dictionary<int, string>();
		static readonly Dictionary<int, string> namesByIndex = new Dictionary<int, string>();

		static readonly LinkedList<Instruction> nodes = new LinkedList<Instruction>();
		static readonly Dictionary<int, LinkedListNode<Instruction>> nodesMap = new Dictionary<int, LinkedListNode<Instruction>>();

		static readonly VarParserExt vars = new VarParserExt();

		public static int Main()
		{
			Directory.CreateDirectory("GAMEDATA");

			//parse vars
			if(File.Exists(@"GAMEDATA\vars.txt"))
			{
				vars.Load(@"GAMEDATA\vars.txt", 
				          VarEnum.LIFES, 
				          VarEnum.BODYS, 
				          VarEnum.MUSIC, 
				          VarEnum.ANIMS, 
				          VarEnum.SPECIAL, 
				          VarEnum.TRACKS,
				          VarEnum.POSREL, 
				          VarEnum.VARS, 
				          VarEnum.CVARS, 
				          VarEnum.SOUNDS, 
				          VarEnum.ACTIONS, 
				          VarEnum.KEYBOARD_INPUT,
				          VarEnum.TRACKMODE);
			}

			Regex r = new Regex(@"[0-9a-fA-F]{8}\.DAT", RegexOptions.IgnoreCase);
			int fileCount = 0;
			if (File.Exists(@"GAMEDATA\LISTLIFE.PAK")) 
			{
				using (var pak = new UnPAK(@"GAMEDATA\LISTLIFE.PAK"))
				{
					fileCount = pak.EntryCount;	
				}							
			}

			//dump names
			var languagePakFiles = new [] {	"ENGLISH.PAK", "FRANCAIS.PAK", "DEUTSCH.PAK", "ESPAGNOL.PAK", "ITALIANO.PAK", "USA.PAK" };
			string languageFile = languagePakFiles
				.Select(x => Path.Combine("GAMEDATA", x))
				.FirstOrDefault(File.Exists);

			if (languageFile != null)
			{
				byte[] buffer;
				using (var pak = new UnPAK(languageFile))
				{
					buffer = pak.GetEntry(0);
				}

				foreach(var item in Tools.ReadLines(buffer, Encoding.GetEncoding(850))
					.Where(x => x.Contains(":"))
					.Select(x => x.Split(':'))
					.Where(x => x[1] != string.Empty))
				{
					namesByIndex[int.Parse(item[0].TrimStart('@'))] = item[1];
				}				
			}
			
			if(File.Exists(@"GAMEDATA\OBJETS.ITD"))
			{
				allBytes = File.ReadAllBytes(@"GAMEDATA\OBJETS.ITD");
				int count = allBytes.ReadShort(0);
				int offset;
				
				#if JITD
				offset = 54;
				#else
				offset = 52;
				#endif
				
				for(int i = 0 ; i < count ; i++)
				{
					string name = null;
					int n = i * offset + 2;
					int body = allBytes.ReadShort(n + 2);
					if(body != -1)
					{
						name = vars.GetText(VarEnum.BODYS, body, string.Empty);
					}
					
					if(string.IsNullOrEmpty(name))
					{
						int index = allBytes.ReadShort(n + 10);
						if(index != -1 && index != 0)
						{
							name = namesByIndex[index].ToLowerInvariant();
							name = string.Join("_", name.Split(' ').Where(x => x != "an" && x != "a").ToArray());
						}
					}
					
					if(string.IsNullOrEmpty(name))
					{
						int life = allBytes.ReadShort(n + 34);
						if(life != -1)
						{
							name = vars.GetText(VarEnum.LIFES, life, string.Empty);
						}
					}
															
					if(!string.IsNullOrEmpty(name))
					{
						objectsByIndex.Add(i, name.ToLowerInvariant());
					}					
				}
			}

			using (var writer = new StreamWriter("scripts.life"))
			using (var pak = new UnPAK(@"GAMEDATA\LISTLIFE.PAK"))				
			{
				//dump all
				for(int i = 0 ; i < pak.EntryCount ; i++)
				{
					writer.WriteLine("--------------------------------------------------");
					writer.WriteLine("#{0} {1}", i, vars.GetText(VarEnum.LIFES, i, string.Empty));
					writer.WriteLine("--------------------------------------------------");					
					allBytes = pak.GetEntry(i);
					#if AITD2 && !AITD3
					if(i == 670 && allBytes.Length == 182) Fix670();
					#endif
					
					ParseFile();
					#if !NO_OPTIMIZE
					var optimizer = new Optimizer(nodes, nodesMap);
					optimizer.Run();
					Debug.Assert(nodes.Count(x => x.IndentDec) == nodes.Count(x => x.IndentInc), "Indentation should be equal to zero");
					Debug.Assert(nodes.All(x => x.Type != LifeEnum.GOTO), "Unexpected goto");
					
					ProcessCaseStatements();
					#endif					
					Dump(writer);
				}
			}

			return 0;
		}
		
		static void ProcessCaseStatements()
		{
			foreach(var ins in nodes)
			{
				switch(ins.Type)
				{
					case LifeEnum.CASE:
					case LifeEnum.MULTI_CASE:
					{						
						if (ins.Parent != null)
						{
							for(int i = 0 ; i < ins.Arguments.Count ; i++)
							{
								ins.Arguments[i] = GetConditionName(ins.Parent.EvalEnum, ins.Arguments[i]);
							}
						}
					}
					break;
				}
			}
		}

		static void ParseFile()
		{
			pos = 0;
			nodes.Clear();
			nodesMap.Clear();

			while(pos < allBytes.Length)
			{
				int position = pos;

				int curr = allBytes.ReadShort(pos);
				int actor = -1;
				if((curr & 0x8000) == 0x8000)
				{
					curr = curr & 0x7FFF;
					pos += 2;
					actor = allBytes.ReadShort(pos);
				}

				LifeEnum life = (LifeEnum)curr;
				
				Instruction ins = new Instruction();
				ins.Type = life;
				ins.Line = pos;
				
				if(actor != -1) ins.Actor = GetObjectName(actor);
				pos += 2;

				ParseArguments(life, ins);
				var node = nodes.AddLast(ins);
				nodesMap.Add(position, node);
			}
		}
		
		#if AITD2 && !AITD3
		static void Fix670()
		{
			var list = allBytes.ToList();
			list.RemoveRange(172, 8); //remove GOTO (4) + invalid instruction (4)
			allBytes = list.ToArray();
			foreach(var j in new [] { 28, 108, 132, 146, 160, 166 }) //fix GOTOs
			{
				allBytes[j] -= 4;
			}
		}
		#endif

		static void Dump(TextWriter writer)
		{
			int indent = 0;
			foreach(var ins in nodes)
			{
				if(ins.IndentDec)
				{
					indent--;
				}

				writer.Write(new String('\t', indent));
				
				#if NO_OPTIMIZE
				writer.Write(ins.Line.ToString().PadLeft(4) + " ");
				#endif
				
				writer.Write(ins.Name);	
				if (ins.Arguments.Any())
				{
					writer.Write(" " + string.Join(ins.Separator, ins.Arguments.ToArray()));	
				}

				writer.WriteLine();

				if(ins.IndentInc)
				{
					indent++;
				}
			}
		}

		static void ParseArguments(LifeEnum life, Instruction ins)
		{
			switch(life)
			{
				case LifeEnum.IF_EGAL:
				case LifeEnum.IF_DIFFERENT:
				case LifeEnum.IF_SUP_EGAL:
				case LifeEnum.IF_SUP:
				case LifeEnum.IF_INF_EGAL:
				case LifeEnum.IF_INF:
					EvalEnum evalEnum;
					string paramA = Evalvar(out evalEnum);
					string paramB = Evalvar();

					paramB = GetConditionName(evalEnum, paramB);

					switch(life)
					{
						case LifeEnum.IF_EGAL:
							ins.Add("{0} == {1}", paramA, paramB);
							break;
						case LifeEnum.IF_DIFFERENT:
							ins.Add("{0} <> {1}", paramA, paramB);
							break;
						case LifeEnum.IF_SUP_EGAL:
							ins.Add("{0} >= {1}", paramA, paramB);
							break;
						case LifeEnum.IF_SUP:
							ins.Add("{0} > {1}", paramA, paramB);
							break;
						case LifeEnum.IF_INF_EGAL:
							ins.Add("{0} <= {1}", paramA, paramB);
							break;
						case LifeEnum.IF_INF:
							ins.Add("{0} < {1}", paramA, paramB);
							break;
					}

					ins.Goto = GetParam() * 2 + pos;
					#if NO_OPTIMIZE
					ins.Add("goto {0}", ins.Goto);
					#endif
					break;

				case LifeEnum.GOTO: //should never be called
					ins.Goto = GetParam() * 2 + pos;
					#if NO_OPTIMIZE
					ins.Add("{0}", ins.Goto);
					#endif
					break;

				case LifeEnum.SWITCH:		
					ins.Add(Evalvar(out ins.EvalEnum));
					break;

				case LifeEnum.CASE:
					ins.Add(GetParam());
					ins.Goto = GetParam() * 2 + pos;
					#if NO_OPTIMIZE
					ins.Add("goto {0}", ins.Goto);
					#endif
					break;

				case LifeEnum.MULTI_CASE:
					int numcases = GetParam();
					for(int n = 0; n < numcases; n++)
					{
						ins.Add(GetParam());
					}

					ins.Goto = GetParam() * 2 + pos;
					#if NO_OPTIMIZE
					ins.Add("goto {0}", ins.Goto);
					#endif
					break;

				case LifeEnum.SOUND:
					#if AITD2
						ins.Add(vars.GetText(VarEnum.SOUNDS, GetParam()));
					#else
						ins.Add(vars.GetText(VarEnum.SOUNDS, Evalvar()));
					#endif
					break;

				case LifeEnum.BODY:
					ins.Add(vars.GetText(VarEnum.BODYS, Evalvar()));
					break;

				case LifeEnum.SAMPLE_THEN:
					#if AITD2
						ins.Add(vars.GetText(VarEnum.SOUNDS, GetParam()));
			        	ins.Add(vars.GetText(VarEnum.SOUNDS, GetParam()));
					#else
						ins.Add(vars.GetText(VarEnum.SOUNDS, Evalvar()));
			        	ins.Add(vars.GetText(VarEnum.SOUNDS, Evalvar()));
					#endif										
					break;
					
				case LifeEnum.DELETE:
				case LifeEnum.IN_HAND:
					#if AITD2 
						ins.Add(GetObjectName(Evalvar()));
					#else
						ins.Add(GetObjectName(GetParam()));
					#endif
					break;					

				case LifeEnum.CAMERA_TARGET:
				case LifeEnum.TAKE:
				case LifeEnum.FOUND:
					ins.Add(GetObjectName(GetParam()));
					break;

				case LifeEnum.FOUND_NAME:
				case LifeEnum.MESSAGE:
					ins.Add(GetMessage(GetParam()));
					break;

				case LifeEnum.FOUND_FLAG:
				case LifeEnum.FLAGS:
					ins.Add("0x{0:X4}", GetParam());
					break;

				case LifeEnum.LIFE:
					ins.Add(vars.GetText(VarEnum.LIFES, GetParam()));
					break;

				case LifeEnum.FOUND_BODY:
					ins.Add(vars.GetText(VarEnum.BODYS, GetParam()));
					break;

				case LifeEnum.NEXT_MUSIC:
				case LifeEnum.MUSIC:
					ins.Add(vars.GetText(VarEnum.MUSIC, GetParam()));
					break;

				case LifeEnum.ANIM_REPEAT:
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					break;

				case LifeEnum.SPECIAL:
					ins.Add(vars.GetText(VarEnum.SPECIAL, GetParam()));
					break;
					
				case LifeEnum.STOP_SAMPLE:
					break;
					
				case LifeEnum.READ_ON_PICTURE:
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;
					
				case LifeEnum.ANIM_RESET:
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;
				
				case LifeEnum.UNKNOWN1:
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;	

				case LifeEnum.UNKNOWN2:
					ins.Add(Evalvar());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;								
					
				case LifeEnum.PLAY_SEQUENCE:
					#if AITD2 
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					#else
					ins.Add(GetParam());
					#endif
					break;
					
				case LifeEnum.FIRE_UP_DOWN:
					ins.Add(Evalvar());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(Evalvar());
					break;
					
				case LifeEnum.DEF_ABS_ZV:
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;				
	
				case LifeEnum.STAGE_LIFE:
				case LifeEnum.SET_GROUND:
				case LifeEnum.SET_INVENTORY:
				case LifeEnum.COPY_ANGLE:
				case LifeEnum.TEST_COL:
				case LifeEnum.LIFE_MODE:
				case LifeEnum.FOUND_WEIGHT:
				case LifeEnum.ALLOW_INVENTORY:
				case LifeEnum.WATER:
				case LifeEnum.RND_FREQ:
				case LifeEnum.LIGHT:
				case LifeEnum.SHAKING:
				case LifeEnum.FADE_MUSIC:
					ins.Add(GetParam());
					break;

				case LifeEnum.READ:
					ins.Add(GetParam());
					ins.Add(GetParam());
					#if !JITD && !AITD1_FLOPPY
					ins.Add(GetParam());
					#endif
					break;

				case LifeEnum.PUT_AT:
					ins.Add(GetObjectName(GetParam()));
					ins.Add(GetObjectName(GetParam()));
					break;

				case LifeEnum.TRACKMODE:
				{
					int curr = GetParam();
					ins.Add(vars.GetText(VarEnum.TRACKMODE, curr));

					switch(curr)
					{
						case 0: //none
						case 1: //manual
							GetParam();
							break;

						case 2: //follow
							ins.Add(GetObjectName(GetParam()));
							break;

						case 3: //track
							ins.Add(vars.GetText(VarEnum.TRACKS, GetParam()));
							break;
					}
					break;
				}

				case LifeEnum.ANIM_ONCE:
				case LifeEnum.ANIM_ALL_ONCE:
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					break;

				case LifeEnum.ANIM_HYBRIDE_REPEAT:
				case LifeEnum.ANIM_HYBRIDE_ONCE:
				case LifeEnum.SET_BETA:
				case LifeEnum.SET_ALPHA:
				case LifeEnum.HIT_OBJECT:
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;
					
				case LifeEnum.ENDLIFE:
					ins.ToRemove = true;
					break;
				
				case LifeEnum.PLUIE:
				case LifeEnum.DEL_INVENTORY:	
				case LifeEnum.CONTINUE_TRACK:
				case LifeEnum.RESET_MOVE_MANUAL:				
				#if !JITD
				case LifeEnum.END_SEQUENCE:
				case LifeEnum.UP_COOR_Y:
				case LifeEnum.GET_HARD_CLIP:
				#endif
				case LifeEnum.RETURN:
				case LifeEnum.DO_MAX_ZV:
				case LifeEnum.GAME_OVER:
				case LifeEnum.WAIT_GAME_OVER:
				case LifeEnum.STOP_HIT_OBJECT:
				case LifeEnum.START_CHRONO:
				case LifeEnum.DO_MOVE:
				case LifeEnum.MANUAL_ROT:				
				case LifeEnum.DO_ROT_ZV:
				case LifeEnum.DO_REAL_ZV:
				case LifeEnum.DO_CARRE_ZV:
				case LifeEnum.DO_NORMAL_ZV:
				case LifeEnum.CALL_INVENTORY:
					break;

				case LifeEnum.HIT:
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(Evalvar());
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					break;

				case LifeEnum.DEF_ZV:
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.FIRE:
					#if AITD2
						ins.Add(vars.GetText(VarEnum.ANIMS, Evalvar()));
					#else
						ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					#endif					
					ins.Add(GetParam()); //shoot frame
					ins.Add(GetParam()); //hotpoint
					ins.Add(GetParam()); //range
					ins.Add(GetParam()); //hitforce					
					#if AITD2
						ins.Add(GetParam());
						ins.Add(vars.GetText(VarEnum.ANIMS, Evalvar()));
					#else
						ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					#endif
					break;

				case LifeEnum.ANIM_MOVE:
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					break;

				case LifeEnum.THROW:
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetObjectName(GetParam()));
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					break;

				case LifeEnum.PICTURE:
				case LifeEnum.ANGLE:
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.CHANGEROOM:
					ins.Add("E{0}R{1}", GetParam(), GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.REP_SAMPLE:
					#if AITD2
					ins.Add(vars.GetText(VarEnum.SOUNDS, GetParam()));
					#else
					ins.Add(vars.GetText(VarEnum.SOUNDS, Evalvar()));
					#endif
					ins.Add(GetParam());
					break;
					
				case LifeEnum.DEF_SEQUENCE_SAMPLE:
					pos = GetParam() * 4 + pos;
					break;

				case LifeEnum.DROP:
					ins.Add(GetObjectName(Evalvar()));
					ins.Add(GetObjectName(GetParam()));
					break;

				case LifeEnum.PUT:
					ins.Add(GetObjectName(GetParam()));
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add("E{1}R{0}", GetParam(), GetParam());					
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.ANIM_SAMPLE:
					ins.Add(vars.GetText(VarEnum.SOUNDS, Evalvar()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(GetParam());
					break;

				case LifeEnum.SET:
				{
					int curr = GetParam();
					ins.Add("{0} = {1}", vars.GetText(VarEnum.VARS, curr, "var_" + curr), Evalvar());
					break;
				}

				case LifeEnum.ADD:
				case LifeEnum.SUB:
				{
					int curr = GetParam();
					ins.Add(vars.GetText(VarEnum.VARS, curr, "var_" + curr));
					ins.Add(Evalvar());
					break;
				}

				case LifeEnum.INC:
				case LifeEnum.DEC:
				{
					int curr = GetParam();
					ins.Add(vars.GetText(VarEnum.VARS, curr, "var_" + curr));
					break;
				}

				case LifeEnum.C_VAR:
				{
					int curr = GetParam();
					ins.Add("{0} = {1}", vars.GetText(VarEnum.CVARS, curr, "cvar_" + curr), Evalvar());
					break;
				}

				case LifeEnum.BODY_RESET:
					ins.Add(Evalvar());
					ins.Add(Evalvar());
					break;

				default:
					throw new NotImplementedException(life.ToString());
			}
		}

		#region Get
		
		static string GetConditionName(EvalEnum evalEnum, string value)
		{
			switch (evalEnum)
			{
				case EvalEnum.INHAND:
				case EvalEnum.COL_BY:
				case EvalEnum.CONTACT:
				case EvalEnum.HIT_BY:
				case EvalEnum.HIT:
				case EvalEnum.ACTOR_COLLIDER:
					return GetObjectName(value);
					
				case EvalEnum.POSREL:
					return vars.GetText(VarEnum.POSREL, value);

				case EvalEnum.ACTION:
					return vars.GetText(VarEnum.ACTIONS, value);
					
				case EvalEnum.ANIM:
					return vars.GetText(VarEnum.ANIMS, value);

				case EvalEnum.BODY:
					return vars.GetText(VarEnum.BODYS, value);

				case EvalEnum.KEYBOARD_INPUT:
					return vars.GetText(VarEnum.KEYBOARD_INPUT, value);
					
				case EvalEnum.NUM_TRACK:
					return vars.GetText(VarEnum.TRACKS, value);	

				case EvalEnum.MUSIC:
					return vars.GetText(VarEnum.MUSIC, value);	
					
				case EvalEnum.LIFE:
					return vars.GetText(VarEnum.LIFES, value);						
						
				default:
					return value;
			}	
		}

		static string GetObjectName(string index)
		{
			int value;
			if(int.TryParse(index, out value))
			{
				index = GetObjectName(value);
			}

			return index;
		}

		static string GetObjectName(int index)
		{
			if (index == -1)
			{
				return "-1";
			}

			if (index == 1)
			{
				return "player";
			}

			string text;
			if (objectsByIndex.TryGetValue(index, out text))
			{
				return text + "_" + index;
			}
			return "obj_" + index;
		}

		static string GetMessage(int index)
		{
			string text;
			if (namesByIndex.TryGetValue(index, out text))
			{
				return "\"" + text + "\"";
			}
			return "msg_" + index;
		}

		static int GetParam()
		{
			int curr = allBytes.ReadShort(pos);
			pos += 2;
			return curr;
		}
		
		#endregion
		
		#region Eval
		
		static string Evalvar()
		{
			EvalEnum evalEnum;
			return Evalvar(out evalEnum);
		}

		static string Evalvar(out EvalEnum evalEnum)
		{
			evalEnum = EvalEnum.NONE;
			int curr = GetParam();
			if(curr == -1)
			{
				//CONST
				return GetParam().ToString();
			}

			if(curr == 0)
			{
				//CONST
				curr = GetParam();
				string name =  vars.GetText(VarEnum.VARS, curr, "var_" + curr);				
				if(name == "player_current_action")
				{
					evalEnum = EvalEnum.ACTION;
				}
				
				return name;
			}

			string result = string.Empty;
			if((curr & 0x8000) == 0x8000)
			{
				//change actor
				result = GetObjectName(GetParam()) + ".";
			}

			curr &= 0x7FFF;
			curr--;
			evalEnum = (EvalEnum)curr;

			string parameter = evalEnum.ToString().ToLowerInvariant();
			
			switch (evalEnum)
			{
				case EvalEnum.DIST:
				case EvalEnum.POSREL:
				case EvalEnum.OBJECT:
				#if !JITD
				case EvalEnum.THROW:
				#endif
					parameter += string.Format("({0})", GetObjectName(GetParam()));
					break;
				
				case EvalEnum.ISFOUND:
					parameter += string.Format("({0})", GetObjectName(Evalvar()));
					break;

				case EvalEnum.RAND:
					parameter += string.Format("({0})", GetParam());
					break;

				case EvalEnum.C_VAR:
					parameter = vars.GetText(VarEnum.CVARS, GetParam(), "cvar_" + curr);
					break;
					
				case EvalEnum.TEST_ZV_END_ANIM:
				#if JITD
				case EvalEnum.MATRIX:
				#endif
					parameter += string.Format("({0} {1})", GetParam(), GetParam());
					break;
			}

			result += parameter;
			return result;
		}
		
		#endregion
	}
}