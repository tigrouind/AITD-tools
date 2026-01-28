using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
			string dosBoxPath = null;

			if (File.Exists("pkzip.exe")) //cannot use DOSBox compression without PKZIP.exe
			{
				var searchDirectories = new string[]
				{
					".",
					Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
					Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
				};

				dosBoxPath = searchDirectories
				.SelectMany(x => Directory.GetDirectories(x))
					.Where(x => x.Contains("dosbox", StringComparison.InvariantCultureIgnoreCase))
					.Select(x => Path.Combine(x, "dosbox.exe"))
					.FirstOrDefault(x => File.Exists(x));
			}

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
				if (File.Exists(pakFile)) //try to see if file has been changed, if not use file from already existing PAK archive
				{
					existingEntries = PakArchive.Load(pakFile);
				}

				var filesToCompressWithDOSBox = new List<(string FilePath, PakArchiveEntry Entry)>();
				foreach (var filePath in Directory.EnumerateFiles(folder, "*.*"))
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
									entries.Add(entry); //leave untouched
									continue;
								}
							}
						}

						if (!string.IsNullOrEmpty(dosBoxPath))
						{
							var entry = new PakArchiveEntry(data.Length, extra, compressedData, compressionType);
							filesToCompressWithDOSBox.Add((filePath, entry)); //for later on
							entries.Add(entry); //might be modified later, after DOSBOX compression
							continue;
						}
					}

					entries.Add(new PakArchiveEntry(data.Length, extra, compressedData, compressionType));
				}

				if (filesToCompressWithDOSBox.Any())
				{
					var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
					Directory.CreateDirectory(tempDirectory);
					Directory.CreateDirectory(Path.Combine(tempDirectory, "FILES"));

					try
					{
						//copy pkzip and all files to be compressed to temporary folder
						File.Copy("pkzip.exe", Path.Combine(tempDirectory, "pkzip.exe"));
						foreach (var (filePath, _) in filesToCompressWithDOSBox)
						{
							File.Copy(filePath, Path.Combine(tempDirectory, "FILES", Path.GetFileName(filePath)));
						}

						var destFile = Path.Combine(tempDirectory, "temp.zip");
						RunDOSBox(dosBoxPath, destFile);
						if (File.Exists(destFile))
						{
							var data = File.ReadAllBytes(destFile);
							var result = GetCompressedDataFromZip(data);

							foreach (var (filePath, entry) in filesToCompressWithDOSBox)
							{
								//some entries might be missing (eg: file wasn't compressed because it make no sense (eg: stored compression type)
								if (result.TryGetValue(Path.GetFileName(filePath), out var compressedInfo))
								{
									var (compressedData, uncompressedSize) = compressedInfo;
									if (uncompressedSize == entry.UncompressedSize)
									{
										entry.Write(compressedData);
										entry.CompressionType = 1;
									}
								}
							}
						}
					}
					finally
					{
						Directory.Delete(tempDirectory, true);
					}
				}

				Program.WriteFile(pakFile, PakArchive.Save(entries));
			}
		}

		static Dictionary<string, (byte[] ComparessedData, int UncompressedSize)> GetCompressedDataFromZip(byte[] data)
		{
			int offset = 0;
			var result = new Dictionary<string, (byte[] ComparessedData, int UncompressedSize)>(StringComparer.InvariantCultureIgnoreCase);
			while ((offset + 0x20) <= data.Length &&
					Tools.ReadUnsignedInt(data, offset + 0) == 0x04034b50) //PKZIP header (v1.1 should be used)
			{
				var compressionType = data.ReadUnsignedShort(offset + 0x08);
				var compressedSize = data.ReadUnsignedShort(offset + 0x12);
				var uncompressedSize = data.ReadUnsignedShort(offset + 0x16);
				var fileNameLen = data.ReadUnsignedShort(offset + 0x1a);
				var extraLen = data.ReadUnsignedShort(offset + 0x1c);
				var fileName = data.ReadString(offset + 0x1e, fileNameLen);

				offset += 0x1e + fileNameLen + extraLen;
				if ((offset + compressedSize) <= data.Length
					&& compressionType == 6  //implode
					&& Tools.ReadUnsignedInt(data, offset) == 0x0312000F) //implode signature
				{
					result[fileName] = ([.. data.AsSpan(offset, compressedSize)], uncompressedSize);
				}

				offset += compressedSize;
			}

			return result;
		}

		static void RunDOSBox(string dosboxExePath, string directory)
		{
			var psi = new ProcessStartInfo
			{
				FileName = dosboxExePath,
				UseShellExecute = false,
				CreateNoWindow = false,
			};

			psi.ArgumentList.Add("-c");
			psi.ArgumentList.Add("cycles max");

			psi.ArgumentList.Add("-c");
			psi.ArgumentList.Add($"mount c \"{Path.GetDirectoryName(directory)}\"");

			psi.ArgumentList.Add("-c");
			psi.ArgumentList.Add("c:");

			psi.ArgumentList.Add("-c");
			psi.ArgumentList.Add($"pkzip -ei C:\\{Path.GetFileName(directory)} C:\\FILES\\*.*");

			psi.ArgumentList.Add("-c");
			psi.ArgumentList.Add("exit");

			using var p = Process.Start(psi);
			p.WaitForExit();
		}
	}
}
