using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Shared;

namespace LifeDISA
{
	class Program
	{
		static int pos;
		static byte[] allBytes;

		static LifeEnum[] table;
		static EvalEnum[] tableEval;
		static bool isCDROMVersion;
		static bool isAITD2;
		static readonly Dictionary<int, string> objectsByIndex = new Dictionary<int, string>();
		static readonly Dictionary<int, string> namesByIndex = new Dictionary<int, string>();

		static string[] trackModes = { "none", "manual", "follow", "track"};

		static readonly LinkedList<Instruction> nodes = new LinkedList<Instruction>();
		static readonly Dictionary<int, LinkedListNode<Instruction>> nodesMap = new Dictionary<int, LinkedListNode<Instruction>>();

		static readonly VarParserExt vars = new VarParserExt();

		public static int Main()
		{
			Directory.CreateDirectory("GAMEDATA");

			//parse vars
			if(File.Exists(@"GAMEDATA\vars.txt"))
			{
				vars.Load(@"GAMEDATA\vars.txt");
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

			table = MacroTable.AITD1;
			tableEval = MacroTable.AITD1Eval;
			if (fileCount < 60) //JITD
			{
				isAITD2 = true;
				table = MacroTable.AITD2;
				tableEval = MacroTable.AITD2Eval;
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
				
				var names = ReadAllLines(buffer, Encoding.GetEncoding(850));

				foreach(var item in names
					.Where(x => x.Contains(":"))
					.Select(x =>  x.Split(':')))
				{
					namesByIndex.Add(int.Parse(item[0].TrimStart('@')), item[1]);
				}				
			}

			if(File.Exists(@"GAMEDATA\OBJETS.ITD"))
			{
				allBytes = File.ReadAllBytes(@"GAMEDATA\OBJETS.ITD");
				int count = allBytes.ReadShort(0);

				int offset = isAITD2 ? 54 : 52;
				int i = 0;
				for(int s = 0 ; s < count ; s++)
				{
					int n = s * offset + 2;
					int index = allBytes.ReadShort(n+10);
					if(index != -1 && index != 0)
					{
						string name = namesByIndex[index].ToLowerInvariant();
						name = string.Join("_", name.Split(' ').Where(x => x != "an" && x != "a").ToArray());
						objectsByIndex.Add(i, name);
					}

					i++;
				}
			}

			isCDROMVersion = AskForCDROMVersion();

			using (var writer = new StreamWriter("output.txt"))
			using (var pak = new UnPAK(@"GAMEDATA\LISTLIFE.PAK"))				
			{
				//dump all
				for(int i = 0 ; i < pak.EntryCount ; i++)
				{
					writer.WriteLine("--------------------------------------------------");
					writer.WriteLine("#{0} {1}", i, vars.GetText("LIFES", i, string.Empty));
					writer.WriteLine("--------------------------------------------------");					
					allBytes = pak.GetEntry(i);
					ParseFile();
					#if !NO_DECOMPILE
					Optimize();
					Cleanup();
					#endif
					Dump(writer);
				}
			}

			return 0;
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

				LifeEnum life;
				if(curr >= 0 && curr < table.Length)
				{
					life = table[curr];
				}
				else
				{
					life = (LifeEnum)curr;
				}
				
				Instruction ins = new Instruction();
				ins.Type = life;
				ins.Line = pos;
				
				if(actor != -1) ins.Actor = GetObject(actor);
				pos += 2;

				ParseArguments(life, ins);
				var node = nodes.AddLast(ins);
				nodesMap.Add(position, node);
			}
		}

		#if !NO_DECOMPILE
		static void Optimize()
		{
			for(var node = nodes.First; node != null; node = node.Next)
			{
				var ins = node.Value;
				switch(ins.Type)
				{
					case LifeEnum.IF_EGAL:
					case LifeEnum.IF_DIFFERENT:
					case LifeEnum.IF_SUP_EGAL:
					case LifeEnum.IF_SUP:
					case LifeEnum.IF_INF_EGAL:
					case LifeEnum.IF_INF:
						OptimizeIf(node);
						break;

					case LifeEnum.SWITCH:
						OptimizeSwitch(node);
						break;
				}
			}
		}
		
