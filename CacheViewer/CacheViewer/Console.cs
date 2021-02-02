using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CacheViewer
{
	public static class Console
	{
		[DllImport("Kernel32.dll")]
		static extern SafeFileHandle CreateFile(
			string fileName,
			[MarshalAs(UnmanagedType.U4)] uint fileAccess,
			[MarshalAs(UnmanagedType.U4)] uint fileShare,
			IntPtr securityAttributes,
			[MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
			[MarshalAs(UnmanagedType.U4)] int flags,
			IntPtr template);

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

		static readonly short SIZEX = 120;
		static readonly short SIZEY = 80;
		static readonly SafeFileHandle handle;
		static CharInfo[] buf = new CharInfo[SIZEX * SIZEY];
		static CharInfo[] previousBuf = new CharInfo[SIZEX * SIZEY];

		static Console()
		{
			handle = CreateFile("CONOUT$", 0x40000000, 2, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
		}

		public static void Clear()
		{
			Array.Clear(buf, 0, buf.Length);
		}

		static void Write(int x, int y, ConsoleColor color, char value)
		{
			if (x < SIZEX && y < SIZEY)
			{
				buf[y * SIZEX + x] = new CharInfo { Char = new CharUnion { UnicodeChar = value }, Attributes = (short)color };
			}
		}
		
		public static void Write(int x, int y, ConsoleColor color, string value)
		{
			for (int i = 0 ; i < value.Length ; i++)
			{
				Write(x++, y, color, value[i]);
			}
		}
		
		#region String format 
						
		public static void Write(int x, int y, ConsoleColor color, string format, int arg0, int arg1 = 0, int arg2 = 0, int arg3 = 0)
		{
			//GC friendly (unlike string.Format())
			int pos = 0;
			while (pos < format.Length)
			{
				char ch = format[pos++];
				if (ch == '{')
				{					
					ch = format[pos++];
					if (ch < '0' || ch > '9') throw new FormatException();
					
					int value;
					int arg = ch - '0';
					switch (arg)
					{
						case 0:
							value = arg0;
							break;							
						case 1:
							value = arg1;
							break;						
						case 2:
							value = arg2;
							break;							
						case 3:
							value = arg3;
							break;							
						default:
							throw new FormatException();
					}
					
					ch = format[pos++]; // ','
					if (ch != ',') throw new FormatException();
					
					ch = format[pos++]; // width
					if (ch < '0' || ch > '9') throw new FormatException();
					int width = ch - '0';					
															
					ch = format[pos++]; // ':'
					if (ch != ':') throw new FormatException();
					
					int length;
					ch = format[pos++]; 				
					switch (ch) 
					{
						case 'D': //decimal
							Write(x + width - 1, y, color, value, out length);													
							Pad(x, y, color, width - length);
							break;
							
						case 'S': //size
							string suffix = " B";
							if (value >= 1024)
							{
								value /= 1024;
								suffix = " K";
							}
							
							Write(x + width - 2, y, color, suffix);
							Write(x + width - 3, y, color, value, out length);							
							Pad(x, y, color, width - 2 - length);
							break;
							
						default: 
							throw new FormatException();
					}
					x += width;
					
					ch = format[pos++]; // '}'
					if (ch != '}') throw new FormatException();
				}
				else
				{
					Write(x, y, color, ch);
					x++;	
				}
			}
		}
		
		static void Pad(int x, int y, ConsoleColor color, int width)
		{
			for(int i = 0 ; i < width ; i++)
			{
				Write(x++, y, color, ' ');
			}
		}
		
		static int Write(int x, int y, ConsoleColor color, int value, out int length)
		{
			length = 0;
			bool negative = false;
			if (value < 0)
			{
				value = -value;
				negative = true;
			}
			
			do
			{	
				var reminder = value % 10;
				Write(x--, y, color, (char)(reminder + '0'));
				value /= 10;
				length++;
			}
			while(value > 0);
			
			if(negative) 
			{
				Write(x--, y, color, '-');
				length++;
			}
			
			return length;
		}		
		
		#endregion

		static bool CompareBuffers(out SmallRect rect)
		{
			bool refresh = false;
			rect = new SmallRect { Left = short.MaxValue, Top = short.MaxValue, Right = short.MinValue, Bottom = short.MinValue };

			for (short y = 0 ; y < SIZEY ; y++)
			{
				for (short x = 0 ; x < SIZEX ; x++)
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

		public static void Flush()
		{
			SmallRect rect;
			if (CompareBuffers(out rect))
			{
				WriteConsoleOutput(handle, buf,
					new Coord { X = SIZEX, Y = SIZEY },
					new Coord { X = rect.Left, Y = rect.Top },
					ref rect);
			}

			//swap
			var tmp = buf;
			buf = previousBuf;
			previousBuf = tmp;
		}
	}
}
