
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LifeDISA
{
	public class Parser
	{
		Dictionary<string, Dictionary<int, string>> sections 
			= new Dictionary<string, Dictionary<int, string>>();
		
		HashSet<string> sectionsToParse = new HashSet<string>(
			new[]
			{
				"VARS",
				"LIFES",
				"ANIMS",
				"TRACKS",
				"SOUNDS",
				"MUSIC",
				"BODYS"
			}
		);
		
		public string GetValue(string sectionname, string number, string defaultValue = "", bool nospaces = false)
		{
			int parsedNumber;
			if(int.TryParse(number, out parsedNumber))
			{
				return GetValue(sectionname, parsedNumber, defaultValue, nospaces);
			}
			
			return defaultValue;
		}
		
		public string GetValue(string sectionname, int number, string defaultValue = "", bool nospaces = false)
		{
			string text;
			Dictionary<int, string> section;
			
			if (sections.TryGetValue(sectionname, out section))
		    {
	 		   	if(section.TryGetValue(number, out text))
				{	
	 		   		if(nospaces)
	 		   		{
	 		   			text = Regex.Replace(text, "[^A-Za-z0-9 ]", string.Empty);
						text = Regex.Replace(text, "\\s+", "_");
	 		   		}
	 		   		
					return text;
				}
			}
							
			return defaultValue;
		}
		
		public void Load(string filePath)
		{
			var allLines = System.IO.File.ReadAllLines(filePath);
			Regex section = new Regex("^[A-Z]+");
			Regex item = new Regex("^([0-9]+)(-([0-9]+))?(.*)");
			Dictionary<int, string> currentSection = null;
			
			int i = 0;
			while(i < allLines.Length)
			{
				string line = allLines[i].Trim();
				if(section.IsMatch(line))
				{
					if(sectionsToParse.Contains(line))
					{
						currentSection = new Dictionary<int, string>();
						sections.Add(line, currentSection);
						i++;
						continue;
					}
					
					currentSection = null;
				}
				
				if(currentSection != null)
				{
					Match itemMatch = item.Match(line);
					if(itemMatch.Success)
					{						
						string lineNumber = int.Parse(itemMatch.Groups[1].Value).ToString();
						string nextNumber = itemMatch.Groups[3].Value.Trim();
						if(!string.IsNullOrEmpty(nextNumber))
						{													
							nextNumber = int.Parse(itemMatch.Groups[3].Value).ToString(); //remove leading zeroes							 
							while(nextNumber.Length < lineNumber.Length)
							{
								nextNumber = lineNumber[lineNumber.Length - nextNumber.Length - 1] + nextNumber;
							}
						}
						else
						{
							nextNumber = lineNumber;
						}
												
						string text = itemMatch.Groups[4].Value.Trim('-', ' ');
						if(!string.IsNullOrEmpty(text))
						{
							for(int j = int.Parse(lineNumber); j <= int.Parse(nextNumber) ; j++)
							{								
								currentSection[j] = text;
							}
						}
					}
				}				
					
				i++;
			}
		}
	}
}