		static void OptimizeIf(LinkedListNode<Instruction> node)
		{
			var ins = node.Value;
			var target = nodesMap[ins.Goto];
			var previous = target.Previous;
			if (previous.Value.Type == LifeEnum.GOTO)
			{							
				previous.Value.ToRemove = true;
				nodes.AddBefore(target, new Instruction { Type = LifeEnum.ELSE });						
				nodes.AddBefore(nodesMap[previous.Value.Goto], new Instruction { Type = LifeEnum.END });
			}
			else
			{
				nodes.AddBefore(target, new Instruction { Type = LifeEnum.END });
			}

			//check for consecutive IFs
			var next = node.Next;
			while(next != null &&
				(
				next.Value.Type == LifeEnum.IF_EGAL ||
				next.Value.Type == LifeEnum.IF_DIFFERENT ||
				next.Value.Type == LifeEnum.IF_INF ||
				next.Value.Type == LifeEnum.IF_INF_EGAL ||
				next.Value.Type == LifeEnum.IF_SUP ||
				next.Value.Type == LifeEnum.IF_SUP_EGAL) &&
				target == nodesMap[next.Value.Goto]) //the IFs ends up at same place
			{
				var after = next.Next;
				ins.Arguments.Add(next.Value.Arguments[0]);
				nodes.Remove(next);

				next = after;
			}
		}
		
		static void OptimizeSwitch(LinkedListNode<Instruction> node)
		{
			var ins = node.Value;			
			//instruction after switch should be CASE or MULTICASE
			//but if could be instructions (eg: DEFAULT after switch)
			var target = node.Next;
			while(target != null &&
				  target.Value.Type != LifeEnum.CASE &&
				  target.Value.Type != LifeEnum.MULTI_CASE)
			{
				target = target.Next;
			}
	
			//detect end of switch
			string switchValue = node.Value.Arguments.First().Split('.').Last();
			LinkedListNode<Instruction> endOfSwitch = null;
			bool lastInstruction = false;
	
			do
			{
				ins = target.Value;
				switch(ins.Type)
				{
					case LifeEnum.CASE:
					case LifeEnum.MULTI_CASE:
					{
						for(int i = 0 ; i < ins.Arguments.Count ; i++)
						{
							ins.Arguments[i] = GetConditionName(switchValue, ins.Arguments[i]);
						}
	
						target = nodesMap[ins.Goto];
						if (target.Previous.Value.Type == LifeEnum.GOTO)
						{
							target.Previous.Value.ToRemove = true;
							if(endOfSwitch == null)
							{
								endOfSwitch = nodesMap[target.Previous.Value.Goto];
							}
						}
	
						nodes.AddBefore(target, new Instruction { Type = LifeEnum.END });
						break;
					}
	
					default:
						lastInstruction = true;
						break;
				}
			}
			while (!lastInstruction);
	
			//should be equal, otherwise there is a default case
			if(endOfSwitch != null && target != endOfSwitch)
			{
				nodes.AddBefore(endOfSwitch, new Instruction { Type = LifeEnum.END });
				nodes.AddBefore(endOfSwitch, new Instruction { Type = LifeEnum.END });
				nodes.AddBefore(target, new Instruction { Type = LifeEnum.DEFAULT });
			}
			else
			{
				nodes.AddBefore(target, new Instruction { Type = LifeEnum.END });
			}
		}
		
		static void Cleanup()
		{
			//remove GOTO and ENDLIFE
			var currentNode = nodes.First;
			while(currentNode != null)
			{
				var nextNode = currentNode.Next;
				if(currentNode.Value.ToRemove)
				{
					nodes.Remove(currentNode);
				}

				currentNode = nextNode;
			}
		}
		#endif

