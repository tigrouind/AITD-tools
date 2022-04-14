using System;
using System.Text.RegularExpressions;
using Shared;

namespace CacheViewer
{
	public class VarParserExt : VarParser
	{
		protected override string FormatText(string text)
		{			
			const int maximumSize = 7;			
			text = Regex.Replace(text, @"^(E\d+|R\d+|-|player)+\s+", string.Empty, RegexOptions.IgnoreCase);
			if (text.Length > maximumSize) 
			{
				text = text.Substring(0, maximumSize);
			}
			return text.ToLowerInvariant();
		}
	}
}