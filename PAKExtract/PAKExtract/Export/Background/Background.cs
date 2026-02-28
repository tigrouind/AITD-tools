using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;

namespace PAKExtract
{
	public class Background
	{
		public static readonly Image<Rgba32> Bitmap = new(320, 200);

		public static bool IsBackground(long size)
		{
			return size switch
			{
				64000 or 64768 or 64770 => true,
				_ => false,
			};
		}

		public static bool IsAITD1Background(long size)
		{
			return size == 64000;
		}

		public static void GetBackground(byte[] data)
		{
			switch (data.Length)
			{
				case 64000: //AITD1
					{
						var pal = Palette.LoadITDPalette();
						for (int i = 0; i < 64000; i++)
						{
							Bitmap[i % 320, i / 320] = new Rgba32(pal[data[i]]);
						}
						break;
					}

				case 64768: //AITD2, AITD3, TIME GATE
					{
						var pal = Palette.LoadPalette(data, 64000);
						for (int i = 0; i < 64000; i++)
						{
							Bitmap[i % 320, i / 320] = new Rgba32(pal[data[i]]);
						}
						break;
					}

				case 64770: //ITD_RESS
					{
						var pal = Palette.LoadPalette(data, 2);
						for (int i = 0; i < 64000; i++)
						{
							Bitmap[i % 320, i / 320] = new Rgba32(pal[data[i + 770]]);
						}
						break;
					}
			}
		}

		public static byte[] SaveBitmap()
		{
			return SaveBitmap(Bitmap);
		}

		public static byte[] SaveBitmap(Image<Rgba32> bitmap)
		{
			using var stream = new MemoryStream();
			bitmap.Save(stream, new PngEncoder()); // Save as PNG
			return stream.ToArray();
		}
	}
}
