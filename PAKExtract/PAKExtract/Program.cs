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
			return CommandLine.ParseAndInvoke(args, new Func<string[], bool, SvgInfo, bool, bool, int>(Run));
		}

		static int Run(string[] args, bool background, SvgInfo svg, bool update, bool info)
		{
			if (background)
			{
				if (InvalidArguments(args))
				{
					return -1;
				}
				Export.ExportBackground();
			}
			else if (svg != null)
			{
				if (InvalidArguments(args))
				{
					return -1;
				}
				Export.ExportSvg(svg);
			}
			else if (update)
			{
				UpdateEntries(args);
			}
			else
			{
				var files = GetFiles(args).ToList();
				if (!files.Any() && !args.Any())
				{
					Directory.CreateDirectory(RootFolder);
					foreach (var filePath in Directory.EnumerateFiles(RootFolder, "*.PAK", SearchOption.TopDirectoryOnly))
					{
						files.Add(filePath);
					}
				}

				var compressType = new[] { "-", "INFLATE", "", "", "DEFLATE" };
				if (info)
				{
					Console.WriteLine("     PAK Entry   CType   CSize   USize Extra");
				}

				foreach (var file in files)
				{
					using (var pak = new PakArchive(file))
					{
						foreach (var entry in pak)
						{
							if (info)
							{
								Console.WriteLine($"{Path.GetFileNameWithoutExtension(file),8} " +
									$"{entry.Index,5} " +
									$"{(entry.CompressionType >= 0 && entry.CompressionType <= 4 ? compressType[entry.CompressionType] : entry.CompressionType.ToString()),7} " +
									$"{(entry.CompressionType != 0 ? entry.CompressedSize.ToString() : "-"),7} " +
									$"{entry.UncompressedSize,7} " +
									$"{string.Join(" ", Enumerable.Range(0, entry.Extra.Length / 4).Select(x => entry.Extra.ReadInt(x * 4)))}");
							}
							else
							{
								var destPath = Path.Combine(Path.GetFileNameWithoutExtension(file), $"{entry.Index:D8}.dat");
								WriteFile(destPath, entry.Read());
							}
						}
					}
				}
			}

			return 0;
		}

		static bool InvalidArguments(string[] args)
		{
			if (args.Any())
			{
				Console.Error.WriteLine($"Invalid argument(s): {string.Join(", ", args)}");
				return true;
			}

			return false;
		}

		static IEnumerable<string> GetFiles(string[] args)
		{
			foreach (var arg in args)
			{
				if (Directory.Exists(arg))
				{
					foreach (var filePath in Directory.EnumerateFiles(arg, "*.PAK", SearchOption.TopDirectoryOnly))
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
					Console.Error.WriteLine($"Cannot find file or folder '{arg}'");
				}
			}
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
					foreach (var file in Directory.EnumerateFiles(directory, "*.*"))
					{
						int index = int.Parse(Path.GetFileNameWithoutExtension(file));
						if (index < 0 || index >= entries.Length)
						{
							Console.Error.Write($"Invalid entry index {index} for {Path.GetFileName(pakFile)}");
							return -1;
						}

						var previousData = entries[index].Read();
						var data = File.ReadAllBytes(file);
						if (previousData.Length != data.Length || !Enumerable.SequenceEqual(previousData, data))
						{
							entries[index].Write(data);
							Console.WriteLine($"{Path.GetFileName(pakFile)} entry {index} updated");
						}
					}

					PakArchive.Save(pakFile, entries);
				}
			}

			return 0;
		}

	}
}
