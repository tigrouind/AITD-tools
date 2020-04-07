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
		}
		
		[StructLayout(LayoutKind.Explicit)]
		public struct CharInfo
		{
			[FieldOffset(0)] public CharUnion Char;
			[FieldOffset(2)] public short Attributes;
		}
		
		[StructLayout(LayoutKind.Sequential)]
		public struct SmallRect
		{
			public short Left;
			public short Top;
			public short Right;
			public short Bottom;
		}
		
		static readonly SafeFileHandle handle;
		static readonly CharInfo[] buf = new CharInfo[120 * 80];
	    static SmallRect rect = new SmallRect { Left = 0, Top = 0, Right = 120, Bottom = 80 };
		
		static Console() 
		{
			handle = CreateFile("CONOUT$", 0x40000000, 2, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
		}
	    
	    public static void Clear()
		{
			for(int i = 0 ; i < buf.Length ; i++)
			{
				buf[i] = new CharInfo { Char = new CharUnion { UnicodeChar = (char)0 }, Attributes = 0 };
			}
		}
		
		public static void Write(int x, int y, ConsoleColor color, string format, params object[] value)
		{
			foreach (char ch in string.Format(format, value))
			{		
				if (x < 120 && y < 80)
				{
					buf[y * 120 + x] = new CharInfo { Char = new CharUnion { UnicodeChar = (char)ch }, Attributes = (short)color };
				}
				
				x++;
			}
		}
		public static void Flush()
		{		
	        WriteConsoleOutput(handle, buf,
	          new Coord { X = 120, Y = 80 },
	          new Coord { X = 0, Y = 0 },
	          ref rect);
		}
	}
}
