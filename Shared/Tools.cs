using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Shared
{
	public static class Tools
	{
		#region Read

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

		public static int ReadInt(this byte[] data, int offset)
		{
			unchecked
			{
				return data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24;
			}
		}

		public static int ReadFarPointer(this byte[] data, int offset)
		{
			unchecked
			{
				return ReadUnsignedShort(data, offset) + ReadUnsignedShort(data, offset + 2) * 16;
			}
		}

		public static (int X, int Y, int Z) ReadVector(this byte[] data, int offset)
		{
			unchecked
			{
				return (
					ReadShort(data, offset + 0),
					ReadShort(data, offset + 2),
					ReadShort(data, offset + 4)
				);
			}
		}

		public static ((int X, int Y, int Z) lower, (int X, int Y, int Z) upper) ReadBoundingBox(this byte[] data, int offset)
		{
			return ((
				data.ReadShort(offset + 0),
				data.ReadShort(offset + 4),
				data.ReadShort(offset + 8)
			),(
				data.ReadShort(offset + 2),
				data.ReadShort(offset + 6),
				data.ReadShort(offset + 10)
			));
		}

		#endregion

		#region Write

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

		#endregion

		#region Pattern

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

		#endregion

		public static TimeSpan GetTimeSpan(long start, long end)
		{
			return TimeSpan.FromTicks(start - end);
		}

		public static IEnumerable<string> ReadLines(byte[] buffer, Encoding encoding)
		{
			using var stream = new MemoryStream(buffer);
			using var reader = new StreamReader(stream, encoding);
			string line;
			while ((line = reader.ReadLine()) != null)
			{
				yield return line;
			}
		}
	}
}
