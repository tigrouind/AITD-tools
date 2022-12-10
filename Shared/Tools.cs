using System;
using System.Linq;

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
		
		public static ushort ReadUnsignedShortSwap(this byte[] data, int offset)
		{
			unchecked
			{
				return (ushort)(data[offset] << 8 | data[offset + 1]);
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

		public static int IndexOf(byte[] buffer, byte[] pattern, int offset = 0, int stride = 1)
		{
			for (int index = offset; index < buffer.Length - pattern.Length + 1; index += stride)
			{
				if (buffer[index] == pattern[0] && buffer.IsMatch(pattern, index))
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
		
		public static T GetArgument<T>(string[] args, string name)
		{
			int index = Array.FindIndex(args, x => x.Equals(name, StringComparison.InvariantCultureIgnoreCase));
			if (index >= 0 && index < (args.Length - 1))
			{
				Type type = typeof(T);
				string argument = args[index + 1];					
					
				if (type == typeof(int) || type == typeof(int?))
				{
					int value;
					if (int.TryParse(argument, out value))
					{
						return (T)(object)value;
					}
				}
				else if (type == typeof(string))
				{
					return (T)(object)argument;
				}
				else 
				{
					throw new NotSupportedException(type.ToString());
				}
			}

			return default(T);
		}
		
		public static bool HasArgument(string[] args, string name)
		{
			return args.Contains(name, StringComparer.InvariantCultureIgnoreCase);
		}
	}
}