		static void Dump(TextWriter writer)
		{
			int indent = 0;
			for(var node = nodes.First; node != null; node = node.Next)
			{
				var ins = node.Value;

				if(ins.IndentDec)
				{
					indent--;
				}

				writer.Write(new String('\t', indent));
				
				#if NO_DECOMPILE
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
			
			#if !NO_DECOMPILE
			Debug.Assert(indent == 0);
			Debug.Assert(nodes.All(x => x.Type != LifeEnum.GOTO));
			#endif
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
					string paramA = Evalvar();
					string paramB = Evalvar();

					string paramAShort = paramA.Split('.').Last();
					paramB = GetConditionName(paramAShort, paramB);

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
					#if NO_DECOMPILE
					ins.Add("goto {0}", ins.Goto);
					#endif
					break;

				case LifeEnum.GOTO: //should never be called
					ins.Goto = GetParam() * 2 + pos;
					#if NO_DECOMPILE
					ins.Add("{0}", ins.Goto);
					#endif
					break;

				case LifeEnum.SWITCH:
					ins.Add(Evalvar());
					break;

				case LifeEnum.CASE:
					ins.Add(GetParam());
					ins.Goto = GetParam() * 2 + pos;
					#if NO_DECOMPILE
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
					break;

				case LifeEnum.SOUND:
					ins.Add(vars.GetText("SOUNDS", Evalvar()));
					break;

				case LifeEnum.BODY:
					ins.Add(vars.GetText("BODYS", Evalvar()));
					break;

				case LifeEnum.SAMPLE_THEN:
					ins.Add(vars.GetText("SOUNDS", Evalvar()));
			        ins.Add(vars.GetText("SOUNDS", Evalvar()));
					break;

				case LifeEnum.CAMERA_TARGET:
				case LifeEnum.TAKE:
				case LifeEnum.IN_HAND:
				case LifeEnum.DELETE:
				case LifeEnum.FOUND:
					ins.Add(GetObject(GetParam()));
					break;

				case LifeEnum.FOUND_NAME:
				case LifeEnum.MESSAGE:
					ins.Add(GetName(GetParam()));
					break;

				case LifeEnum.FOUND_FLAG:
				case LifeEnum.FLAGS:
					ins.Add("0x{0:X4}", GetParam());
					break;

				case LifeEnum.LIFE:
					ins.Add(vars.GetText("LIFES", GetParam()));
					break;

				case LifeEnum.FOUND_BODY:
					ins.Add(vars.GetText("BODYS", GetParam()));
					break;

				case LifeEnum.NEXT_MUSIC:
				case LifeEnum.MUSIC:
					ins.Add(vars.GetText("MUSIC", GetParam()));
					break;

				case LifeEnum.ANIM_REPEAT:
					ins.Add(vars.GetText("ANIMS", GetParam()));
					break;

				case LifeEnum.SPECIAL:
					ins.Add(vars.GetText("SPECIAL", GetParam()));
					break;

				case LifeEnum.SET_INVENTORY:
				case LifeEnum.PLAY_SEQUENCE:
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
					if (isCDROMVersion)
					{
						ins.Add(GetParam());
					}
					break;

				case LifeEnum.PUT_AT:
					ins.Add(GetObject(GetParam()));
					ins.Add(GetObject(GetParam()));
					break;

				case LifeEnum.TRACKMODE:
				{
					int curr = GetParam();
					ins.Add(GetTrackMode(curr));

					switch(curr)
					{
						case 0: //none
						case 1: //manual
							GetParam();
							break;

						case 2: //follow
							ins.Add(GetObject(GetParam()));
							break;

						case 3: //track
							ins.Add(vars.GetText("TRACKS", GetParam()));
							break;
					}
					break;
				}

				case LifeEnum.ANIM_ONCE:
				case LifeEnum.ANIM_ALL_ONCE:
					ins.Add(vars.GetText("ANIMS", GetParam()));
					ins.Add(vars.GetText("ANIMS", GetParam()));
					break;

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

				case LifeEnum.RESET_MOVE_MANUAL:				
				case LifeEnum.RETURN:
				case LifeEnum.END_SEQUENCE:
				case LifeEnum.DO_MAX_ZV:
				case LifeEnum.GAME_OVER:
				case LifeEnum.WAIT_GAME_OVER:
				case LifeEnum.STOP_HIT_OBJECT:
				case LifeEnum.START_CHRONO:
				case LifeEnum.UP_COOR_Y:
				case LifeEnum.DO_MOVE:
				case LifeEnum.MANUAL_ROT:
				case LifeEnum.GET_HARD_CLIP:
				case LifeEnum.DO_ROT_ZV:
				case LifeEnum.DO_REAL_ZV:
				case LifeEnum.DO_CARRE_ZV:
					break;

				case LifeEnum.HIT:
					ins.Add(vars.GetText("ANIMS", GetParam()));
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(Evalvar());
					ins.Add(vars.GetText("ANIMS", GetParam()));
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
					ins.Add(vars.GetText("ANIMS", GetParam()));
					ins.Add(GetParam());//shoot frame
					ins.Add(GetParam()); //hotpoint
					ins.Add(GetParam()); //range
					ins.Add(GetParam());//hitforce
					ins.Add(vars.GetText("ANIMS", GetParam()));
					break;

				case LifeEnum.ANIM_MOVE:
					ins.Add(vars.GetText("ANIMS", GetParam()));
					ins.Add(vars.GetText("ANIMS", GetParam()));
					ins.Add(vars.GetText("ANIMS", GetParam()));
					ins.Add(vars.GetText("ANIMS", GetParam()));
					ins.Add(vars.GetText("ANIMS", GetParam()));
					ins.Add(vars.GetText("ANIMS", GetParam()));
					ins.Add(vars.GetText("ANIMS", GetParam()));
					break;

				case LifeEnum.THROW:
					ins.Add(vars.GetText("ANIMS", GetParam()));
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetObject(GetParam()));
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(vars.GetText("ANIMS", GetParam()));
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
					ins.Add(vars.GetText("SOUNDS", Evalvar()));
					ins.Add(GetParam());
					break;

				case LifeEnum.DROP:
					ins.Add(GetObject(Evalvar()));
					ins.Add(GetObject(GetParam()));
					break;

				case LifeEnum.PUT:
					ins.Add(GetObject(GetParam()));
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add("E{0}R{1}", GetParam(), GetParam());					
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.ANIM_SAMPLE:
					ins.Add(vars.GetText("SOUNDS", Evalvar()));
					ins.Add(vars.GetText("ANIMS", GetParam()));
					ins.Add(GetParam());
					break;

				case LifeEnum.SET:
				{
					int curr = GetParam();
					ins.Add("{0} = {1}", vars.GetText("VARS", curr, "VAR" + curr), Evalvar());
					break;
				}

				case LifeEnum.ADD:
				case LifeEnum.SUB:
				{
					int curr = GetParam();
					ins.Add(vars.GetText("VARS", curr, "VAR" + curr));
					ins.Add(Evalvar());
					break;
				}

				case LifeEnum.INC:
				case LifeEnum.DEC:
				{
					int curr = GetParam();
					ins.Add(vars.GetText("VARS", curr, "VAR" + curr));
					break;
				}

				case LifeEnum.C_VAR:
					ins.Add("c_var{0} = {1}", GetParam(), Evalvar());
					break;

				case LifeEnum.BODY_RESET:
					ins.Add(Evalvar());
					ins.Add(Evalvar());
					break;

				default:
					throw new NotImplementedException(life.ToString());
			}
		}

