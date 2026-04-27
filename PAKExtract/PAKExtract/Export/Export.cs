using Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace PAKExtract
{
	public static class Export
	{
		public static void ExportBackground()
		{
			bool paletteNotFoundMessage = false;
			foreach (var directory in Directory.GetDirectories("."))
			{
				if (Path.GetFileName(directory).StartsWith("CAMERA", StringComparison.InvariantCultureIgnoreCase)
					|| Path.GetFileName(directory).Equals("ITD_RESS", StringComparison.InvariantCultureIgnoreCase))
				{
					ExportBackgrounds(ref paletteNotFoundMessage, directory);
				}

				if (Path.GetFileName(directory).StartsWith("TEXTURES", StringComparison.InvariantCultureIgnoreCase))
				{
					Textures.Export(ref paletteNotFoundMessage);
				}
			}
		}

		public static void ExportBackgrounds(ref bool paletteNotFoundMessage, string directory)
		{
			foreach (var filePath in Directory.EnumerateFiles(directory, @"*.*", SearchOption.TopDirectoryOnly))
			{
				var length = new FileInfo(filePath).Length;
				if (Background.IsBackground(length))
				{
					if (Background.IsAITD1Background(length) && !Directory.Exists("ITD_RESS"))
					{
						if (!paletteNotFoundMessage)
						{
							paletteNotFoundMessage = true;
							Console.Error.WriteLine("Cannot find folder ITD_RESS. Please extract it first.");
						}
					}
					else
					{
						var data = File.ReadAllBytes(filePath);
						Background.GetBackground(data);
						var destPath = Path.Combine("BACKGROUND", Path.GetFileName(Path.GetDirectoryName(filePath)), $"{Path.GetFileNameWithoutExtension(filePath)}.png");
						Program.WriteFile(destPath, Background.SaveBitmap());
					}
				}
			}
		}

		public static void ExportSvg(int[] rooms, int rotate, int zoom, bool trigger, bool camera, bool caption)
		{
			foreach (var directory in Directory.GetDirectories("."))
			{
				if (Path.GetFileName(directory).StartsWith("ETAGE", StringComparison.InvariantCultureIgnoreCase))
				{
					var data = Svg.Export(directory, [.. rooms], rotate, zoom, trigger, camera, caption);
					Program.WriteFile(Path.Combine("SVG", Path.GetFileNameWithoutExtension(directory) + ".svg"), data);
				}
			}
		}

		public static void ExportMasks(GameVersion version)
		{
			var mask = new bool[64000];
			var backgroundErrorMessage = new HashSet<int>();

			switch (version)
			{
				case GameVersion.AITD1:
				case GameVersion.AITD1_FLOPPY:
				case GameVersion.AITD1_DEMO:
					foreach (var directory in Directory.GetDirectories("."))
					{
						if (Path.GetFileName(directory).StartsWith("ETAGE", StringComparison.InvariantCultureIgnoreCase))
						{
							var reg = Regex.Match(Path.GetFileName(directory), @"ETAGE(\d{2})");
							var index = int.Parse(reg.Groups[1].Value);

							foreach (var filePath in Directory.GetFiles(directory, "00000001.*"))
							{
								foreach (var cameraID in MaskAITD1.GetMasks(filePath, mask))
								{
									var destPath = Path.Combine($"MASK{index:D2}", $"{cameraID:D8}.png");
									SaveImage(index, cameraID, destPath);
								}
							}
						}
					}
					break;

				case GameVersion.JACK:
				case GameVersion.AITD2:
				case GameVersion.AITD2_DEMO:
				case GameVersion.AITD3:
					foreach (var directory in Directory.GetDirectories("."))
					{
						if (Path.GetFileName(directory).StartsWith("MASK") || Path.GetFileName(directory).StartsWith("NASK"))
						{
							var reg = Regex.Match(Path.GetFileName(directory), @"(MASK|NASK)(\d{2})");
							string directoryName = reg.Groups[1].Value;
							var index = int.Parse(reg.Groups[2].Value);

							foreach (var filePath in Directory.GetFiles(directory))
							{
								int cameraID = int.Parse(Path.GetFileNameWithoutExtension(filePath));
								MaskAITD2.RenderMask(filePath, mask);

								var destPath = Path.Combine($"{directoryName}{index:D2}", $"{cameraID:D8}.png");
								SaveImage(index, cameraID, destPath);
							}
						}
					}
					break;

				case GameVersion.TIMEGATE:
				case GameVersion.TIMEGATE_DEMO:
					foreach (var directory in Directory.GetDirectories("."))
					{
						if (Path.GetFileName(directory).StartsWith("MK") || Path.GetFileName(directory).StartsWith("NK"))
						{
							var reg = Regex.Match(Path.GetFileName(directory), @"(MK|NK)(\d{2})(\d{2})");
							string directoryName = reg.Groups[1].Value;
							int index = int.Parse(reg.Groups[2].Value);
							int cameraID = int.Parse(reg.Groups[3].Value);

							MaskAITD2.RenderMasksTimeGate(directory, mask);
							var destPath = Path.Combine($"{directoryName}{index:D2}", $"{cameraID:D8}.png");
							SaveImage(index, cameraID, destPath);
						}
					}
					break;
			}

			void SaveImage(int cameraFolderId, int cameraId, string destPath)
			{
				string backgroundFile = Path.Combine("BACKGROUND", $"CAMERA{cameraFolderId:D2}", $"{cameraId:D8}.png");
				if (File.Exists(backgroundFile))
				{
					var image = Image.Load(backgroundFile) as Image<Rgba32>;
					if (image != null && MaskAITD1.RenderMask(mask, image))
					{
						Program.WriteFile(Path.Combine("BACKGROUND", destPath), Background.SaveBitmap(image));
					}
				}
				else if (backgroundErrorMessage.Add(cameraFolderId))
				{
					Console.Error.WriteLine($"Cannot find BACKGROUND for CAMERA{cameraFolderId:D2}. Please extract it first.");
				}
			}
		}
	}
}
