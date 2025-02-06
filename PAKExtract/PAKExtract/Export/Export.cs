using Shared;
using System;
using System.IO;
using System.Linq;

namespace PAKExtract
{
	public static class Export
	{
		public static void ExportBackground()
		{
			bool paletteNotFoundMessage = false;
			foreach (var directory in Directory.GetDirectories("."))
			{
				if (Path.GetFileName(directory).StartsWith("CAMERA") || Path.GetFileName(directory) == "ITD_RESS")
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

		public static void ExportSvg(SvgInfo svg)
		{
			foreach (var directory in Directory.GetDirectories("."))
			{
				if (Path.GetFileName(directory).StartsWith("ETAGE"))
				{
					var data = Svg.Export(directory, svg.Room.ToHashSet(), svg.Rotate, svg.Trigger);
					Program.WriteFile(Path.Combine("SVG", Path.GetFileNameWithoutExtension(directory) + ".svg"), data);
				}
			}
		}
	}
}
