using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PAKExtract
{
	class Program
	{
		public static string RootFolder = "GAMEDATA";

		static int Main(string[] args)
		{
			return CommandLine.ParseAndInvoke(args, new Func<string[], bool, SvgInfo, Archive, bool, int>(Run));
		}

		static int Run(string[] args, bool background, SvgInfo svg, Archive archive, bool info)
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
			else if (archive != null)
			{
				Archive.CreateArchive(args, archive.Timegate);
			}
			else
			{
				var files = GetFiles(args)
					.ToList();

				var compressType = new[] { "-", "INFLATE", "", "", "DEFLATE" };
				if (info && files.Any())
				{
					Console.WriteLine("     PAK Entry   CType   CSize   USize Extra");
				}

				foreach (var file in files)
				{
					using var pak = new PakArchive(file);
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
							if (entry.Extra.Length > 0)
							{
								WriteFile(Path.Combine(Path.GetDirectoryName(destPath), "EXTRA", Path.GetFileName(destPath)), entry.Extra);
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
				if (File.Exists(arg))
				{
					yield return arg;
				}
				else if (Directory.Exists(arg) && Directory.EnumerateFiles(arg, "*.PAK", SearchOption.TopDirectoryOnly).Any())
				{
					foreach (var filePath in Directory.EnumerateFiles(arg, "*.PAK", SearchOption.TopDirectoryOnly))
					{
						yield return filePath;
					}
				}
				else if (File.Exists(Path.Combine(RootFolder, arg)))
				{
					yield return Path.Combine(RootFolder, arg);
				}
				else
				{
					Console.Error.WriteLine($"Cannot find file or folder '{arg}'");
					yield break;
				}
			}

			if (!args.Any())
			{
				Directory.CreateDirectory(RootFolder);
				foreach (var filePath in Directory.EnumerateFiles(RootFolder, "*.PAK", SearchOption.TopDirectoryOnly))
				{
					yield return filePath;
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
	}
}
