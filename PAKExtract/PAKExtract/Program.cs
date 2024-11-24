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
				var preview = Tools.HasArgument(args, "-preview");

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
				if (!arg.StartsWith("-") && (i == 0 || !args[i - 1].StartsWith("-")))
				{
					if (File.Exists(arg))
					{
						yield return arg;
					}
					else
					{
						throw new FileNotFoundException($"Cannot find file '{arg}'");
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
			Console.WriteLine(Path.GetFileName(filePath));
			Console.WriteLine($"Entry\tCSize\tUSize\tCType");
			Console.WriteLine("------------------------------");
			foreach (var entry in pak)
			{
				Console.WriteLine($"{entry.Index,3}\t{entry.CompressedSize,5}\t{entry.UncompressedSize,5}\t{entry.CompressionType}");
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
						if (index < 0 || index > entries.Length)
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
