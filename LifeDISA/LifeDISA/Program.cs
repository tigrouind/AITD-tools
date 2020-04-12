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
					namesByIndex.Add(int.Parse(item[0].TrimStart('@')), "\"" + item[1] + "\"");
				}
			}
			else
			{
				Console.WriteLine("A folder named {0} is required", string.Join(" / ", validFolderNames));
				return -1;
			}
			   			
			if(File.Exists(@"GAMEDATA\OBJETS.ITD"))
			{
				allBytes = File.ReadAllBytes(@"GAMEDATA\OBJETS.ITD");
				int count = allBytes.ReadShort(0);
			
				int i = 0;
				for(int s = 0 ; s < count ; s++)
				{
					int n = s * 52 + 2;
					int name = allBytes.ReadShort(n+10);
					if(name != -1 && name != 0)
					{
						objectsByIndex.Add(i, GetObjectName(namesByIndex[name], i));
					}
					
					i++;
				}
			}
			else
			{
				Console.WriteLine("OBJETS.ITD is required");
				return -1;
			}
			
			objectsByIndex[-1] = "-1";			    
		    objectsByIndex[1] = "PLAYER";
		    for(int i = 0 ; i < 1000 ; i++)
		    {
		    	if(!objectsByIndex.ContainsKey(i)) objectsByIndex[i] = "OBJ" + i;
		    }
		    
		    isCDROMVersion = AskForCDROMVersion();
			Console.WriteLine();
		    		   				
			using (TextWriter writer = new StreamWriter("output.txt"))
			{					
				//dump all
				Regex r = new Regex(@"[0-9a-fA-F]{8}\.DAT", RegexOptions.IgnoreCase);				
				foreach(var file in Directory.GetFiles(@"GAMEDATA\LISTLIFE")
			        .Where(x => r.IsMatch(Path.GetFileName(x)))
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
				
				LifeEnum life = (LifeEnum)Enum.ToObject(typeof(LifeEnum), curr);
				
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
				if(actor != -1) lifeString = objectsByIndex[actor] + "." + lifeString;
				
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
						if(IsTargetingObject(paramAShort))
							paramB = objectsByIndex[int.Parse(paramB)];	

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
						curr = allBytes.ReadShort(pos);
						pos +=2;
						
						//detect if else
						int beforeGoto = pos+curr*2 - 4;
						int next = allBytes.ReadShort(beforeGoto);
						int gotoPosition = beforeGoto+4 + allBytes.ReadShort(beforeGoto+2)*2;
						
						//detection might fail if there is 0x10 (eg: constant) right before end of if
						//because of that, we also check if goto position is within bounds
						if (next == (int)LifeEnum.GOTO && gotoPosition >= 0 && gotoPosition <= (allBytes.Length - 2)) 
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
						curr = allBytes.ReadShort(pos);
						pos += 2;
						
						if(curr == (int)LifeEnum.IF_EGAL ||
						   curr == (int)LifeEnum.IF_DIFFERENT ||
						   curr == (int)LifeEnum.IF_INF ||
 						   curr == (int)LifeEnum.IF_INF_EGAL ||
 						   curr == (int)LifeEnum.IF_SUP ||
 						   curr == (int)LifeEnum.IF_SUP_EGAL)
						{							
							//skip if evaluated vars
							int dummyActor;
							
							EvalvarImpl(out dummyActor);
							EvalvarImpl(out dummyActor);
							
							//check if the two if end up at same place
							curr = allBytes.ReadShort(pos);
							pos += 2;
							
							if((beforeGoto+4) == (pos+curr*2))
							{
								consecutiveIfs = true;
								indentation.RemoveAt(indentation.Count - 1);
							}
						}

						pos = previousPos;												
						break;
												
					case LifeEnum.GOTO: //should never be called
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0}", pos+curr*2);				
						break;						
						
					case LifeEnum.SWITCH:	
						string paramS = Evalvar();
						writer.Write("{0}", paramS);	
																			
						//find end of switch
						bool endOfSwitch = false;
						int gotoPos = pos;
						
						//fix for #353 : IF appearing just after switch (while a CASE is expected)
						while(allBytes.ReadShort(gotoPos) != (int)LifeEnum.CASE &&
						      allBytes.ReadShort(gotoPos) != (int)LifeEnum.MULTI_CASE)
						{
							gotoPos += 2;
						}
						
						int switchEndGoto = -1;						
						do
						{
							int casePos = allBytes.ReadShort(gotoPos);
							if(casePos == (int)LifeEnum.CASE)
							{
								switchEvalVar.Add(gotoPos, paramS);
								gotoPos += 4; //skip case + value
								
								//goto just after case 
								gotoPos += 2 + allBytes.ReadShort(gotoPos)*2;							
								if(allBytes.ReadShort(gotoPos-4) == (int)LifeEnum.GOTO)
								{
									gotosToIgnore.Add(gotoPos-4); //goto at the end of the case statement (end of switch)
									if(switchEndGoto == -1)
									{										
										switchEndGoto = gotoPos + allBytes.ReadShort(gotoPos-2)*2;
									}
							   	}
							}							
							else if(casePos == (int)LifeEnum.MULTI_CASE)
							{
								switchEvalVar.Add(gotoPos, paramS);
								gotoPos += 2; //skip multi case
								casePos = allBytes.ReadShort(gotoPos);
								gotoPos += 2 + casePos * 2; //skip values
								
								//goto just after case
								gotoPos += 2 + allBytes.ReadShort(gotoPos)*2;								
								if(allBytes.ReadShort(gotoPos-4) == (int)LifeEnum.GOTO)
								{
									gotosToIgnore.Add(gotoPos-4); //goto at the end of the case statement (end of switch)
									if(switchEndGoto == -1)
									{
										//end of switch
										switchEndGoto = gotoPos + allBytes.ReadShort(gotoPos-2)*2;
									}
							   	}
							}
							else
							{
								endOfSwitch = true;
							}
						}
						while(!endOfSwitch);	
						
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
												
					case LifeEnum.SOUND:
						{
							string param = Evalvar();
							writer.Write("{0}", vars.GetText("SOUNDS", param));
							break;
						}
						
					case LifeEnum.BODY:
						{
							string param = Evalvar();						
							writer.Write("{0}", vars.GetText("BODYS", param));
							break;
						}

					case LifeEnum.SAMPLE_THEN:
						string param1 = Evalvar();
						writer.Write("{0} ", param1);
						string param2 = Evalvar();
						writer.Write("{0}", param2);
						break;
						
					case LifeEnum.CAMERA_TARGET:
					case LifeEnum.TAKE:		
					case LifeEnum.IN_HAND:
					case LifeEnum.DELETE:
					case LifeEnum.FOUND:
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0}", objectsByIndex[curr]);
						break;
						
					case LifeEnum.FOUND_NAME:						
					case LifeEnum.MESSAGE:
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0}", namesByIndex[curr]);
						break;	

					case LifeEnum.FOUND_FLAG:
					case LifeEnum.FLAGS:	
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("0x{0:X4}", curr);
						break;						
						
					case LifeEnum.LIFE:
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0}", vars.GetText("LIFES", curr));
						break;
						
					case LifeEnum.FOUND_BODY:	
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0}", vars.GetText("BODYS", curr));
						break;
						
					case LifeEnum.NEXT_MUSIC:
					case LifeEnum.MUSIC:
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0}", vars.GetText("MUSIC", curr));
						break;
						
					case LifeEnum.ANIM_REPEAT:
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0}", vars.GetText("ANIMS", curr));
						break;
						
					case LifeEnum.SPECIAL:						
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0}", vars.GetText("SPECIAL", curr));
						break;
						
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
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0}", curr);
						break;
						
					case LifeEnum.READ:
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0} ", curr);
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0} ", curr);
						if(isCDROMVersion)
						{							
							curr = allBytes.ReadShort(pos);
							pos +=2;
							writer.Write("{0}", curr);
						}											
						break;
						
					case LifeEnum.PUT_AT:
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0} ", objectsByIndex[curr]);
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0}", objectsByIndex[curr]);
						break;
						
					case LifeEnum.TRACKMODE:							
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0} ", trackModes[curr]);												
						int trackmode = curr;
						curr = allBytes.ReadShort(pos);
						pos +=2;
						switch(trackmode)
						{
							case 2: //follow
								writer.Write("{0}", objectsByIndex[curr]);
								break;
							case 3: //track
								writer.Write("{0}", vars.GetText("TRACKS", curr));
								break;
						}
						break;
						
					case LifeEnum.ANIM_ONCE:
					case LifeEnum.ANIM_ALL_ONCE:
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0} ", vars.GetText("ANIMS", curr));
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0}", vars.GetText("ANIMS", curr));
						break;
						
					case LifeEnum.SET_BETA:
					case LifeEnum.SET_ALPHA:					
					case LifeEnum.HIT_OBJECT:
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0} ", curr);
						curr = allBytes.ReadShort(pos);
						pos +=2;
						writer.Write("{0}", curr);
						break;
												
					case LifeEnum.CASE:
						curr = allBytes.ReadShort(pos);
						pos +=2;

						string lastSwitchVar = switchEvalVar[pos-4].Split('.').Last();
						if(IsTargetingObject(lastSwitchVar))
							writer.Write("{0}", objectsByIndex[curr]);
						else if (IsTargetingAction(lastSwitchVar))
							writer.Write("{0}", GetActionName(curr));
						else if (lastSwitchVar == "ANIM")
							writer.Write("{0}", vars.GetText("ANIMS", curr));
						else if (lastSwitchVar == "BODY")
							writer.Write("{0}", vars.GetText("BODYS", curr));
						else if (lastSwitchVar == "KEYBOARD_INPUT")
								writer.Write("{0}", vars.GetText("KEYBOARD INPUT", curr));
						else if(lastSwitchVar.StartsWith("POSREL"))
							writer.Write("{0}", vars.GetText("POSREL", curr));
						else					
							writer.Write("{0}", curr);	
						
						curr = allBytes.ReadShort(pos);
						pos +=2;

						indentation.Add(pos+curr*2);									
						break;

					case LifeEnum.MULTI_CASE:
						int numcases =  allBytes.ReadShort(pos);
						pos += 2;
						string lastSwitchVarb = switchEvalVar[pos-4].Split('.').Last();
						
						for(int n = 0; n < numcases; n++) {
							curr = allBytes.ReadShort(pos);							
							pos += 2;
																				
							if(IsTargetingObject(lastSwitchVarb))
								writer.Write("{0}", objectsByIndex[curr]);
							else if (IsTargetingAction(lastSwitchVarb))
								writer.Write("{0}", GetActionName(curr));
							else if (lastSwitchVarb == "ANIM")
								writer.Write("{0}", vars.GetText("ANIMS", curr));
							else if (lastSwitchVarb == "BODY")
								writer.Write("{0}", vars.GetText("BODYS", curr));
							else if (lastSwitchVarb == "KEYBOARD_INPUT")
								writer.Write("{0}", vars.GetText("KEYBOARD INPUT", curr));
							else if(lastSwitchVarb.StartsWith("POSREL"))
								writer.Write("{0}", vars.GetText("POSREL", curr));
							else					
								writer.Write("{0}", curr);														
							
							if(n > 0) writer.Write(" ");
							else writer.Write(", ");
						}
						
						curr = allBytes.ReadShort(pos);
						pos +=2;
						indentation.Add(pos+curr*2);						
						break;
					
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
						curr = allBytes.ReadShort(pos);
						writer.Write("{0} ", vars.GetText("ANIMS", curr));
						pos += 2;
						
						
						for(int n = 0; n < 3 ; n++) {
							curr = allBytes.ReadShort(pos);
							writer.Write("{0} ", curr);
							pos += 2;
						}
						writer.Write("{0} ", Evalvar());
						
						
						curr = allBytes.ReadShort(pos);
						writer.Write("{0} ", vars.GetText("ANIMS", curr));
						pos += 2;
						break;
						
					case LifeEnum.DEF_ZV:	
						for(int n = 0; n < 6; n++) {
							curr = allBytes.ReadShort(pos);
							writer.Write("{0} ", curr);
							pos += 2;
						}
						break;						
						
					case LifeEnum.FIRE:
						int fire_anim = allBytes.ReadShort(pos);
						pos += 2;
						int shoot_frame = allBytes.ReadShort(pos);
						pos += 2;
						int hotpoint = allBytes.ReadShort(pos);
						pos += 2;
						int range = allBytes.ReadShort(pos);
						pos += 2;
						int hitforce = allBytes.ReadShort(pos);
						pos += 2;
						int next_anim = allBytes.ReadShort(pos);
						pos += 2;
						
						writer.Write("{0} {1} {2} {3} {4} {5}", 
				             vars.GetText("ANIMS", fire_anim),
				             shoot_frame, 
				             hotpoint,
				             range, 
				             hitforce, 
				             vars.GetText("ANIMS", next_anim));
						break;						
						
					case LifeEnum.ANIM_MOVE:	
						for(int i = 0 ; i < 7 ; i++)
						{
							curr = allBytes.ReadShort(pos);
							writer.Write("{0} ", vars.GetText("ANIMS", curr));
							pos += 2;
						}
						break;					
						
					case LifeEnum.THROW:
						curr = allBytes.ReadShort(pos);
						writer.Write("{0} ", vars.GetText("ANIMS", curr));
						pos += 2;
						
						for(int i = 0 ; i < 2 ; i++)
						{
							curr = allBytes.ReadShort(pos);
							writer.Write("{0} ", curr);
							pos += 2;
						}						
						
						curr = allBytes.ReadShort(pos);
						writer.Write("{0} ", objectsByIndex[curr]);
						pos += 2;
						
						for(int i = 0 ; i < 2 ; i++)
						{
							curr = allBytes.ReadShort(pos);
							writer.Write("{0} ", curr);
							pos += 2;
						}
						
						curr = allBytes.ReadShort(pos);
						writer.Write("{0} ", vars.GetText("ANIMS", curr));
						pos += 2;						
						break;
											
					case LifeEnum.PICTURE:						
					case LifeEnum.ANGLE:
						int alpha = curr = allBytes.ReadShort(pos);
						pos += 2;
						int beta = curr = allBytes.ReadShort(pos);
						pos += 2;
						int gamma = curr = allBytes.ReadShort(pos);
						pos += 2;
						writer.Write("{0} {1} {2}", alpha, beta, gamma);
						break;
						
					case LifeEnum.CHANGEROOM:
						curr = allBytes.ReadShort(pos);
						writer.Write("E{0}", curr);
						pos += 2;
						curr = allBytes.ReadShort(pos);
						writer.Write("R{0} ", curr);
						pos += 2;
						
						for(int n = 0; n < 3 ; n++) {
							curr = allBytes.ReadShort(pos);
							writer.Write("{0} ", curr);
							pos += 2;
						}
						break;
						
					case LifeEnum.REP_SAMPLE:											
						writer.Write("{0} ", Evalvar());
						curr = allBytes.ReadShort(pos);
						writer.Write("{0} ", curr);
						pos += 2;
						break;
						
					case LifeEnum.DROP:
						string ev = Evalvar();
						int obj;
						if(int.TryParse(ev, out obj))
						{
							ev = objectsByIndex[obj];
						}
						writer.Write("{0} ", ev);
						curr = allBytes.ReadShort(pos);
						writer.Write("{0} ", objectsByIndex[curr]);
						pos += 2;
						break;
						
					case LifeEnum.PUT:
						for(int n = 0; n < 9; n++) {
							curr = allBytes.ReadShort(pos);
							writer.Write("{0} ", curr);
							pos += 2;
						}
						break;
					
					case LifeEnum.ANIM_SAMPLE:
						string eval = Evalvar();
						writer.Write(vars.GetText("SOUNDS", eval) + " ");
						curr = allBytes.ReadShort(pos);
						writer.Write(vars.GetText("ANIMS", curr) + " ");
						pos += 2;
						curr = allBytes.ReadShort(pos);
						writer.Write(curr);
						pos += 2;
						break;
					
					case LifeEnum.SET:
						curr = allBytes.ReadShort(pos);						
						writer.Write(vars.GetText("VARS", curr, "VAR" + curr) + " = ");
						pos +=2;
						writer.Write("{0}", Evalvar());
						break;
						
					case LifeEnum.ADD:	
					case LifeEnum.SUB:	
						curr = allBytes.ReadShort(pos);						
						writer.Write(vars.GetText("VARS", curr, "VAR" + curr) + " ");
						pos +=2;
						writer.Write("{0}", Evalvar());
						break;
						
					case LifeEnum.INC:	
					case LifeEnum.DEC:
						curr = allBytes.ReadShort(pos);
						writer.Write(vars.GetText("VARS", curr, "VAR" + curr) + " ");
						pos += 2;
						break;
												
					case LifeEnum.C_VAR:
						curr = allBytes.ReadShort(pos);
						pos += 2;						
						writer.Write("{0} = {1}", curr, Evalvar());
						break;
						
					default:
						throw new NotImplementedException(life.ToString());						
				}									
				
				if(!consecutiveIfs) writer.WriteLine();
			}
		}
		
		static string GetObjectName(string name, int index)
		{
			string objectName = name.TrimStart('"').TrimEnd('"').ToLowerInvariant();
			objectName = string.Join("_", objectName.Split(' ').Where(x => x != "AN" && x != "A").ToArray());			
			return objectName + "_" + index;
		}
		
		static string GetActionName(int value)
		{
			return vars.GetText("ACTIONS", value);
		}
		
		static void WriteLine(TextWriter writer, int indentation, string text)
		{
			writer.Write("{0}{1}",  new String('\t', indentation), text);
		}
		
		static string Evalvar()
		{			
			int actor;
			string eval = EvalvarImpl(out actor);
			if(actor != -1) eval = objectsByIndex[actor] + "." + eval;
			return eval;
		}
		
		static bool IsTargetingAction(string varName)
		{
			return varName == "ACTION"							
				|| varName == "player_current_action";
		}
		
		static bool IsTargetingObject(string varName)
		{
			return varName == "INHAND" 
				|| varName == "COL_BY" 
				|| varName == "HIT_BY" 
				|| varName == "HIT" 
				|| varName == "ACTOR_COLLIDER";
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
			int curr = allBytes.ReadShort(pos);
			pos +=2;
			
			actor = -1;
			if(curr == -1)
			{
				//CONST
				curr = allBytes.ReadShort(pos);
				pos +=2;
				
				return curr.ToString();
			}
			
			if(curr == 0)
			{
				//CONST
				curr = allBytes.ReadShort(pos);
				pos +=2;
				
				return vars.GetText("VARS", curr, "VAR" + curr);
			}

			if((curr & 0x8000) == 0x8000) {
				//change actor					
				actor = allBytes.ReadShort(pos);
				pos +=2;											
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
					curr = allBytes.ReadShort(pos);
					pos += 2;
					return "DIST("+objectsByIndex[curr]+")";
				case 0xF:
					return "COL_BY";
				case 0x10:
					string eval = Evalvar();
					int objectIndex;
					if(int.TryParse(eval, out objectIndex))
						return "ISFOUND("+objectsByIndex[objectIndex]+")";
					else
						return "ISFOUND("+eval+")";							
				case 0x11:
					return "ACTION";
				case 0x12:
					curr = allBytes.ReadShort(pos);
					pos += 2;
					return "POSREL("+objectsByIndex[curr]+")";
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
					curr = allBytes.ReadShort(pos);
					pos += 2;
					return "RAND("+curr+")";
				case 0x1D:
					return "FALLING";
				case 0x1E:
					return "ROOM";
				case 0x1F:
					return "LIFE";
				case 0x20:
					curr = allBytes.ReadShort(pos);
					pos += 2;
					return "OBJECT("+objectsByIndex[curr]+")";
				case 0x21:
					return "ROOMY";
				case 0x22:
					curr = allBytes.ReadShort(pos);
					pos += 2;
					int curr2 = allBytes.ReadShort(pos);
					pos += 2;
					return "TEST_ZV_END_ANIM (" + curr + " " + curr + ")";
				case 0x23:
					return "MUSIC";
				case 0x24:
					curr = allBytes.ReadShort(pos);
					pos += 2;
					return "C_VAR"+curr;
				case 0x25:
					return "STAGE";
				case 0x26:
					curr = allBytes.ReadShort(pos);
					pos += 2;
					return "THROW("+objectsByIndex[curr]+")";
				default:
					throw new NotImplementedException(curr.ToString());					
			}
		}
	}
}