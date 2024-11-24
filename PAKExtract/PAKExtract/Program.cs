using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PAKExtract
{
	class Program
	{
		public static string RootFolder = "GAMEDATA";

		static int Main(string[] args)
		{
			if (Tools.HasArgument(args, "-background"))
			{
				Export.ExportBackground();
			}
			else if (Tools.HasArgument(args, "-svg"))
			{
				Export.ExportSvg(args);
			}
			else if (Tools.HasArgument(args, "-update"))
			{
				UpdateEntries(args);
			}
			else
			{
				var preview = Tools.HasArgument(args, "-info");

				var files = GetFiles(args).ToList();
				if (!files.Any())
				{
					Directory.CreateDirectory(RootFolder);
					foreach (var filePath in Directory.GetFiles(RootFolder, "*.PAK", SearchOption.TopDirectoryOnly))
					{
						files.Add(filePath);
					}
				}

				foreach (var file in files)
				{
					ExtractFile(file, preview);
				}
			}

			return 0;
		}

		static IEnumerable<string> GetFiles(string[] args)
		{
			for (int i = 0; i < args.Length; i++)
			{
				var arg = args[i];
				if (!arg.StartsWith("-"))
				{
					if (Directory.Exists(arg))
					{
						foreach (var filePath in Directory.GetFiles(arg, "*.PAK", SearchOption.TopDirectoryOnly))
						{
							yield return filePath;
						}
					}
					else if (File.Exists(arg))
					{
						yield return arg;
					}
					else
					{
						throw new FileNotFoundException($"Cannot find file or folder '{arg}'");
					}
				}
			}
		}

		static void ExtractFile(string filePath, bool preview)
		{
			using (var pak = new PakArchive(filePath))
			{
				if (preview)
				{
					ArchiveInfo(pak, filePath);
				}
				else
				{
					foreach (var entry in pak)
					{
						var destPath = Path.Combine(Path.GetFileNameWithoutExtension(filePath), $"{entry.Index:D8}.dat");
						WriteFile(destPath, entry.Read());
					}
				}
			}
		}

		static void ArchiveInfo(PakArchive pak, string filePath)
		{
			var compressType = new[] { "-", "INFLA", "", "", "DEFLA" };
			Console.WriteLine(Path.GetFileName(filePath));
			Console.WriteLine($"Entry\tSize\tCSize\tCType\tExtra");
			foreach (var entry in pak)
			{
				Console.WriteLine($"{entry.Index,5}\t{entry.UncompressedSize,5}\t{(entry.CompressedSize != entry.UncompressedSize ? entry.CompressedSize.ToString() : "-"),5}\t{compressType[entry.CompressionType],5}\t{string.Join(" ", Enumerable.Range(0, entry.Extra.Length / 4).Select(x => entry.Extra.ReadInt(x * 4)))}");
			}
			Console.WriteLine();
		}

		public static void WriteFile(string filePath, byte[] data)
		{
			if (!File.Exists(filePath) || data.Length != new FileInfo(filePath).Length || !Enumerable.SequenceEqual(File.ReadAllBytes(filePath), data))
			{
				Console.WriteLine($"{filePath} {data.Length}");
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				File.WriteAllBytes(filePath, data);
			}
		}

		static int UpdateEntries(string[] args)
		{
			var files = GetFiles(args).ToArray();
			foreach (var pakFile in files)
			{
				var directory = Path.GetFileNameWithoutExtension(pakFile);
				if (Directory.Exists(directory))
				{
					var entries = PakArchive.Load(pakFile);
					foreach (var file in Directory.GetFiles(directory, "*.*"))
					{
						int index = int.Parse(Path.GetFileNameWithoutExtension(file));
						if (index < 0 || index >= entries.Length)
						{
							Console.Error.Write($"Invalid entry index {index} for {Path.GetFileName(pakFile)}");
							return -1;
						}

						entries[index].Write(File.ReadAllBytes(file));
						Console.WriteLine($"{Path.GetFileName(pakFile)} entry {index} updated");
					}

					PakArchive.Save(pakFile, entries);
				}
			}

			return 0;
		}

	}
}
