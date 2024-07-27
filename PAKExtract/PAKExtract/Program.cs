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

		static GameVersion? version;
		static bool mask, background, svg;
		static int[] svgrooms;
		static int svgrotate;

		static int Main(string[] args)
		{
			background = Tools.HasArgument(args, "-background");
			mask = Tools.HasArgument(args, "-mask");
			svg = Tools.HasArgument(args, "-svg");
			svgrooms = (Tools.GetArgument<string>(args, "-svgrooms") ?? string.Empty)
				.Split(',')
				.Where(x => x != string.Empty)
				.Select(x => int.Parse(x)).ToArray();
			svgrotate = Tools.GetArgument<int>(args, "-svgrotate");
			version = Tools.GetArgument<GameVersion?>(args, "-version");

			if ((mask || svg) && !version.HasValue)
			{
				Console.Error.Write("Version must be specified");
				return -1;
			}

			bool foundFile = false;
			for (int i = 0; i < args.Length; i++)
			{
				var arg = args[i];
				if (!arg.StartsWith("-") && (i == 0 || !args[i - 1].StartsWith("-")))
				{
					if (File.Exists(arg))
					{
						ExtractFile(arg);
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

		static void ExtractFile(string filePath)
		{
			string fileName = Path.GetFileNameWithoutExtension(filePath);

			using (var pak = new PakArchive(filePath))
			{
				if (svg && fileName.StartsWith("ETAGE"))
				{
					ExportSVG(pak, fileName, svgrooms, svgrotate, version.Value);
				}

				if (mask)
				{
					if (fileName.StartsWith("ETAGE") && (version == GameVersion.AITD1 || version == GameVersion.AITD1_FLOPPY || version == GameVersion.AITD1_DEMO))
					{
						RenderMasks(pak, fileName, null, MaskAITD1.RenderMasks);
					}

					if (fileName.StartsWith("MASK") || fileName.StartsWith("NASK"))
					{
						RenderMasks(pak, fileName, null, MaskAITD2.RenderMasks);
					}

					if (fileName.StartsWith("MK") || fileName.StartsWith("NK"))
					{
						RenderMasks(pak, fileName.Substring(0, 4), int.Parse(fileName.Substring(4, 2)), MaskAITD2.RenderMasksTimeGate);
					}
				}

				if (background && (fileName.StartsWith("CAMERA") || fileName == "ITD_RESS"))
				{
					foreach (var entry in pak.Where(Background.IsBackground))
					{
						var data = entry.Read();
						Background.GetBackground(data);
						var destPath = Path.Combine(fileName, $"{entry.Index:D8}.png");
						WriteFile(destPath, Background.SaveBitmap());
					}
				}

				if (!background && !mask && !svg)
				{
					foreach (var entry in pak)
					{
						var destPath = Path.Combine(fileName, $"{entry.Index:D8}.dat");
						WriteFile(destPath, entry.Read());
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
