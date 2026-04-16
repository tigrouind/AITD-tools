using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace PAKExtract
{
	public class Archive
	{
		public static void CreateArchive(string[] args, bool timegate)
		{
			string dosBoxPath = DosBoxZip.GetDosBoxPath();

			foreach (var folder in args)
			{
				if (!Directory.Exists(folder))
				{
					Console.Error.WriteLine($"Cannot find folder '{folder}'");
				}
				else
				{
					CreateArchive(folder);
				}
			}

			void CreateArchive(string folder)
			{
				var pakFile = Path.Combine("GAMEDATA", $"{folder}.PAK");
				var entries = new List<PakArchiveEntry>();

				PakArchiveEntry[] existingEntries = [];
				if (File.Exists(pakFile)) //load all entries to compare them with files to be compressed and see if it has changed
				{
					existingEntries = PakArchive.Load(pakFile);
				}

				//try to see if file has been changed, if not use file from already existing PAK archive
				var filesToCompressWithDOSBox = new List<(string FilePath, PakArchiveEntry Entry)>();
				foreach (var filePath in Directory.EnumerateFiles(folder, "*.*"))
				{
					entries.Add(GetEntry());
					
					PakArchiveEntry GetEntry()
					{
						var data = File.ReadAllBytes(filePath);
						var extraPath = Path.Combine(Path.GetDirectoryName(filePath)!, "EXTRA", Path.GetFileName(filePath));
						var extra = File.Exists(extraPath) ? File.ReadAllBytes(extraPath) : [];

						byte[] compressedData = data;
						byte compressionType = 0; //uncompressed
						if (timegate)
						{
							using var memoryStream = new MemoryStream();
							using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress))
							{
								deflateStream.Write(data, 0, data.Length);
							}

							compressedData = memoryStream.ToArray();
							compressionType = 4; //gzip
						}
						else
						{
							var filename = Path.GetFileNameWithoutExtension(filePath);
							if (Regex.IsMatch(filename, "[0-9]+"))
							{
								int entryIndex = int.Parse(filename);
								if (entryIndex >= 0 && entryIndex < existingEntries.Length)
								{
									var entry = existingEntries[entryIndex];
									if (Enumerable.SequenceEqual(entry.Read(), data)) //file unmodified
									{
										return entry; //untouched
									}
								}
							}

							if (!string.IsNullOrEmpty(dosBoxPath))
							{
								var entry = new PakArchiveEntry(data.Length, extra, compressedData, compressionType);
								filesToCompressWithDOSBox.Add((filePath, entry)); //for later on
								return entry; //untouched
							}
						}

						return new PakArchiveEntry(data.Length, extra, compressedData, compressionType);
					}
				}

				if (filesToCompressWithDOSBox.Any())
				{
					DosBoxZip.CompressWithDosBox(dosBoxPath, filesToCompressWithDOSBox); //will modify "entries" indirectly
				}

				Program.WriteFile(pakFile, PakArchive.Save(entries));
			}

			
		}
	}
}
