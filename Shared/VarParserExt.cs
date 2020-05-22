using System;
using System.Text.RegularExpressions;

namespace Shared
{
	public class VarParserExt : VarParser
	{
		public override string GetText(string sectionName, int value)
		{
			return GetText(sectionName, value, null);
		}

		public string GetText(string sectionName, string value)
		{
			int parsedNumber;
			if(int.TryParse(value, out parsedNumber))
			{
				return GetText(sectionName, parsedNumber);
			}

			return value;
		}

		public string GetText(string sectionName, int value, string defaultText)
		{
			string text = base.GetText(sectionName, value);

			if (!string.IsNullOrEmpty(text))
			{
				text = Regex.Replace(text, @"-", " ");
				text = Regex.Replace(text, @"/", " or ");
				text = Regex.Replace(text, @"[^A-Za-z0-9 ]", string.Empty);
				text = Regex.Replace(text, @"\s+", "_");

				return text;
			}

			if (defaultText != null)
				return defaultText;

			return value.ToString();
		}
	}
}