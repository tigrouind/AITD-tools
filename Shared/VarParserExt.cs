using System;
using System.Text.RegularExpressions;

namespace Shared
{
	public class VarParserExt : VarParser
	{
		public string GetText(VarEnum section, string value)
		{
			int parsedNumber;
			if (int.TryParse(value, out parsedNumber))
			{
				return GetText(section, parsedNumber);
			}

			return value;
		}

		public override string GetText(VarEnum section, int value)
		{
			return GetText(section, value, null);
		}

		public string GetText(VarEnum section, int value, string defaultText, bool includeId = true)
		{
			string text = base.GetText(section, value);
			if (!string.IsNullOrEmpty(text))
			{
				if (includeId)
				{
					return text + "_" + value;
				}

				return text;
			}

			if (defaultText != null)
			{
				return defaultText;
			}

			return value.ToString();
		}

		protected override string FormatText(string text)
		{
			text = Regex.Replace(text, @"-", " ");
			text = Regex.Replace(text, @"/", " or ");
			text = Regex.Replace(text, @"[^A-Za-z0-9 ]", string.Empty);
			text = Regex.Replace(text, @"\s+", "_");

			return text.ToLowerInvariant();
		}
	}
}