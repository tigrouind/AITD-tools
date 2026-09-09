using Shared;
using System.Text.RegularExpressions;

namespace VarsViewer
{
	public class VarParserForCache : VarParser
	{
		protected override string FormatText(string text)
		{
			text = Regex.Replace(text, @"^(E\d+|R\d+|-|player)+\s+", string.Empty, RegexOptions.IgnoreCase);
			text = Regex.Replace(text, @"(""|\(|\))", string.Empty);
			return text.ToLowerInvariant();
		}
	}
}
