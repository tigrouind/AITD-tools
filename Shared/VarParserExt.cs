using System;
using System.Text.RegularExpressions;

namespace Shared
{
	public class VarParserExt : VarParser
	{
		public string GetText(VarEnum section, string value)
		{
			int parsedNumber;
			if(int.TryParse(value, out parsedNumber))
			{
				return GetText(section, parsedNumber);
			}

			return value;
		}

		public string GetText(VarEnum varsEnum, int value, string defaultText = null)
		{
			string text = base.GetText(varsEnum, value);

			if (!string.IsNullOrEmpty(text))
			{
				text = Regex.Replace(text, @"-", " ");
				text = Regex.Replace(text, @"/", " or ");
				text = Regex.Replace(text, @"[^A-Za-z0-9 ]", string.Empty);
				text = Regex.Replace(text, @"\s+", "_");

				return text.ToLowerInvariant();
			}

			if (defaultText != null)
				return defaultText;

			return value.ToString();
		}
	}
}