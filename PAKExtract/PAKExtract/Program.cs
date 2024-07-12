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

		static GameVersion version;
		static bool raw;
		static int[] rooms;
		static int rotate;

		static void Main(string[] args)
		{
			version = Tools.GetArgument<GameVersion>(args, "-version");
			raw = Tools.HasArgument(args, "-raw");
			rooms = (Tools.GetArgument<string>(args, "-rooms") ?? string.Empty)
				.Split(',')
				.Where(x => x != string.Empty)
				.Select(x => int.Parse(x)).ToArray();
			rotate = Tools.GetArgument<int>(args, "-rotate");

			bool foundFile = false;
			foreach (var arg in args)
			{
				if (!arg.StartsWith("-") && File.Exists(arg))
				{
					ExtractFile(arg);
					foundFile = true;
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
					Console.WriteLine($"{Path.GetFileName(ex.FileName)} not found");
				}
			}
		}

		static void ExtractFiles()
		{
			Directory.CreateDirectory(RootFolder);
			foreach (var filePath in Directory.GetFiles(RootFolder, "*.PAK", SearchOption.TopDirectoryOnly))
			{
				ExtractFile(filePath);
			}
		}

		static void ExtractFile(string filePath)
		{
			string fileName = Path.GetFileNameWithoutExtension(filePath);

			using (var pak = new PakArchive(filePath))
			{
				if (fileName.StartsWith("ETAGE") && !raw)
				{
					ExportSVG(pak, fileName, rooms, rotate, version);
					if (version == GameVersion.AITD1 || version == GameVersion.AITD1_FLOPPY || version == GameVersion.AITD1_DEMO)
					{
						RenderMasks(pak, fileName, null, MaskAITD1.RenderMasks);
					}
				}
				else if ((fileName.StartsWith("MASK") || fileName.StartsWith("NASK")) && !raw)
				{
					RenderMasks(pak, fileName, null, MaskAITD2.RenderMasks);
				}
				else if ((fileName.StartsWith("MK") || fileName.StartsWith("NK")) && !raw)
				{
					RenderMasks(pak, fileName.Substring(0, 4), int.Parse(fileName.Substring(4, 2)), MaskAITD2.RenderMasksTimeGate);
				}
				else
				{
					foreach (var entry in pak)
					{
						if ((fileName.StartsWith("CAMERA") || fileName == "ITD_RESS") && Background.IsBackground(entry) && !raw)
						{
							var data = entry.Read();
							Background.GetBackground(data);
							var destPath = Path.Combine(fileName, $"{entry.Index:D8}.png");
							WriteFile(destPath, Background.SaveBitmap());
						}
						else
						{
							var destPath = Path.Combine(fileName, $"{entry.Index:D8}.dat");
							WriteFile(destPath, entry.Read());
						}
					}
				}
			}
		}

		static void WriteFile(string filePath, byte[] data)
		{
			if (!File.Exists(filePath) || data.Length != new FileInfo(filePath).Length || !Enumerable.SequenceEqual(File.ReadAllBytes(filePath), data))
			{
				Console.WriteLine($"{filePath} {data.Length}");
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				File.WriteAllBytes(filePath, data);
			}
		}

		static void RenderMasks(PakArchive pak, string folder, int? camID, Func<PakArchive, uint[], IEnumerable<int>> renderMasks)
		{
			foreach (var cameraID in renderMasks(pak, Background.Bitmap.Bits))
			{
				var destPath = Path.Combine(folder, $"{camID ?? cameraID:D8}.png");
				WriteFile(destPath, Background.SaveBitmap());
			}
		}

		static void ExportSVG(PakArchive pak, string fileName, int[] rooms, int rotate, GameVersion version)
		{
			var data = Svg.Export(pak, rooms, rotate, version);
			WriteFile(Path.Combine("SVG", Path.GetFileNameWithoutExtension(fileName) + ".svg"), data);
		}
	}
}
