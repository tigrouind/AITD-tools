using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VarsViewer
{
	public static class Console
	{
		#region Native

		#region Output

		[DllImport("Kernel32.dll")]
		static extern IntPtr CreateFile(
			string fileName,
			FileAccess fileAccess,
			FileShare fileShare,
			IntPtr securityAttributes,
			FileMode creationDisposition,
			FileAttributes flags,
			IntPtr template);

		const uint GENERIC_WRITE = 0x40000000;

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		static extern bool WriteConsoleOutput(
			IntPtr hConsoleOutput,
			CharInfo[,] lpBuffer,
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

			public readonly bool Equals(CharUnion obj)
			{
				return UnicodeChar == obj.UnicodeChar && AsciiChar == obj.AsciiChar;
			}

			public override readonly bool Equals(object obj)
			{
				return obj is CharUnion union && Equals(union);
			}

			public override int GetHashCode()
			{
				throw new NotImplementedException();
			}
		}

		[StructLayout(LayoutKind.Explicit)]
		struct CharInfo : IEquatable<CharInfo>
		{
			[FieldOffset(0)] public CharUnion Char;
			[FieldOffset(2)] public short Attributes;

			public readonly bool Equals(CharInfo obj)
			{
				return Attributes == obj.Attributes && Char.Equals(obj.Char);
			}

			public override readonly bool Equals(object obj)
			{
				return obj is CharInfo info && Equals(info);
			}

			public override int GetHashCode()
			{
				throw new NotImplementedException();
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

		#region Input

		[DllImport("Kernel32.dll")]
		static extern IntPtr GetStdHandle(int nStdHandle);

		const int STD_INPUT_HANDLE = -10;

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		static extern bool ReadConsoleInputEx(IntPtr hConsoleInput, out InputRecord buffer, int numInputRecords_UseOne, out int numEventsRead, ushort wFlags);

		const int CONSOLE_READ_NOWAIT = 0x0002;

		[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Auto)]
		struct InputRecord
		{
			[FieldOffset(0)] public ushort eventType;
			[FieldOffset(4)] public KeyEventRecord keyEvent;
			[FieldOffset(4)] public MouseEventRecord mouseEvent;
			[FieldOffset(4)] public Coord windowBufferSizeEvent;
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

		#endregion

		#region ConsoleMode

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

		#endregion

		#region WindowPos

		[DllImport("user32.dll")]
		static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

		const int SWP_NOMOVE = 0x0002;

		[DllImport("kernel32.dll")]
		static extern IntPtr GetConsoleWindow();

		[DllImport("user32.dll")]
		static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

		const int GW_OWNER = 4;

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

		#endregion

		static bool forceRefresh;

		static readonly IntPtr outputHandle;
		static readonly IntPtr inputHandle;

		static Buffer<CharInfo> buf = new();
		static Buffer<CharInfo> previousBuf = new();

		public static ConsoleColor BackgroundColor = ConsoleColor.Black;
		public static ConsoleColor ForegroundColor = ConsoleColor.Gray;
		public static int CursorLeft;
		public static int CursorTop;
		public static int WindowWidth, WindowHeight;

		public static event EventHandler<ConsoleKeyInfo> KeyDown;
		public static event EventHandler<ConsoleKeyInfo> KeyUp;
		public static event EventHandler<(int x, int y)> MouseMove;
		public static event EventHandler<(int x, int y)> MouseDown;
		public static event EventHandler<int> MouseWheel;

		static Console()
		{
			outputHandle = CreateFile("CONOUT$", (FileAccess)GENERIC_WRITE, FileShare.Write, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
			inputHandle = GetStdHandle(STD_INPUT_HANDLE);
			WindowWidth = System.Console.WindowWidth;
			WindowHeight = System.Console.WindowHeight;
		}

		public static void Clear()
		{
			CursorLeft = 0;
			CursorTop = 0;
			buf.Clear();
		}

		public static void SetWindowSize(int width, int height)
		{
			var hwnd = GetConsoleHandle();
			if (!SetWindowPos(hwnd, IntPtr.Zero, 0, 0, width, height, SWP_NOMOVE))
			{
				throw new InvalidOperationException();
			}
		}

		public static void GetWindowSize(out int width, out int height)
		{
			var hwnd = GetConsoleHandle();
			if (!GetWindowRect(hwnd, out Rect rect))
			{
				throw new InvalidOperationException();
			}

			width = rect.Right - rect.Left;
			height = rect.Bottom - rect.Top;
		}

		static IntPtr GetConsoleHandle()
		{
			var hwnd = GetConsoleWindow(); //works with legacy console host
			var owner = GetWindow(hwnd, GW_OWNER); //needed for new Windows Terminal
			if (owner != IntPtr.Zero) return owner;
			return hwnd;
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
						if (ir.mouseEvent.eventFlags == 1)
						{
							MouseMove.Invoke(null, position);
						}
						else if (ir.mouseEvent.eventFlags == 4)
						{
							MouseWheel.Invoke(null, (ir.mouseEvent.buttonState & 0x80000000) != 0 ? -1 : 1);
						}
						else if (ir.mouseEvent.buttonState != 0)
						{
							MouseDown.Invoke(null, position);
						}
						break;

					case 4: //resize
						forceRefresh = true;
						WindowWidth = ir.windowBufferSizeEvent.X;
						WindowHeight = ir.windowBufferSizeEvent.Y;
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
