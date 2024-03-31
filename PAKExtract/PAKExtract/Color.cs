using System.Runtime.InteropServices;

namespace PAKExtract
{
	[StructLayout(LayoutKind.Explicit)]
	struct Color
	{
		[FieldOffset(0)]
		public uint ARGB;

		[FieldOffset(0)]
		public byte B;
		[FieldOffset(1)]
		public byte G;
		[FieldOffset(2)]
		public byte R;
		[FieldOffset(3)]
		public byte A;

		public Color(byte a, byte r, byte g, byte b): this()
		{
			A = a;
			R = r;
			G = g;
			B = b;
		}

		public Color(uint argb) : this()
		{
			ARGB = argb;
		}
	}
}
