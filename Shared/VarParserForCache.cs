using System.Text.RegularExpressions;

namespace Shared
{
	public class VarParserForCache : VarParser
	{
		protected override string FormatText(string text)
		{
			var result = Regex.Replace(text, @"^(E\d+|R\d+|-|player)+\s+", string.Empty, RegexOptions.IgnoreCase);
			return result.ToLowerInvariant();
		}
	}
}
