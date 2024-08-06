using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VarsViewer
{
	public static class Console
	{
		#region Native

		[DllImport("Kernel32.dll")]
		static extern IntPtr CreateFile(
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
			IntPtr hConsoleOutput,
			CharInfo[] lpBuffer,
			Coord dwBufferSize,
			Coord dwBufferCoord,
			ref SmallRect lpWriteRegion);

		[StructLayout(LayoutKind.Sequential)]
		struct Coord
		{
			public short X;
			public short Y;
		};

		[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
		struct CharUnion : IEquatable<CharUnion>
		{
			[FieldOffset(0)] public char UnicodeChar;
			[FieldOffset(0)] public byte AsciiChar;

			public bool Equals(CharUnion obj)
			{
				return UnicodeChar == obj.UnicodeChar && AsciiChar == obj.AsciiChar;
			}
		}

		[StructLayout(LayoutKind.Explicit)]
		struct CharInfo : IEquatable<CharInfo>
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

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		static extern bool ReadConsoleInputEx(IntPtr hConsoleInput, out InputRecord buffer, int numInputRecords_UseOne, out int numEventsRead, ushort wFlags);

		const int CONSOLE_READ_NOWAIT = 0x0002;

		[DllImport("kernel32.dll")]
		static extern bool GetNumberOfConsoleInputEvents(IntPtr hConsoleInput, out uint numberOfEvents);

		[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Auto)]
		struct InputRecord
		{
			[FieldOffset(0)] public ushort eventType;
			[FieldOffset(4)] public KeyEventRecord keyEvent;
			[FieldOffset(4)] public MouseEventRecord mouseEvent;
		}

		[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Auto)]
		struct KeyEventRecord
		{
			[FieldOffset(0)] public bool keyDown;
			[FieldOffset(4)] public short repeatCount;
			[FieldOffset(6)] public short virtualKeyCode;
			[FieldOffset(8)] public short virtualScanCode;
			[FieldOffset(10)] public char uChar;
			[FieldOffset(12)] public int controlKeyState;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct MouseEventRecord
		{
			public Coord mousePosition;
			public uint buttonState;
			public uint controlKeyState;
			public uint eventFlags;
		}

		[DllImport("Kernel32.dll")]
		static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll")]
		static extern bool GetConsoleMode(IntPtr hConsoleHandle, out ConsoleMode lpMode);

		[DllImport("kernel32.dll")]
		static extern bool SetConsoleMode(IntPtr hConsoleHandle, ConsoleMode dwMode);

		[Flags]
		enum ConsoleMode : uint
		{
			ENABLE_WINDOW_INPUT = 0x0008,
			ENABLE_MOUSE_INPUT = 0x0010,
			ENABLE_QUICK_EDIT_MODE = 0x0040
		}

		const int STD_INPUT_HANDLE = -10;

		[DllImport("user32.dll")]
		static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

		const int SWP_NOMOVE = 0x0002;

		[DllImport("user32.dll")]
		static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		static extern bool GetWindowRect(IntPtr hwnd, out Rect lpRect);

		[StructLayout(LayoutKind.Sequential)]
		public struct Rect
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		#endregion

		static bool forceRefresh;

		static readonly IntPtr outputHandle;
		static readonly IntPtr inputHandle;

		static Buffer<CharInfo> buf = new Buffer<CharInfo>();
		static Buffer<CharInfo> previousBuf = new Buffer<CharInfo>();

		public static ConsoleColor BackgroundColor = ConsoleColor.Black;
		public static ConsoleColor ForegroundColor = ConsoleColor.Gray;
		public static int CursorLeft;
		public static int CursorTop;

		public static event EventHandler<ConsoleKeyInfo> KeyDown;
		public static event EventHandler<ConsoleKeyInfo> KeyUp;
		public static event EventHandler<(int x, int y)> MouseMove;
		public static event EventHandler<(int x, int y)> MouseDown;

		static Console()
		{
			outputHandle = CreateFile("CONOUT$", (FileAccess)GENERIC_WRITE, FileShare.Write, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
			inputHandle = GetStdHandle(STD_INPUT_HANDLE);
		}

		public static void Clear()
		{
			CursorLeft = 0;
			CursorTop = 0;
			buf.Clear();
		}

		public static void SetWindowSize(int width, int height)
		{
			var hwnd = GetForegroundWindow();
			if (width == 0 || height == 0)
			{
				GetWindowRect(hwnd, out Rect rect);

				if (width == 0)
				{
					width = rect.Right - rect.Left;
				}

				if (height == 0)
				{
					height = rect.Bottom - rect.Top;
				}
			}

			SetWindowPos(hwnd, IntPtr.Zero, 0, 0, width, height, SWP_NOMOVE);
		}

		public static void Write(char value)
		{
			short color = (short)((int)ForegroundColor | (int)BackgroundColor << 4);
			buf[CursorTop, CursorLeft] = new CharInfo { Char = new CharUnion { UnicodeChar = value }, Attributes = color };
			previousBuf.EnsureCapacity(CursorLeft + 1, CursorTop + 1);
			CursorLeft++;
		}

		public static void Write(string value)
		{
			for (int i = 0; i < value.Length; i++)
			{
				Write(value[i]);
			}
		}

		public static void Write(string format, params object[] args)
		{
			Write(string.Format(format, args));
		}

		public static void SetCursorPosition(int left, int top)
		{
			CursorLeft = left;
			CursorTop = top;
		}

		public static void Flush()
		{
			if (CompareBuffers(out SmallRect rect) || forceRefresh)
			{
				if (forceRefresh) //resize
				{
					System.Console.Clear(); //clear off screen characters
					System.Console.SetCursorPosition(1, 0); //fix terminal bug
					rect = new SmallRect() { Left = 0, Right = (short)(buf.Width - 1), Top = 0, Bottom = (short)(buf.Height - 1) };
					forceRefresh = false;
				}

				WriteConsoleOutput(outputHandle, buf.AsArray(),
					new Coord { X = (short)buf.Width, Y = (short)buf.Height },
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

			for (short y = 0; y < buf.Height; y++)
			{
				for (short x = 0; x < buf.Width; x++)
				{
					if (!buf[y, x].Equals(previousBuf[y, x]))
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

		public static void ProcessEvents()
		{
			while (ReadConsoleInputEx(inputHandle, out InputRecord ir, 1, out int numEvents, CONSOLE_READ_NOWAIT) && numEvents > 0)
			{
				switch (ir.eventType)
				{
					case 1: //key
						var keyInfo = new ConsoleKeyInfo(ir.keyEvent.uChar, (ConsoleKey)(ir.keyEvent.virtualKeyCode & 0xFF), false, false, false);
						if (ir.keyEvent.keyDown)
						{
							KeyDown.Invoke(null, keyInfo);
						}
						else
						{
							KeyUp.Invoke(null, keyInfo);
						}
						break;

					case 2: //mouse
						var position = (ir.mouseEvent.mousePosition.X, ir.mouseEvent.mousePosition.Y);
						if (ir.mouseEvent.buttonState != 0 && ir.mouseEvent.eventFlags == 0)
						{
							MouseDown.Invoke(null, position);
						}
						else
						{
							MouseMove.Invoke(null, position);
						}
						break;

					case 4: //resize
						forceRefresh = true;
						break;
				}
			}
		}

		public static bool MouseInput
		{
			set
			{
				GetConsoleMode(inputHandle, out ConsoleMode previousMode);
				ConsoleMode mode = previousMode;
				if (value)
				{
					mode |= ConsoleMode.ENABLE_MOUSE_INPUT;
					mode &= ~ConsoleMode.ENABLE_QUICK_EDIT_MODE;
				}
				else
				{
					mode &= ~ConsoleMode.ENABLE_MOUSE_INPUT;
					mode |= ConsoleMode.ENABLE_QUICK_EDIT_MODE;
				}

				if (previousMode != mode)
				{
					SetConsoleMode(inputHandle, mode);
				}
			}
		}
	}
}
