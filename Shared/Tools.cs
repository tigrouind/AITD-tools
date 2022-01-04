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
				return ReadUnsignedShort(data, offset) + ReadUnsignedShort(data, offset + 2) * 16;
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
		
		public static void Write(this byte[] data, ushort value, int offset)
		{
			unchecked
			{
				data[offset + 0] = (byte)(value & 0xFF);
				data[offset + 1] = (byte)(value >> 8);
			}
		}

		public static int IndexOf(byte[] buffer, byte[] pattern)
		{
			for (int index = 0; index < buffer.Length - pattern.Length + 1; index++)
			{
				if (buffer.IsMatch(pattern, index))
				{
					return index;
				}
			}
	
			return -1;
		}

		static bool IsMatch(this byte[] buffer, byte[] pattern, int index)
		{
			for (int i = 0; i < pattern.Length; i++)
			{
				if (buffer[i + index] != pattern[i])
				{
					return false;
				}
			}
	
			return true;
		}	
		
		public static TimeSpan GetTimeSpan(int start, int end)
		{
			return TimeSpan.FromMilliseconds(start - end);
		}
		
		public static int? GetArgument(string[] args, string name)
		{
			int index = Array.IndexOf(args, name);
			if (index >= 0 && index < (args.Length - 1))
			{
				int value;
				if (int.TryParse(args[index + 1], out value))
				{
					return value;
				}
			}

			return null;
		}
	}
}
