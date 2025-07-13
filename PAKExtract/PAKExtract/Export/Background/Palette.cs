using Shared;
using System.IO;
using System.Linq;

namespace PAKExtract
{
	static class Palette
	{
		static bool paletteLoaded = false;
		static readonly uint[] paletteCustom = new uint[256];
		static readonly uint[] paletteITD = new uint[256];

		public static uint[] LoadITDPalette()
		{
			if (!paletteLoaded)
			{
				paletteLoaded = true;
				foreach (var filePath in Directory.EnumerateFiles("ITD_RESS").Reverse())
				{
					if (new FileInfo(filePath).Length == 768)
					{
						LoadPalette(File.ReadAllBytes(filePath), 0, paletteITD);
						break;
					}
				}
			}

			return paletteITD;
		}

		public static uint[] LoadPalette(byte[] data, int offset)
		{
			LoadPalette(data, offset, paletteCustom);
			return paletteCustom;
		}

		static void LoadPalette(byte[] data, int offset, uint[] pal)
		{
			bool vgaMap = Enumerable.Range(0, 768)
				.All(x => data[x + offset] <= 63);

			for (int i = 0; i < 256; i++)
			{
				int r = data[offset++];
				int g = data[offset++];
				int b = data[offset++];

				if (vgaMap) //AITD2, AITD3
				{
					r = r << 2 | r >> 4;
					g = g << 2 | g >> 4;
					b = b << 2 | b >> 4;
				}

				unchecked
				{
					pal[i] = (uint)((255 << 24) | (b << 16) | (g << 8) | r);
				}
			}
		}
	}
}
