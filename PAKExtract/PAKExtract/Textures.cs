using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;

namespace PAKExtract
{
	public class Textures
	{
		public static void Export()
		{
			bool paletteNotFoundMessage = false;
			var pal = Palette.LoadITDPalette();
			foreach (var filePath in Directory.EnumerateFiles("TEXTURES", @"*.*", SearchOption.TopDirectoryOnly))
			{
				if (!Directory.Exists("ITD_RESS") && !paletteNotFoundMessage)
				{
					paletteNotFoundMessage = true;
					Console.Error.WriteLine("Cannot find folder ITD_RESS. Please extract it first.");
				}

				var data = File.ReadAllBytes(filePath);
				if (data.Length == 256) //single row image, skip it
				{
					continue;
				}

				var bitmap = new Image<Rgba32>(256, data.Length / 256);
				for (int i = 0; i < data.Length; i++)
				{
					bitmap[i % 256, i / 256] = new Rgba32(pal[data[i]]);
				}

				var destPath = Path.Combine("BACKGROUND", Path.GetFileName(Path.GetDirectoryName(filePath)), $"{Path.GetFileNameWithoutExtension(filePath)}.png");
				Program.WriteFile(destPath, Background.SaveBitmap(bitmap));
			}
		}
	}
}