		static string GetConditionName(string valueA, string valueB)
		{
			if(valueA.StartsWith("posrel"))
			{
				return vars.GetText("POSREL", valueB);
			}

			switch (valueA)
			{
				case "inhand":
				case "col_by":
				case "hit_by":
				case "hit":
				case "actor_collider":
					return GetObject(valueB);

				case "action":
				case "player_current_action":
					return vars.GetText("ACTIONS", valueB);

				case "anim":
					return vars.GetText("ANIMS", valueB);

				case "body":
					return vars.GetText("BODYS", valueB);

				case "keyboard_input":
					return vars.GetText("KEYBOARD INPUT", valueB);
					
				case "num_track":
					return vars.GetText("TRACKS", valueB);

				default:
					return valueB;
			}
		}

		static string GetObject(string index)
		{
			int value;
			if(int.TryParse(index, out value))
			{
				index = GetObject(value);
			}

			return index;
		}

		static string GetObject(int index)
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
			return "obj" + index;
		}

		static string GetName(int index)
		{
			string text;
			if (namesByIndex.TryGetValue(index, out text))
			{
				return "\"" + text + "\"";
			}
			return "msg" + index;
		}

		static int GetParam()
		{
			int curr = allBytes.ReadShort(pos);
			pos +=2;
			return curr;
		}

		static string GetTrackMode(int index)
		{
			return trackModes[index];
		}

		static bool AskForCDROMVersion()
		{
			string line;
			do
			{
				Console.Write("CD-ROM version [y/n] ? ");
				line = Console.ReadLine().ToLower();
			}
			while (line != "y" && line != "n");
			return line == "y";
		}

		static string Evalvar()
		{
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
				return vars.GetText("VARS", curr, "var" + curr);
			}

			string result = string.Empty;
			if((curr & 0x8000) == 0x8000)
			{
				//change actor
				result = GetObject(GetParam()) + ".";
			}

			curr &= 0x7FFF;
			curr--;
			var evalEnum = tableEval[curr];

			string parameter = string.Empty;
			switch (evalEnum)
			{
				case EvalEnum.DIST:
				case EvalEnum.POSREL:
				case EvalEnum.OBJECT:
				case EvalEnum.THROW:
					parameter = string.Format("({0})", GetObject(GetParam()));
					break;

				case EvalEnum.ISFOUND:
					parameter = string.Format("({0})", GetObject(Evalvar()));
					break;

				case EvalEnum.RAND:
					parameter = string.Format("({0})", GetParam());
					break;

				case EvalEnum.C_VAR:
					parameter = GetParam().ToString();
					break;

				case EvalEnum.TEST_ZV_END_ANIM:
				case EvalEnum.MATRIX:
					parameter = string.Format("({0} {1})", GetParam(), GetParam());
					break;
			}

			result += evalEnum.ToString().ToLower() + parameter;
			return result;
		}
		
		static string[] ReadAllLines(byte[] buffer, Encoding encoding)
		{
			List<string> lines = new List<string>();
			using(var stream = new MemoryStream(buffer))
			using(var reader = new StreamReader(stream, encoding))
	      	{
				string line;					
				while((line = reader.ReadLine()) != null)
				{
			      	lines.Add(line);
				}					      		
			}
			
			return lines.ToArray();
		}
	}
}