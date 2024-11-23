using Shared;
using System;
using System.IO;
using System.Linq;

namespace PAKExtract
{
	class Program
	{
		public static string RootFolder = "GAMEDATA";
		static bool preview;

		static int Main(string[] args)
		{
			if (Tools.HasArgument(args, "-background"))
			{
				Export.ExportBackground();
				return 0;
			}
			else if (Tools.HasArgument(args, "-svg"))
			{
				Export.ExportSvg(args);
				return 0;
			}

			preview = Tools.HasArgument(args, "-preview");
			bool foundFile = false;
			for (int i = 0; i < args.Length; i++)
			{
				var arg = args[i];
				if (!arg.StartsWith("-") && (i == 0 || !args[i - 1].StartsWith("-")))
				{
					if (File.Exists(arg))
					{
						if (Tools.HasArgument(args, "-update"))
						{
							var updateArgs = Tools.GetArgument<string>(args, "-update");
							var updateParam = updateArgs.Split(' ');
							string inputFile = updateParam[0];
							int index = int.Parse(updateParam[1]);
							return UpdateFile(arg, inputFile, index);
						}
						else
						{
							ExtractFile(arg);
						}
						foundFile = true;
					}
					else
					{
						Console.Error.Write($"Cannot find file '{arg}'");
						return -1;
					}
				}
			}

			if (!foundFile)
			{
				try
				{
					ExtractFiles();
				}
				catch (FileNotFoundException ex)
				{
					Console.Error.Write($"{Path.GetFileName(ex.FileName)} not found");
					return -1;
				}
			}

			return 0;
		}

		static void ExtractFiles()
		{
			Directory.CreateDirectory(RootFolder);
			foreach (var filePath in Directory.GetFiles(RootFolder, "*.PAK", SearchOption.TopDirectoryOnly))
			{
				ExtractFile(filePath);
			}
		}

		static int UpdateFile(string filePath, string inputFile, int index)
		{
			var entries = PakArchive.Load(filePath);
			if (index < 0 || index > entries.Length)
			{
				Console.Error.Write($"Invalid entry index {index} for {Path.GetFileName(filePath)}");
				return -1;
			}

			entries[index].Write(File.ReadAllBytes(inputFile));
			PakArchive.Save(filePath, entries);
			Console.WriteLine($"Entry {index} in {Path.GetFileName(filePath)} updated");
			return 0;
		}

		static void ExtractFile(string filePath)
		{
			string fileName = Path.GetFileNameWithoutExtension(filePath);

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
						var destPath = Path.Combine(fileName, $"{entry.Index:D8}.dat");
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
	}
}
