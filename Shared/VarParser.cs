using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shared
{
	public class VarParser
	{
		readonly Dictionary<VarEnum, Dictionary<int, string>> sections = [];

		public virtual string GetText(VarEnum section, int value)
		{
			if (sections.TryGetValue(section, out Dictionary<int, string> sectionDict))
			{
				if (sectionDict.TryGetValue(value, out string text))
				{
					return text;
				}
			}

			return string.Empty;
		}

		public void Load(string filePath, params VarEnum[] sectionsToParse)
		{
			var allowedSections = sectionsToParse.ToDictionary(x => x.ToString(), x => x);
			var allLines = File.ReadLines(filePath);

			Dictionary<int, string> currentSection = null;
			Regex regex = new(@"^(?<from>[0-9]+)(-(?<to>[0-9]+))?\s+(?<text>.*)$");

			foreach (string line in allLines)
			{
				//check if new section
				if (line.Length > 0 && line[0] >= 'A' && line[0] <= 'Z')
				{
					if (allowedSections.TryGetValue(line, out VarEnum section))
					{
						currentSection = CreateNewSection(section);
					}
					else
					{
						currentSection = null;
					}
				}
				else if (currentSection != null)
				{
					//parse line if inside section
					Match match = regex.Match(line);
					if (match.Success)
					{
						string from = match.Groups["from"].Value;
						string to = match.Groups["to"].Value;
						string text = match.Groups["text"].Value.Trim();

						AddEntry(currentSection, from, to, FormatText(text));
					}
				}
			}
		}

		Dictionary<int, string> CreateNewSection(VarEnum section)
		{
			Dictionary<int, string> sectionDict = [];
			sections.Add(section, sectionDict);
			return sectionDict;
		}

		static void AddEntry(Dictionary<int, string> section, string fromString, string toString, string text)
		{
			if (!(string.IsNullOrEmpty(text) || text.Trim() == string.Empty))
			{
				int from = int.Parse(fromString);
				int to = string.IsNullOrEmpty(toString) ? from : int.Parse(toString);

				for (int i = from; i <= to ; i++)
				{
					section[i] = text;
				}
			}
		}

		protected virtual string FormatText(string text)
		{
			return text;
		}
	}
}