using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LifeDISA
{		
	class Program
	{
		public static int pos;
		
		public static byte[] allBytes;
				
		public static short ReadShort (byte a, byte b)
		{
			unchecked
			{
				return (short)(a | b << 8);
			}
		}
		
		static bool IsCDROMVersion;
		static Dictionary<int, string> objectsByIndex = new Dictionary<int, string>();
		static Dictionary<int, string> namesByIndex = new Dictionary<int, string>();
		
		static string[] trackModes = { "NONE", "MANUAL", "FOLLOW", "TRACK"};

		public static int Main()
		{	
			string line;
			do
			{
				Console.Write("CD-ROM version [y/n] ? ");
				line = Console.ReadLine().ToLower();
			} 
			while (line != "y" && line != "n");
			IsCDROMVersion = line == "y";
			Console.WriteLine();
			
			//dump names
			if(File.Exists(@"LISTLIFE\ENGLISH.TXT"))
			{
				string[] names = File.ReadAllLines(@"LISTLIFE\ENGLISH.TXT", Encoding.GetEncoding(850));
				namesByIndex = names
					.Where(x => x.Contains(":"))
					.Select(x =>  x.Split(':'))
					.ToDictionary(x => int.Parse(x[0].TrimStart('@')), x => "\"" + x[1] + "\"");				
			}
			else
			{
				Console.WriteLine("ENGLISH.TXT is required");
				return -1;
			}
			   			
			if(File.Exists(@"LISTLIFE\OBJETS.ITD"))
			{
				allBytes = File.ReadAllBytes(@"LISTLIFE\OBJETS.ITD");
				int count = ReadShort(allBytes[0], allBytes[1]);
			
				int i = 0;
				for(int s = 0 ; s < count ; s++)
				{
					int n = s * 52 + 2;
					int name = ReadShort(allBytes[n+10], allBytes[n+11]);
					if(name != -1 && name != 0)
					{
						objectsByIndex.Add(i, getObjectName(namesByIndex[name], i));
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
		    		   				
			using (TextWriter writer = new StreamWriter("output.txt"))
			{					
				//dump all
				Regex r = new Regex(@"[0-9a-fA-F]{8}\.DAT", RegexOptions.IgnoreCase);				
				foreach(var file in Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LISTLIFE"))
			        .Where(x => r.IsMatch(Path.GetFileName(x)))
	        		.Select(x => new
	                {
                   		FilePath = x,
                   		FileNumber = Convert.ToInt32(Path.GetFileNameWithoutExtension(x), 16)
                	})
		        	.OrderBy(x => x.FileNumber))				    
				{		        	
					writer.WriteLine("#{0} --------------------------------------------------", file.FileNumber);
					Dump(file.FilePath, writer);					
				}
			}	

			return 0;
		}

		public static void Dump(string filename, TextWriter writer)
		{
			List<int> indentation = new List<int>();
			List<int> elseIndent = new List<int>();
			HashSet<int> gotosToIgnore = new HashSet<int>();
			Dictionary<int, string> switchEvalVar = new Dictionary<int, string>();
			List<Tuple<int, int>> switchDefault = new List<Tuple<int, int>>();
						
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

				Tuple<int, int> defaultCase = switchDefault.FirstOrDefault(x => x.Item1 == pos);
				if (defaultCase != null)
				{					
					switchDefault.RemoveAt(switchDefault.IndexOf(defaultCase));					
					WriteLine(writer, indentation.Count(), "DEFAULT\r\n");
					indentation.Add(defaultCase.Item2);
				}
				
				int oldPos = pos;
				int actor = -1;
				int curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
				if((curr & 0x8000) == 0x8000)
				{					
					curr = curr & 0x7FFF;					
					pos += 2;
					actor = ReadShort(allBytes[pos+0], allBytes[pos+1]);	
				}
				
				LifeEnum life = (LifeEnum)Enum.ToObject(typeof(LifeEnum), curr);
				
				//skip gotos
				if(life == LifeEnum.GOTO && gotosToIgnore.Contains(oldPos))
				{
					pos += 4;
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
					WriteLine(writer, indentation.Count(x => x > pos), lifeString + " ");
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
						string paramA = evalvar();						
						string paramB = evalvar();
						if(paramA == "INHAND" || paramA == "COL_BY" || paramA == "HIT_BY" || paramA == "HIT" || paramA == "COLLIDING_ACTOR")
							paramB = objectsByIndex[int.Parse(paramB)];
						
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
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						
						//detect if else
						int beforeGoto = pos+curr*2 - 4;
						int next = ReadShort(allBytes[beforeGoto+0], allBytes[beforeGoto+1]);
						if(next == (int)LifeEnum.GOTO) 
						{
							gotosToIgnore.Add(beforeGoto);
							elseIndent.Add(pos+curr*2);
							indentation.Add(beforeGoto+4+ReadShort(allBytes[beforeGoto+2], allBytes[beforeGoto+3])*2);
						}
						else
						{
							indentation.Add(pos+curr*2);
						}
						
						//check if next instruction is also an if
						int previousPos = pos;
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
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
							
							evalvarImpl(out dummyActor);
							evalvarImpl(out dummyActor);
							
							//check if the two if end up at same place
							curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
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
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("{0}", pos+curr*2);				
						break;						
						
					case LifeEnum.SWITCH:	
						string paramS = evalvar();
						writer.Write("{0}", paramS);	
																			
						//find end of switch
						bool endOfSwitch = false;
						int gotoPos = pos;
						
						//fix for #353 : IF appearing just after switch (while a CASE is expected)
						while(ReadShort(allBytes[gotoPos+0], allBytes[gotoPos+1]) != (int)LifeEnum.CASE &&
						      ReadShort(allBytes[gotoPos+0], allBytes[gotoPos+1]) != (int)LifeEnum.MULTI_CASE)
						{
							gotoPos += 2;
						}
						
						int switchEndGoto = -1;						
						do
						{
							int casePos = ReadShort(allBytes[gotoPos+0], allBytes[gotoPos+1]);
							if(casePos == (int)LifeEnum.CASE)
							{
								switchEvalVar.Add(gotoPos, paramS);
								gotoPos += 4; //skip case + value
								
								//goto just after case 
								gotoPos += 2 + ReadShort(allBytes[gotoPos+0], allBytes[gotoPos+1])*2;							
								if(ReadShort(allBytes[gotoPos-4], allBytes[gotoPos-3]) == (int)LifeEnum.GOTO)
								{
									gotosToIgnore.Add(gotoPos-4); //goto at the end of the case statement (end of switch)
									if(switchEndGoto == -1)
									{										
										switchEndGoto = gotoPos + ReadShort(allBytes[gotoPos-2], allBytes[gotoPos-1])*2;
									}
							   	}
							}							
							else if(casePos == (int)LifeEnum.MULTI_CASE)
							{
								switchEvalVar.Add(gotoPos, paramS);
								gotoPos += 2; //skip multi case
								casePos = ReadShort(allBytes[gotoPos+0], allBytes[gotoPos+1]);
								gotoPos += 2 + casePos * 2; //skip values
								
								//goto just after case
								gotoPos += 2 + ReadShort(allBytes[gotoPos+0], allBytes[gotoPos+1])*2;								
								if(ReadShort(allBytes[gotoPos-4], allBytes[gotoPos-3]) == (int)LifeEnum.GOTO)
								{
									gotosToIgnore.Add(gotoPos-4); //goto at the end of the case statement (end of switch)
									if(switchEndGoto == -1)
									{
										//end of switch
										switchEndGoto = gotoPos + ReadShort(allBytes[gotoPos-2], allBytes[gotoPos-1])*2;
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
							switchDefault.Add(new Tuple<int, int>(gotoPos, switchEndGoto)); //default start + end pos
							indentation.Add(switchEndGoto); //end of switch
						}
						else
						{
							indentation.Add(gotoPos); //end of switch
						}
												
						break;
												
					case LifeEnum.SOUND:
					case LifeEnum.BODY:
						string param = evalvar();
						writer.Write("{0}", param);	
						break;

					case LifeEnum.SAMPLE_THEN:
						string param1 = evalvar();
						writer.Write("{0} ", param1);
						string param2 = evalvar();
						writer.Write("{0}", param2);
						break;
						
					case LifeEnum.CAMERA_TARGET:
					case LifeEnum.TAKE:		
					case LifeEnum.IN_HAND:
					case LifeEnum.DELETE:
					case LifeEnum.FOUND:
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("{0}", objectsByIndex[curr]);
						break;
						
					case LifeEnum.FOUND_NAME:						
					case LifeEnum.MESSAGE:
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("{0}", namesByIndex[curr]);
						break;	

					case LifeEnum.FOUND_FLAG:
					case LifeEnum.FLAGS:	
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("0x{0:X4}", curr);
						break;						
						
					case LifeEnum.LIFE:
					case LifeEnum.ANIM_REPEAT:
					case LifeEnum.COPY_ANGLE:
					case LifeEnum.TEST_COL:							
					case LifeEnum.SPECIAL:
					case LifeEnum.LIFE_MODE:			
					case LifeEnum.FOUND_BODY:					
					case LifeEnum.FOUND_WEIGHT:
					case LifeEnum.ALLOW_INVENTORY:												
					case LifeEnum.WATER:					
					case LifeEnum.MUSIC:
					case LifeEnum.NEXT_MUSIC:
					case LifeEnum.RND_FREQ:
					case LifeEnum.LIGHT:
					case LifeEnum.SHAKING:
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("{0}", curr);
						break;
						
					case LifeEnum.READ:
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("{0} ", curr);
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("{0} ", curr);
						if(IsCDROMVersion)
						{							
							curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
							pos +=2;
							writer.Write("{0}", curr);
						}											
						break;
						
					case LifeEnum.PUT_AT:
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("{0} ", objectsByIndex[curr]);
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("{0}", objectsByIndex[curr]);
						break;
						
					case LifeEnum.MOVE:	
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("{0} ", trackModes[curr]);
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("{0}", curr);
						break;
						
					case LifeEnum.SET_BETA:
					case LifeEnum.SET_ALPHA:					
					case LifeEnum.HIT_OBJECT:
					case LifeEnum.ANIM_ONCE:
					case LifeEnum.ANIM_ALL_ONCE:
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("{0} ", curr);
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;
						writer.Write("{0}", curr);
						break;
												
					case LifeEnum.CASE:
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;

						string lastSwitchVar = switchEvalVar[pos-4];
						if(lastSwitchVar == "INHAND" || lastSwitchVar == "COL_BY" || lastSwitchVar == "HIT_BY"
						   || lastSwitchVar == "HIT" || lastSwitchVar == "COLLIDING_ACTOR")
							writer.Write("{0}", objectsByIndex[curr]);
						else if (lastSwitchVar == "ACTION")
							writer.Write("{0}", getActionName(curr));
						else					
							writer.Write("{0}", curr);	
						
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos +=2;

						indentation.Add(pos+curr*2);									
						break;

					case LifeEnum.MULTI_CASE:
						int numcases =  ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos += 2;
						string lastSwitchVarb = switchEvalVar[pos-4];
						
						for(int n = 0; n < numcases; n++) {
							curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);							
							pos += 2;
							
														
							if(lastSwitchVarb == "INHAND" || lastSwitchVarb == "COL_BY" || lastSwitchVarb == "HIT_BY"
							   || lastSwitchVarb == "HIT" || lastSwitchVarb == "COLLIDING_ACTOR")
								writer.Write("{0}", objectsByIndex[curr]);
							else if (lastSwitchVarb == "ACTION")
								writer.Write("{0}", getActionName(curr));
							else					
								writer.Write("{0}", curr);														
							
							if(n > 0) writer.Write(" ");
							else writer.Write(", ");
						}
						
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
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
						for(int n = 0; n < 4 ; n++) {
							curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
							writer.Write("{0} ", curr);
							pos += 2;
						}
						writer.Write("{0} ", evalvar());
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						writer.Write("{0} ", curr);
						pos += 2;
						break;
						
					case LifeEnum.DEF_ZV:	
					case LifeEnum.FIRE:
						for(int n = 0; n < 6; n++) {
							curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
							writer.Write("{0} ", curr);
							pos += 2;
						}
						break;
						
						
					case LifeEnum.ANIM_MOVE:	
					case LifeEnum.THROW:
						for(int n = 0; n < 7 ; n++) {
							curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
							if(life == LifeEnum.THROW && n == 3)
								writer.Write("{0} ", objectsByIndex[curr]);
							else
								writer.Write("{0} ", curr);
							pos += 2;
						}
						break;
											
					case LifeEnum.PICTURE:						
					case LifeEnum.ANGLE:
						int alpha = curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos += 2;
						int beta = curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos += 2;
						int gamma = curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos += 2;
						writer.Write("{0} {1} {2}", alpha, beta, gamma);
						break;
						
					case LifeEnum.CHANGEROOM:
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						writer.Write("E{0}", curr);
						pos += 2;
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						writer.Write("R{0} ", curr);
						pos += 2;
						
						for(int n = 0; n < 3 ; n++) {
							curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
							writer.Write("{0} ", curr);
							pos += 2;
						}
						break;
						
					case LifeEnum.REP_SAMPLE:											
						writer.Write("{0} ", evalvar());
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						writer.Write("{0} ", curr);
						pos += 2;
						break;
						
					case LifeEnum.DROP:
						string ev = evalvar();
						int obj;
						if(int.TryParse(ev, out obj))
						{
							ev = objectsByIndex[obj];
						}
						writer.Write("{0} ", ev);
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						writer.Write("{0} ", objectsByIndex[curr]);
						pos += 2;
						break;
						
					case LifeEnum.PUT:
						for(int n = 0; n < 9; n++) {
							curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
							writer.Write("{0} ", curr);
							pos += 2;
						}
						break;
					
					case LifeEnum.ANIM_SAMPLE:
						writer.Write(evalvar() + " ");
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						writer.Write(curr + " ");
						pos += 2;
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						writer.Write(curr);
						pos += 2;
						break;
					
					case LifeEnum.SET:
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);						
						writer.Write("VAR{0} = ", curr);
						pos +=2;
						writer.Write("{0}", evalvar());
						break;
						
					case LifeEnum.ADD:	
					case LifeEnum.SUB:	
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);						
						writer.Write("VAR{0} ", curr);
						pos +=2;
						writer.Write("{0}", evalvar());
						break;
						
					case LifeEnum.INC:	
					case LifeEnum.DEC:
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						writer.Write("VAR{0}", curr);
						pos += 2;
						break;
												
					case LifeEnum.C_VAR:
						curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
						pos += 2;						
						writer.Write("{0} = {1}", curr, evalvar());
						break;
						
					default:
						throw new NotImplementedException(life.ToString());						
				}									
				
				if(!consecutiveIfs) writer.WriteLine();
			}
		}
		
		public static string getObjectName(string name, int index)
		{
			string objectName = name.TrimStart('"').TrimEnd('"').ToUpperInvariant();
			objectName = string.Join("_", objectName.Split(' ').Where(x => x != "AN" && x != "A"));			
			return objectName + "_" + index;
		}
		
		public static string getActionName(int value)
		{
			int count = 0;
			int shift = value;
			while(shift > 0)
			{
				shift = shift / 2;
				count++;
			}
			
			if(count > 0 && count < 12)
				return namesByIndex[count+22];			
			else if (count == 12)
				return namesByIndex[22];
			else if (count == 14)
				return "\"Button\"";
			else
				return value.ToString();
		}
		
		public static void WriteLine(TextWriter writer, int indentation, string text)
		{
			writer.Write("{0}{1}",  new String('\t', indentation), text);
		}
		
		public static string evalvar()
		{			
			int actor;
			string eval = evalvarImpl(out actor);
			if(actor != -1) eval = objectsByIndex[actor] + "." + eval;
			return eval;
		}
		
		public static string evalvarImpl(out int actor)
		{
			int curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
			pos +=2;
			
			actor = -1;
			if(curr == -1)
			{
				//CONST
				curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
				pos +=2;
				
				return curr.ToString();
			}
			
			if(curr == 0)
			{
				//CONST
				curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
				pos +=2;
				
				return "VAR" + curr;
			}

			if((curr & 0x8000) == 0x8000) {
				//change actor					
				actor = ReadShort(allBytes[pos+0], allBytes[pos+1]);
				pos +=2;											
			}
			
			curr &= 0x7FFF;
			curr--;
			switch(curr)
			{
				case 0x0:
					return "ACTOR_COLLIDER";
				case 0x1:
					return "TRIGGER";
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
					return "BODYNUM";
				case 0xA:
					return "MARK";
				case 0xB:
					return "NUM_TRACK";
				case 0xC:
					return "CHRONO";
				case 0xD:
					return "ROOM_CHRONO";
				case 0xE:
					curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
					pos += 2;
					return "DIST("+objectsByIndex[curr]+")";
				case 0xF:
					return "COL_BY";
				case 0x10:
					string eval = evalvar();
					int objectIndex;
					if(int.TryParse(eval, out objectIndex))
						return "ISFOUND("+objectsByIndex[objectIndex]+")";
					else
						return "ISFOUND("+eval+")";							
				case 0x11:
					return "ACTION";
				case 0x12:
					curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
					pos += 2;
					return "POSREL("+objectsByIndex[curr]+")";
				case 0x13:
					return "KEYBOARD_INPUT";
				case 0x14:
					return "BUTTON";
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
					curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
					pos += 2;
					return "RAND("+curr+")";
				case 0x1D:
					return "FALLING";
				case 0x1E:
					return "ROOM";
				case 0x1F:
					return "LIFE";
				case 0x20:
					curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
					pos += 2;
					return "OBJECT("+objectsByIndex[curr]+")";
				case 0x21:
					return "ROOMY";
				case 0x22:
					curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
					pos += 2;
					int curr2 = ReadShort(allBytes[pos+0], allBytes[pos+1]);
					pos += 2;
					return "TEST_ZV_END_ANIM (" + curr + " " + curr + ")";
				case 0x23:
					return "MUSIC";
				case 0x24:
					curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
					pos += 2;
					return "C_VAR"+curr;
				case 0x25:
					return "STAGE";
				case 0x26:
					curr = ReadShort(allBytes[pos+0], allBytes[pos+1]);
					pos += 2;
					return "THROW("+objectsByIndex[curr]+")";
				default:
					throw new NotImplementedException(curr.ToString());					
			}
		}
	}
}