using System;

namespace Shared
{
	public static class Tools
	{
		public static ushort ReadUnsignedShort(this byte[] data, int offset)
		{
			unchecked
			{
				return (ushort)(data[offset] | data[offset + 1] << 8);
			}
		}

		public static short ReadShort(this byte[] data, int offset)
		{
			unchecked
			{
				return (short)(data[offset] | data[offset + 1] << 8);
			}
		}

		public static uint ReadUnsignedInt(this byte[] data, int offset)
		{
			unchecked
			{
				return (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
			}
		}
		
		public static int ReadFarPointer(this byte[] data, int offset)
		{
			unchecked
			{
				return (data[offset] | data[offset + 1] << 8) + (data[offset + 2] << 4 | data[offset + 3] << 12);
			}
		}

		public static void Write(this byte[] data, short value, int offset)
		{
			unchecked
			{
				data[offset + 0] = (byte)(value & 0xFF);
				data[offset + 1] = (byte)(value >> 8);
			}
		}

		public static bool IsMatch(this byte[] x, byte[] y, int index)
		{
			for (int i = 0; i < y.Length; i++)
			{
				if (x[i + index] != y[i])
				{
					return false;
				}
			}

			return true;
		}
	}
}
