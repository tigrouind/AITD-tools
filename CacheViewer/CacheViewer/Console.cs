using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CacheViewer
{
	public static class Console
	{
		[DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		static extern SafeFileHandle CreateFile(
		    string fileName,
		    [MarshalAs(UnmanagedType.U4)] uint fileAccess,
		    [MarshalAs(UnmanagedType.U4)] uint fileShare,
		    IntPtr securityAttributes,
		    [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
		    [MarshalAs(UnmanagedType.U4)] int flags,
		    IntPtr template);
		
		[DllImport("kernel32.dll", SetLastError = true)]
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
		
			public Coord(short X, short Y)
			{
				this.X = X;
				this.Y = Y;
			}
		};
		
		[StructLayout(LayoutKind.Explicit)]
		public struct CharUnion
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
	    
	    public static void Write(int x, int y, ConsoleColor color, string text)
	    {
	    	foreach (char ch in text)
			{		
				if (x < SIZEX && y < SIZEY)
				{
					buf[y * SIZEX + x] = new CharInfo { Char = new CharUnion { UnicodeChar = (char)ch }, Attributes = (short)color };
				}
				
				x++;
			}
	    }
		
		public static void Write(int x, int y, ConsoleColor color, string format, params object[] value)
		{
			Write(x, y, color, string.Format(format, value));
		}
		
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
