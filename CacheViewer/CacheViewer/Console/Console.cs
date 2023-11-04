using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CacheViewer
{
	public static class Console
	{
		#region Native

		[DllImport("Kernel32.dll")]
		static extern SafeFileHandle CreateFile(
			string fileName,
			[MarshalAs(UnmanagedType.U4)] FileAccess fileAccess,
			[MarshalAs(UnmanagedType.U4)] FileShare fileShare,
			IntPtr securityAttributes,
			[MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
			[MarshalAs(UnmanagedType.U4)] FileAttributes flags,
			IntPtr template);

		const uint GENERIC_WRITE = 0x40000000;

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		static extern bool WriteConsoleOutput(
		SafeFileHandle hConsoleOutput,
		CharInfo[] lpBuffer,
		Coord dwBufferSize,
		Coord dwBufferCoord,
		ref SmallRect lpWriteRegion);

		[StructLayout(LayoutKind.Sequential)]
		public struct Coord
		{
			public short X;
			public short Y;
		};

		[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
		public struct CharUnion : IEquatable<CharUnion>
		{
			[FieldOffset(0)] public char UnicodeChar;
			[FieldOffset(0)] public byte AsciiChar;

			public bool Equals(CharUnion obj)
			{
				return UnicodeChar == obj.UnicodeChar && AsciiChar == obj.AsciiChar;
			}
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct CharInfo : IEquatable<CharInfo>
		{
			[FieldOffset(0)] public CharUnion Char;
			[FieldOffset(2)] public short Attributes;

			public bool Equals(CharInfo obj)
			{
				return Attributes == obj.Attributes && Char.Equals(obj.Char);
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct SmallRect
		{
			public short Left;
			public short Top;
			public short Right;
			public short Bottom;
		}

		#endregion

		const short SIZEX = 256;
		const short SIZEY = 256;
		static readonly SafeFileHandle handle;
		static CharInfo[] buf = new CharInfo[SIZEX * SIZEY];
		static CharInfo[] previousBuf = new CharInfo[SIZEX * SIZEY];
		static int maxSizeX;
		static int maxSizeY;

		public static ConsoleColor BackgroundColor;
		public static ConsoleColor ForegroundColor = ConsoleColor.Gray;
		public static int CursorLeft;
		public static int CursorTop;

		static Console()
		{
			handle = CreateFile("CONOUT$", (FileAccess)GENERIC_WRITE, FileShare.Write, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
			Clear(SIZEX, SIZEY);
		}

		public static void Clear()
		{
			Clear(maxSizeX, maxSizeY);
		}

		static void Clear(int sizeX, int sizeY)
		{
			for (int y = 0 ; y < sizeY; y++)
			{
				for (int x = 0; x < sizeX; x++)
				{
					buf[x + y * SIZEX] = new CharInfo { Char = new CharUnion { UnicodeChar = ' ' }, Attributes = 0 };
				}
			}
		}

		public static void Write(char value)
		{
			if (CursorLeft < SIZEX && CursorTop < SIZEY)
			{
				short color = (short)((int)ForegroundColor | (int)BackgroundColor << 4);
				buf[CursorTop * SIZEX + CursorLeft] = new CharInfo { Char = new CharUnion { UnicodeChar = value }, Attributes = color };

				if (CursorLeft >= maxSizeX) maxSizeX = CursorLeft + 1;
				if (CursorTop >= maxSizeY) maxSizeY = CursorTop + 1;
				CursorLeft++;
			}
		}

		public static void Write(string value)
		{
			for (int i = 0 ; i < value.Length ; i++)
			{
				Write(value[i]);
			}
		}

		public static void Write(string format,
								FormatArgument arg0,
								FormatArgument arg1 = default,
								FormatArgument arg2 = default,
								FormatArgument arg3 = default,
								FormatArgument arg4 = default,
								FormatArgument arg5 = default,
								FormatArgument arg6 = default)
		{
			StringFormat.Format(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6);
			for (int i = 0 ; i < StringFormat.Buffer.Length ; i++)
			{
				Write(StringFormat.Buffer[i]);  //GC friendly equivalent of Write(string.Format(format, args));
			}
		}

		public static void SetCursorPosition(int left, int top)
		{
			CursorLeft = left;
			CursorTop = top;
		}

		public static void Flush()
		{
			if (CompareBuffers(out SmallRect rect))
			{
				WriteConsoleOutput(handle, buf,
					new Coord { X = SIZEX, Y = SIZEY },
					new Coord { X = rect.Left, Y = rect.Top },
					ref rect);
			}

			//swap
			(previousBuf, buf) = (buf, previousBuf);
		}

		static bool CompareBuffers(out SmallRect rect)
		{
			bool refresh = false;
			rect = new SmallRect { Left = short.MaxValue, Top = short.MaxValue, Right = short.MinValue, Bottom = short.MinValue };

			for (short y = 0; y < maxSizeY; y++)
			{
				for (short x = 0; x < maxSizeX; x++)
				{
					int i = x + y * SIZEX;
					if (!buf[i].Equals(previousBuf[i]))
					{
						refresh = true;
						rect.Left = Math.Min(rect.Left, x);
						rect.Top = Math.Min(rect.Top, y);
						rect.Right = Math.Max(rect.Right, x);
						rect.Bottom = Math.Max(rect.Bottom, y);
					}
				}
			}

			return refresh;
		}
	}
}
