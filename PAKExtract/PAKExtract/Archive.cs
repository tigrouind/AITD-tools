using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace PAKExtract
{
	public class Archive
	{
		public bool Timegate;

		public static void CreateArchive(string[] args, bool timegate)
		{
			foreach (var folder in args)
			{
				if (!Directory.Exists(folder))
				{
					Console.Error.WriteLine($"Cannot find folder '{folder}'");
				}
				else
				{
					var entries = new List<PakArchiveEntry>();
					foreach (var filePath in Directory.EnumerateFiles(folder, "*.*"))
					{
						var data = File.ReadAllBytes(filePath);
						var extraPath = Path.Combine(Path.GetDirectoryName(filePath), "EXTRA", Path.GetFileName(filePath));
						var extra = File.Exists(extraPath) ? File.ReadAllBytes(extraPath) : Array.Empty<byte>();

						byte[] compressedData = data;
						if (timegate)
						{
							using (var memoryStream = new MemoryStream())
							{
								using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress))
								{
									deflateStream.Write(data, 0, data.Length);
								}

								compressedData = memoryStream.ToArray();
							}
						}
						entries.Add(new PakArchiveEntry(data, extra, compressedData));
					}

					string pakFile = Path.Combine("GAMEDATA", $"{folder}.PAK");
					Program.WriteFile(pakFile, PakArchive.Save(entries));
				}
			}
		}
	}
}
