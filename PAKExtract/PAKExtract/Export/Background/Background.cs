using System.Drawing.Imaging;
using System.IO;

namespace PAKExtract
{
	public class Background
	{
		public static readonly DirectBitmap Bitmap = new(320, 200);

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
			var dest = Bitmap.Bits;

			switch (data.Length)
			{
				case 64000: //AITD1
					{
						var pal = Palette.LoadITDPalette();
						for (int i = 0; i < 64000; i++)
						{
							dest[i] = pal[data[i]];
						}
						break;
					}

				case 64768: //AITD2, AITD3, TIME GATE
					{
						var pal = Palette.LoadPalette(data, 64000);
						for (int i = 0; i < 64000; i++)
						{
							dest[i] = pal[data[i]];
						}
						break;
					}

				case 64770: //ITD_RESS
					{
						var pal = Palette.LoadPalette(data, 2);
						for (int i = 0; i < 64000; i++)
						{
							dest[i] = pal[data[i + 770]];
						}
						break;
					}
			}
		}

		public static byte[] SaveBitmap()
		{
			using var stream = new MemoryStream();
			Bitmap.Bitmap.Save(stream, ImageFormat.Png);
			return stream.ToArray();
		}
	}
}
