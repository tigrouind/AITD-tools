using System;
using System.Collections.Generic;
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

		static bool isCDROMVersion;
		static bool isAITD2;
		static Func<int, LifeEnum> macroTable;
		static readonly Dictionary<int, string> objectsByIndex = new Dictionary<int, string>();
		static readonly Dictionary<int, string> namesByIndex = new Dictionary<int, string>();

		static string[] trackModes = { "NONE", "MANUAL", "FOLLOW", "TRACK"};

		static readonly VarParserExt vars = new VarParserExt();

		public static int Main()
		{
			Directory.CreateDirectory("GAMEDATA");

			//parse vars
			if(File.Exists(@"GAMEDATA\vars.txt"))
			{
				vars.Parse(@"GAMEDATA\vars.txt");
			}
			
			Regex r = new Regex(@"[0-9a-fA-F]{8}\.DAT", RegexOptions.IgnoreCase);
			var files = Directory.GetFiles(@"GAMEDATA\LISTLIFE")
				.Where(x => r.IsMatch(Path.GetFileName(x)))
				.ToList();
			
			LifeEnum[] table = MacroTable.AITD1;
			if (files.Count < 60) //JITD
			{
				isAITD2 = true; 
				table = MacroTable.AITD2;				
			}
			
			macroTable = index => 
			{
				if(index >= 0 && index < table.Length)
				{
					return table[index];
				}
				
				return (LifeEnum)index;
			};

			//dump names
			var validFolderNames = new [] {	"ENGLISH", "FRANCAIS", "DEUTSCH", "ESPAGNOL", "ITALIANO", "USA" };
			string languageFile = validFolderNames
				.Select(x => Path.Combine("GAMEDATA", x))
				.Where(Directory.Exists)
				.SelectMany(x => Directory.GetFiles(x))
				.FirstOrDefault(x => x.Contains("00000000"));

			if (languageFile != null)
			{
				string[] names = File.ReadAllLines(languageFile, Encoding.GetEncoding(850));
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

			using (TextWriter writer = new StreamWriter("output.txt"))
			{
				//dump all
				foreach(var file in files
					.Select(x => new
					{
						FilePath = x,
						FileNumber = Convert.ToInt32(Path.GetFileNameWithoutExtension(x), 16)
					})
					.OrderBy(x => x.FileNumber))
				{
					writer.WriteLine("--------------------------------------------------");
					writer.WriteLine("#{0} {1}", file.FileNumber, vars.GetText("LIFES", file.FileNumber, string.Empty));
					writer.WriteLine("--------------------------------------------------");
					Dump(file.FilePath, writer);
				}
			}

			return 0;
		}

		static void Dump(string filename, TextWriter writer)
		{
			List<int> indentation = new List<int>();
			List<int> elseIndent = new List<int>();
			HashSet<int> gotosToIgnore = new HashSet<int>();
			Dictionary<int, string> switchEvalVar = new Dictionary<int, string>();
			List<KeyValuePair<int, int>> switchDefault = new List<KeyValuePair<int, int>>();

			bool consecutiveIfs = false;

			allBytes = File.ReadAllBytes(filename);

			pos = 0;

			while(pos < allBytes.Length)
			{
				while (indentation.Contains(pos))
				{
					indentation.RemoveAt(indentation.IndexOf(pos));
					WriteLine(writer, indentation.Count(), "END\r\n");
				}

				if (elseIndent.Contains(pos))
				{
					elseIndent.RemoveAt(elseIndent.IndexOf(pos));
					WriteLine(writer, indentation.Count()-1, "ELSE\r\n");
				}

				int defaultCase = switchDefault.FindIndex(x => x.Key == pos);
				if (defaultCase != -1)
				{
					int indent = switchDefault[defaultCase].Value;
					switchDefault.RemoveAt(defaultCase);
					WriteLine(writer, indentation.Count(), "DEFAULT\r\n");
					indentation.Add(indent);
				}

				int oldPos = pos;
				int actor = -1;
				int curr = allBytes.ReadShort(pos);
				if((curr & 0x8000) == 0x8000)
				{
					curr = curr & 0x7FFF;
					pos += 2;
					actor = allBytes.ReadShort(pos);
				}

				LifeEnum life = macroTable(curr);

				//skip gotos
				if(life == LifeEnum.GOTO && gotosToIgnore.Contains(oldPos))
				{
					pos += 4;
					continue;
				}

				if((life == LifeEnum.ENDLIFE && pos == allBytes.Length - 2))
				{
					pos += 2;
					continue;
				}

				string lifeString = life.ToString();
				if(lifeString.StartsWith("IF")) lifeString = "IF";
				else if(lifeString == "MULTI_CASE") lifeString = "CASE";
				if(actor != -1) lifeString = GetObject(actor) + "." + lifeString;

				if(consecutiveIfs)
				{
					writer.Write(" AND ");
					consecutiveIfs = false;
				}
				else
				{
					if(life != LifeEnum.C_VAR) lifeString += " ";
					WriteLine(writer, indentation.Count(x => x > pos), lifeString);
				}

				pos +=2;

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

						if(paramAShort == "INHAND" || paramAShort == "COL_BY" || paramAShort == "HIT_BY" || paramAShort == "HIT" || paramAShort == "ACTOR_COLLIDER")
							paramB = GetObject(int.Parse(paramB));

						if(paramAShort == "ANIM")
							paramB = vars.GetText("ANIMS", paramB);

						if(paramAShort == "BODY")
							paramB = vars.GetText("BODYS", paramB);

						if(paramAShort == "KEYBOARD_INPUT")
							paramB = vars.GetText("KEYBOARD INPUT", paramB);

						if(paramAShort.StartsWith("POSREL"))
							paramB = vars.GetText("POSREL", paramB);

						switch(life)
						{
							case LifeEnum.IF_EGAL:
								writer.Write("{0} == {1}", paramA, paramB);
								break;
							case LifeEnum.IF_DIFFERENT:
								writer.Write("{0} <> {1}", paramA, paramB);
								break;
							case LifeEnum.IF_SUP_EGAL:
								writer.Write("{0} >= {1}", paramA, paramB);
								break;
							case LifeEnum.IF_SUP:
								writer.Write("{0} > {1}", paramA, paramB);
								break;
							case LifeEnum.IF_INF_EGAL:
								writer.Write("{0} <= {1}", paramA, paramB);
								break;
							case LifeEnum.IF_INF:
								writer.Write("{0} < {1}", paramA, paramB);
								break;
						}

						//read goto
						curr = GetParam();

						//detect if else
						int beforeGoto = pos+curr*2 - 4;
						LifeEnum next = macroTable(allBytes.ReadShort(beforeGoto));
						int gotoPosition = beforeGoto+4 + allBytes.ReadShort(beforeGoto+2)*2;

						//detection might fail if there is 0x10 (eg: constant) right before end of if
						//because of that, we also check if goto position is within bounds
						if (next == LifeEnum.GOTO && gotoPosition >= 0 && gotoPosition <= (allBytes.Length - 2))
						{
							gotosToIgnore.Add(beforeGoto);
							elseIndent.Add(pos+curr*2);
							indentation.Add(gotoPosition);
						}
						else
						{
							indentation.Add(pos+curr*2);
						}

						//check if next instruction is also an if
						int previousPos = pos;
						next = macroTable(GetParam());

						if(next == LifeEnum.IF_EGAL ||
						   next == LifeEnum.IF_DIFFERENT ||
						   next == LifeEnum.IF_INF ||
						   next == LifeEnum.IF_INF_EGAL ||
						   next == LifeEnum.IF_SUP ||
						   next == LifeEnum.IF_SUP_EGAL)
						{
							//skip if evaluated vars
							int dummyActor;

							EvalvarImpl(out dummyActor);
							EvalvarImpl(out dummyActor);

							//check if the two if end up at same place
							curr = GetParam();

							if((beforeGoto+4) == (pos+curr*2))
							{
								consecutiveIfs = true;
								indentation.RemoveAt(indentation.Count - 1);
							}
						}

						pos = previousPos;
						break;

					case LifeEnum.GOTO: //should never be called
						curr = GetParam();
						writer.Write("{0}", pos+curr*2);
						break;

					case LifeEnum.SWITCH:
						string paramS = Evalvar();
						writer.Write("{0}", paramS);

						//find end of switch
						bool endOfSwitch = false;
						int gotoPos = pos;

						//fix for #353 : IF appearing just after switch (while a CASE is expected)
						while(macroTable(allBytes.ReadShort(gotoPos)) != LifeEnum.CASE &&
						      macroTable(allBytes.ReadShort(gotoPos)) != LifeEnum.MULTI_CASE)
						{
							gotoPos += 2;
						}

						int switchEndGoto = -1;
						do
						{
							LifeEnum casePos = macroTable(allBytes.ReadShort(gotoPos));
							switch(casePos)
							{
								case LifeEnum.CASE:
								{
									switchEvalVar.Add(gotoPos, paramS);
									gotoPos += 4; //skip case + value
	
									//goto just after case
									gotoPos += 2 + allBytes.ReadShort(gotoPos)*2;
									if (macroTable(allBytes.ReadShort(gotoPos-4)) == LifeEnum.GOTO)
									{
										gotosToIgnore.Add(gotoPos-4); //goto at the end of the case statement (end of switch)
										if (switchEndGoto == -1)
										{
											switchEndGoto = gotoPos + allBytes.ReadShort(gotoPos-2)*2;
										}
									}
									break;
								}								
									
								case LifeEnum.MULTI_CASE:
								{
									switchEvalVar.Add(gotoPos, paramS);
									gotoPos += 2; //skip multi case
									
									curr = allBytes.ReadShort(gotoPos);
									gotoPos += 2 + curr * 2; //skip values
	
									//goto just after case
									gotoPos += 2 + allBytes.ReadShort(gotoPos)*2;
									if (macroTable(allBytes.ReadShort(gotoPos-4)) == LifeEnum.GOTO)
									{
										gotosToIgnore.Add(gotoPos-4); //goto at the end of the case statement (end of switch)
										if (switchEndGoto == -1)
										{
											//end of switch
											switchEndGoto = gotoPos + allBytes.ReadShort(gotoPos-2)*2;
										}
									}
									break;
								}
								
									
								default:
									endOfSwitch = true;
									break;
							}
						}
						while (!endOfSwitch);

						//should be equal, otherwise there is a default case
						if(switchEndGoto != -1 && switchEndGoto != gotoPos)
						{
							switchDefault.Add(new KeyValuePair<int, int>(gotoPos, switchEndGoto)); //default start + end pos
							indentation.Add(switchEndGoto); //end of switch
						}
						else
						{
							indentation.Add(gotoPos); //end of switch
						}
						break;
						
					case LifeEnum.CASE:
						curr = GetParam();
						string lastSwitchVar = switchEvalVar[pos-4].Split('.').Last();

						writer.Write("{0}", GetSwitchCaseName(curr, lastSwitchVar));

						curr = GetParam();
						indentation.Add(pos+curr*2);
						break;

					case LifeEnum.MULTI_CASE:
						int numcases = GetParam();
						string lastSwitchVarb = switchEvalVar[pos-4].Split('.').Last();

						for(int n = 0; n < numcases; n++) 
						{
							curr = GetParam();

							writer.Write("{0}", GetSwitchCaseName(curr, lastSwitchVarb));

							if(n > 0) writer.Write(" ");
							else writer.Write(", ");
						}

						curr = GetParam();
						indentation.Add(pos+curr*2);
						break;

					case LifeEnum.SOUND:
						writer.Write("{0}", vars.GetText("SOUNDS", Evalvar()));
						break;

					case LifeEnum.BODY:
						writer.Write("{0}", vars.GetText("BODYS", Evalvar()));
						break;

					case LifeEnum.SAMPLE_THEN:
						writer.Write("{0} {1}", Evalvar(), Evalvar());
						break;

					case LifeEnum.CAMERA_TARGET:
					case LifeEnum.TAKE:
					case LifeEnum.IN_HAND:
					case LifeEnum.DELETE:
					case LifeEnum.FOUND:
						writer.Write("{0}", GetObject(GetParam()));
						break;

					case LifeEnum.FOUND_NAME:
					case LifeEnum.MESSAGE:
						writer.Write("{0}", GetName(GetParam()));
						break;

					case LifeEnum.FOUND_FLAG:
					case LifeEnum.FLAGS:
						writer.Write("0x{0:X4}", GetParam());
						break;

					case LifeEnum.LIFE:
						writer.Write("{0}", vars.GetText("LIFES", GetParam()));
						break;

					case LifeEnum.FOUND_BODY:
						writer.Write("{0}", vars.GetText("BODYS", GetParam()));
						break;

					case LifeEnum.NEXT_MUSIC:
					case LifeEnum.MUSIC:
						writer.Write("{0}", vars.GetText("MUSIC", GetParam()));
						break;

					case LifeEnum.ANIM_REPEAT:
						writer.Write("{0}", vars.GetText("ANIMS", GetParam()));
						break;

					case LifeEnum.SPECIAL:
						writer.Write("{0}", vars.GetText("SPECIAL", GetParam()));
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
						writer.Write("{0}", GetParam());
						break;

					case LifeEnum.READ:
						writer.Write("{0} {1}", GetParam(), GetParam());
						if (isCDROMVersion)
						{
							writer.Write(" {0}", GetParam());
						}
						break;

					case LifeEnum.PUT_AT:
						writer.Write("{0} {1}", GetObject(GetParam()), GetObject(GetParam()));
						break;

					case LifeEnum.TRACKMODE:
						curr = GetParam();
						writer.Write("{0}", GetTrackMode(curr));

						switch(curr)
						{
							case 0: //none
							case 1: //manual
								GetParam();
								break;
								
							case 2: //follow
								writer.Write(" {0}", GetObject(GetParam()));
								break;

							case 3: //track
								writer.Write(" {0}", vars.GetText("TRACKS", GetParam()));
								break;


						}
						break;

					case LifeEnum.ANIM_ONCE:
					case LifeEnum.ANIM_ALL_ONCE:
						writer.Write("{0} {1}",
							vars.GetText("ANIMS", GetParam()),
							vars.GetText("ANIMS", GetParam()));
						break;

					case LifeEnum.ANIM_HYBRIDE_ONCE: 	
					case LifeEnum.SET_BETA:
					case LifeEnum.SET_ALPHA:
					case LifeEnum.HIT_OBJECT:
						writer.Write("{0} {1}", GetParam(), GetParam());
						break;

					case LifeEnum.RESET_MOVE_MANUAL:
					case LifeEnum.ENDLIFE:
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
						writer.Write("{0} {1} {2} {3} {4} {5}",
							vars.GetText("ANIMS", GetParam()),
							GetParam(), GetParam(), GetParam(),
							Evalvar(),
							vars.GetText("ANIMS", GetParam()));
						break;

					case LifeEnum.DEF_ZV:
						writer.Write("{0} {1} {2} {3} {4} {5}",
							 GetParam(), GetParam(), GetParam(),
							 GetParam(), GetParam(), GetParam());
						break;

					case LifeEnum.FIRE:
						writer.Write("{0} {1} {2} {3} {4} {5}",
							 vars.GetText("ANIMS", GetParam()),
							 GetParam(),  //shoot frame
							 GetParam(),  //hotpoint
							 GetParam(),  //range
							 GetParam(),  //hitforce
							 vars.GetText("ANIMS", GetParam()));
						break;

					case LifeEnum.ANIM_MOVE:
						writer.Write("{0} {1} {2} {3} {4} {5} {6}",
							vars.GetText("ANIMS", GetParam()),
							vars.GetText("ANIMS", GetParam()),
							vars.GetText("ANIMS", GetParam()),
							vars.GetText("ANIMS", GetParam()),
							vars.GetText("ANIMS", GetParam()),
							vars.GetText("ANIMS", GetParam()),
							vars.GetText("ANIMS", GetParam()));
						break;

					case LifeEnum.THROW:
						writer.Write("{0} {1} {2} {3} {4} {5} {6}",
							vars.GetText("ANIMS", GetParam()),
							GetParam(), GetParam(),
							GetObject(GetParam()),
							GetParam(), GetParam(),
							vars.GetText("ANIMS", GetParam()));
						break;

					case LifeEnum.PICTURE:
					case LifeEnum.ANGLE:
						writer.Write("{0} {1} {2}", GetParam(), GetParam(), GetParam());
						break;

					case LifeEnum.CHANGEROOM:
						writer.Write("E{0}R{1} {2} {3} {4}",
									 GetParam(), GetParam(),
									 GetParam(), GetParam(), GetParam());
						break;

					case LifeEnum.REP_SAMPLE:
						writer.Write("{0} {1}", Evalvar(), GetParam());
						break;

					case LifeEnum.DROP:						
						writer.Write("{0} {1}", GetObject(Evalvar()), GetObject(GetParam()));
						break;

					case LifeEnum.PUT:
						writer.Write("{0} {1} {2} {3} {4} {5} {6} {7} {8}",
									 GetParam(), GetParam(), GetParam(), GetParam(), GetParam(),
									 GetParam(), GetParam(), GetParam(), GetParam());
						break;

					case LifeEnum.ANIM_SAMPLE:
						writer.Write("{0} {1} {2}",
							vars.GetText("SOUNDS", Evalvar()),
							vars.GetText("ANIMS", GetParam()),
							GetParam());
						break;

					case LifeEnum.SET:
						curr = GetParam();
						writer.Write("{0} = {1}", vars.GetText("VARS", curr, "VAR" + curr), Evalvar());
						break;

					case LifeEnum.ADD:
					case LifeEnum.SUB:
						curr = GetParam();
						writer.Write("{0} {1}", vars.GetText("VARS", curr, "VAR" + curr), Evalvar());
						break;

					case LifeEnum.INC:
					case LifeEnum.DEC:
						curr = GetParam();
						writer.Write(vars.GetText("VARS", curr, "VAR" + curr) + " ");
						break;

					case LifeEnum.C_VAR:
						writer.Write("{0} = {1}", GetParam(), Evalvar());
						break;
											
					case LifeEnum.BODY_RESET:
						writer.Write("{0} {1}", Evalvar(), Evalvar());
						break;

					default:
						throw new NotImplementedException(life.ToString());
				}

				if (!consecutiveIfs) writer.WriteLine();
			}
		}

		static string GetSwitchCaseName(int value, string lastSwitchVar)
		{
			if(lastSwitchVar.StartsWith("POSREL"))
			{
				return vars.GetText("POSREL", value);
			}

			switch (lastSwitchVar)
			{
				case "INHAND":
				case "COL_BY":
				case "HIT_BY":
				case "HIT":
				case "ACTOR_COLLIDER":
					return GetObject(value);

				case "ACTION":
				case "player_current_action":
					return vars.GetText("ACTIONS", value);

				case "ANIM":
					return vars.GetText("ANIMS", value);

				case "BODY":
					return vars.GetText("BODYS", value);

				case "KEYBOARD_INPUT":
					return vars.GetText("KEYBOARD INPUT", value);

				default:
					return value.ToString();
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
				return "PLAYER";
			}

			string text;
			if (objectsByIndex.TryGetValue(index, out text))
			{
				return text + "_" + index;
			}
			return "OBJ" + index;
		}

		static string GetName(int index)
		{
			string text;
			if (namesByIndex.TryGetValue(index, out text))
			{
				return "\"" + text + "\"";
			}
			return "MSG" + index;
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

		static void WriteLine(TextWriter writer, int indentation, string text)
		{
			writer.Write("{0}{1}",	new String('\t', indentation), text);
		}

		static string Evalvar()
		{
			int actor;
			string eval = EvalvarImpl(out actor);
			if(actor != -1) eval = GetObject(actor) + "." + eval;
			return eval;
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

		static string EvalvarImpl(out int actor)
		{
			int curr = GetParam();

			actor = -1;
			if(curr == -1)
			{
				//CONST
				return GetParam().ToString();
			}

			if(curr == 0)
			{
				//CONST
				curr = GetParam();
				return vars.GetText("VARS", curr, "VAR" + curr);
			}

			if((curr & 0x8000) == 0x8000) {
				//change actor
				actor = GetParam();
			}

			curr &= 0x7FFF;
			curr--;
			switch(curr)
			{
				case 0x0:
					return "ACTOR_COLLIDER";
				case 0x1:
					return "TRIGGER_COLLIDER";
				case 0x2:
					return "HARD_COLLIDER";
				case 0x3:
					return "HIT";
				case 0x4:
					return "HIT_BY";
				case 0x5:
					return "ANIM";
				case 0x6:
					return "END_ANIM";
				case 0x7:
					return "FRAME";
				case 0x8:
					return "END_FRAME";
				case 0x9:
					return "BODY";
				case 0xA:
					return "MARK";
				case 0xB:
					return "NUM_TRACK";
				case 0xC:
					return "CHRONO";
				case 0xD:
					return "ROOM_CHRONO";
				case 0xE:
					return "DIST("+GetObject(GetParam())+")";
				case 0xF:
					return "COL_BY";
				case 0x10:
					return "ISFOUND("+GetObject(Evalvar())+")";
				case 0x11:
					return "ACTION";
				case 0x12:
					return "POSREL("+GetObject(GetParam())+")";
				case 0x13:
					return "KEYBOARD_INPUT";
				case 0x14:
					return "SPACE";
				case 0x15:
					return "COL_BY";
				case 0x16:
					return "ALPHA";
				case 0x17:
					return "BETA";
				case 0x18:
					return "GAMMA";
				case 0x19:
					return "INHAND";
				case 0x1A:
					return "HITFORCE";
				case 0x1B:
					return "CAMERA";
				case 0x1C:
					return "RAND("+GetParam()+")";
				case 0x1D:
					return "FALLING";
				case 0x1E:
					return "ROOM";
				case 0x1F:
					return "LIFE";
				case 0x20:
					return "OBJECT("+GetObject(GetParam())+")";
				case 0x21:
					return "ROOMY";
				case 0x22:
					return "TEST_ZV_END_ANIM(" + GetParam() + " " + GetParam() + ")";
				case 0x23:
					return "MUSIC";
				case 0x24:
					return "C_VAR"+GetParam();
				case 0x25:
					if (isAITD2)
					{
						return "MATRIX(" + GetParam() + " " + GetParam() + ")";
					}
					return "STAGE";
				case 0x26:
					if (isAITD2)
					{
						return "TRIGGER_COLLIDER";	
					}
					return "THROW("+GetObject(GetParam())+")";
					
				default:
					throw new NotImplementedException(curr.ToString());
			}
		}
	}
}