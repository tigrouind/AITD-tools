using System;
using System.IO;

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
	}
}
