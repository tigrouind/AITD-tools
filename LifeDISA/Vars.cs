
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LifeDISA
{
	public class Vars
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
				"BODYS",				
				"PLAYER BODY",
				"ACTIONS",
				"POSREL",
				"KEYBOARD INPUT"
			}
		);
		
		public string GetText(string sectionName, string value, string defaultText = null, bool nospaces = true)
		{
			int parsedNumber;
			if(int.TryParse(value, out parsedNumber))
			{
				return GetText(sectionName, parsedNumber, defaultText, nospaces);
			}
			
			if (defaultText != null)
			 	return defaultText;
			
			return value;
		}
		
		public string GetText(string sectionName, int value, string defaultText = null, bool nospaces = true)
		{
			string text;
			Dictionary<int, string> section;
			
			if (sections.TryGetValue(sectionName, out section))
		    {
	 		   	if(section.TryGetValue(value, out text))
				{	
	 		   		if(nospaces)
	 		   		{
	 		   			text = Regex.Replace(text, "[^A-Za-z0-9 ]", string.Empty);
						text = Regex.Replace(text, "\\s+", "_");
	 		   		}
	 		   		
					return text;
				}
			}
							
			if (defaultText != null)
			 	return defaultText;
			
			return value.ToString();
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
				
				//check if new section 
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
				
				//parse line if inside section
				if(currentSection != null)
				{
					Match itemMatch = item.Match(line);
					if(itemMatch.Success)
					{						
						int lineNumber = int.Parse(itemMatch.Groups[1].Value);
						string nextNumberString = itemMatch.Groups[3].Value.Trim();
						int nextNumber;
						if(!string.IsNullOrEmpty(nextNumberString))
						{																										
							nextNumber = int.Parse(nextNumberString);
						}
						else
						{
							nextNumber = lineNumber;
						}
												
						string text = itemMatch.Groups[4].Value.Trim();
						if(!string.IsNullOrEmpty(text))
						{
							for(int j = lineNumber; j <= nextNumber ; j++)
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
