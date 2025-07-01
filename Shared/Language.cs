using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Shared
{
	public class Language
	{
		readonly Dictionary<int, string> namesByIndex = new Dictionary<int, string>();

		public void Load()
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

				Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
				foreach (var (Index, Name) in Tools.ReadLines(buffer, Encoding.GetEncoding(850))
					.Where(x => x.Contains(":"))
					.Select(x => x.Split(':'))
					.Where(x => x[1] != string.Empty)
					.Select(x => (Index: int.Parse(x[0].TrimStart('@')), Name: x[1])))
				{
					string name = string.Join("_", Name.ToLowerInvariant()
						.Split(new char[] { ' ', '\'' })
						.Where(x => x != "an" && x != "a"));

					namesByIndex.Add(Index, name);
				}
			}
		}

		public bool TryGetValue(int index, out string name)
		{
			return namesByIndex.TryGetValue(index, out name);
		}
	}
}
