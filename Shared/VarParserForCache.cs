using System.Text.RegularExpressions;

namespace Shared
{
	public class VarParserForCache : VarParser
	{
		public string GetText(VarEnum section, int value, int maxLength)
		{
			var text = GetText(section, value);

			if (text.Length > maxLength)
			{
				return text.Substring(0, maxLength);
			}

			return text;
		}

		protected override string FormatText(string text)
		{
			var result = Regex.Replace(text, @"^(E\d+|R\d+|-|player)+\s+", string.Empty, RegexOptions.IgnoreCase);
			return result.ToLowerInvariant();
		}
	}
}
