using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Shared
{
	public static class Language
	{
		public static Dictionary<int, string> Load()
		{
			var languagePakFiles = new[] { "ENGLISH.PAK", "FRANCAIS.PAK", "DEUTSCH.PAK", "ESPAGNOL.PAK", "ITALIANO.PAK", "USA.PAK" };
			string languageFile = languagePakFiles
				.Select(x => Path.Combine("GAMEDATA", x))
				.FirstOrDefault(File.Exists);

			if (languageFile != null)
			{
				byte[] buffer;
				using (var pak = new PakArchive(languageFile))
				{
					buffer = pak[0].Read();
				}

				return Tools.ReadLines(buffer, Encoding.GetEncoding(850))
					.Where(x => x.Contains(":"))
					.Select(x => x.Split(':'))
					.Where(x => x[1] != string.Empty)
					.ToDictionary(x => int.Parse(x[0].TrimStart('@')), x => x[1]);
			}

			return default;
		}
	}
}
