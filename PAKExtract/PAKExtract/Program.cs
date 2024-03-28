using Shared;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace PAKExtract
{
	class Program
	{
		static readonly DirectBitmap bitmap = new DirectBitmap(320, 200);
		static readonly uint[] palette = new uint[256];
		static bool paletteLoaded = false;

		static int Main()
		{
			foreach (var filePath in Directory.GetFiles(".", "CAMERA*.PAK", SearchOption.TopDirectoryOnly))
			{
				using (var pak = new UnPAK(filePath))
				{
					for (int n = 0; n < pak.EntryCount; n++)
					{
						var data = pak.GetEntry(n);
						string bmpFilePath = string.Format("{0}{1:D3}.png", Path.GetFileNameWithoutExtension(filePath), n);

						try
						{
							ExportBackground(data, bmpFilePath);
						}
						catch (FileNotFoundException)
						{
							Console.WriteLine("ITD_RESS.PAK not found");
							return -1;
						}
					}
				}
			}

			return 0;
		}

		static void ExportBackground(byte[] data, string filePath)
		{
			if (data.Length == 64000) //AITD1
			{
				LoadAITD1Palette(palette);
				for (int i = 0; i < 64000; i++)
				{
					bitmap.Bits[i] = palette[data[i]];
				}
			}
			else if (data.Length == 64768) //AITD2, AITD3, TIME GATE
			{
				LoadPalette(data, 64000, palette);
				for (int i = 0; i < 64000; i++)
				{
					bitmap.Bits[i] = palette[data[i]];
				}
			}

			Console.WriteLine(filePath);
			bitmap.Bitmap.Save(filePath, ImageFormat.Png);
		}

		static void LoadAITD1Palette(uint[] palette)
		{
			if (!paletteLoaded)
			{
				paletteLoaded = true;
				using (var pak = new UnPAK("ITD_RESS.PAK"))
				{
					for (int i = pak.EntryCount - 1; i >= 0; i--)
					{
						var data = pak.GetEntry(i);
						if (data.Length == 768)
						{
							LoadPalette(data, 0, palette);
							break;
						}
					}
				}
			}
		}

		static void LoadPalette(byte[] data, int offset, uint[] palette)
		{
			bool mapping = Enumerable.Range(0, 768)
				.All(x => data[x + offset] <= 63);

			for (int i = 0; i < 256; i++)
			{
				byte r = data[i * 3 + 0 + offset];
				byte g = data[i * 3 + 1 + offset];
				byte b = data[i * 3 + 2 + offset];

				if (mapping)
				{
					unchecked //AITD2, AITD3
					{
						palette[i] = (uint)((255 << 24) | ((r << 2 | r >> 4) << 16) | ((g << 2 | r >> 4) << 8) | (b << 2 | b >> 4));
					}
				}
				else
				{
					unchecked //AITD1, TIME GATE
					{
						palette[i] = (uint)((255 << 24) | (r << 16) | (g << 8) | b);
					}
				}

			}
		}
	}
}
