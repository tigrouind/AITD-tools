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
			for (int j = 0; j < y.Length; j++)
			{
				if (x[j + index] != y[j])
				{
					return false;
				}
			}
			
			return true;
		}
	}
}
