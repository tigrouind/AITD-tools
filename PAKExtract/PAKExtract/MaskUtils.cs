namespace PAKExtract
{

	public static class MaskUtils
	{
		static readonly uint[] CameraColors = { 0xFFFF8080, 0xFF789CF0, 0xFFB0DE6F, 0xFFCC66C0, 0xFF5DBAAB, 0xFFF2BA79, 0xFF8E71E3, 0xFF6ED169, 0xFFBF6080, 0xFF7CCAF7 };

		public static void ClearBitmap(uint[] dest)
		{
			for (int i = 0; i < 64000; i++)
			{
				dest[i] = 0xFF000000;
			}
		}

		public static void FillBitmap(bool[] mask, uint[] dest, int colorIndex)
		{
			var color = CameraColors[colorIndex % CameraColors.Length];

			for (int i = 0; i < 64000; i++)
			{
				if (mask[i])
				{
					if (dest[i] == 0xFF000000)
					{
						dest[i] = color;
					}
					else
					{
						dest[i] = Mix(color, dest[i]);
					}
				}
			}
		}

		public static uint Mix(uint src, uint dst)
		{
			Color a = new Color(src);
			Color b = new Color(dst);

			return new Color(255,
				(byte)((a.R + b.R) / 2),
				(byte)((a.G + b.G) / 2),
				(byte)((a.B + b.B) / 2)).ARGB;
		}
	}
}
