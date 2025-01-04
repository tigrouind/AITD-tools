using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Shared;

namespace VarsViewer
{
	class Program
	{
		public static readonly byte[] Memory = new byte[640 * 1024];
		public static readonly VarParserForCache VarParser = new VarParserForCache();
		public static readonly Language Language = new Language();
		public static ProcessMemory Process;
		public static int EntryPoint;
		public static GameVersion GameVersion;
		static readonly Stopwatch dosboxTimer = new Stopwatch();
		public static bool Freeze;
		static bool quit;

		static readonly Lazy<IWorker>[] workers = new Lazy<IWorker>[] {
			new Lazy<IWorker>(() => new VarsWorker()),
			new Lazy<IWorker>(() => new CacheWorker()),
			new Lazy<IWorker>(() => new ActorWorker(0)),
			new Lazy<IWorker>(() => new ActorWorker(1))
		};
		static IWorker worker;

		public static void Main(string[] args)
		{
			ParseArguments();

			Directory.CreateDirectory("GAMEDATA");
			if (File.Exists("GAMEDATA/vars.txt"))
			{
				VarParser.Load("GAMEDATA/vars.txt", VarEnum.SOUNDS, VarEnum.LIFES, VarEnum.BODYS, VarEnum.ANIMS, VarEnum.TRACKS, VarEnum.MUSIC, VarEnum.VARS, VarEnum.CVARS);
			}
			Language.Load();

			SetupConsole();

			while (!quit)
			{
				if (Process == null && (!dosboxTimer.IsRunning || dosboxTimer.Elapsed > TimeSpan.FromSeconds(1)))
				{
					SearchDosBox();
					if (Process != null)
					{
						SearchEntryPoint();
					}
					dosboxTimer.Restart();
				}

				Console.ProcessEvents();

				if (Process != null && !Freeze)
				{
					if (Process.Read(Memory, 0, 640 * 1024) != 0)
					{
						worker.ReadMemory();
					}
					else
					{
						CloseReader();
					}
				}

				Console.Clear();
				worker.Render();
				Console.Flush();

				Thread.Sleep(15);
			}

			void ParseArguments()
			{
				string viewArgument = Shared.Tools.GetArgument<string>(args, "-view") ?? "vars";
				int view = Array.IndexOf(new string[] { "vars", "cache", "actors", "objects" }, viewArgument);
				SetView(Math.Max(0, view));

				int width = Shared.Tools.GetArgument<int>(args, "-width");
				int height = Shared.Tools.GetArgument<int>(args, "-height");
				if (width > 0 || height > 0)
				{
					Console.GetWindowSize(out int currentWidth, out int currentHeight);
					Console.SetWindowSize(width == 0 ? currentWidth : width, height == 0 ? currentHeight : height);
				}
			}

			void SetupConsole()
			{
				Console.KeyDown += (sender, keyInfo) =>
				{
					switch (keyInfo.Key)
					{
						case (ConsoleKey)17: //control
							Console.MouseInput = false;
							break;

						case ConsoleKey.F1:
						case ConsoleKey.F2:
						case ConsoleKey.F3:
						case ConsoleKey.F4:
							SetView(keyInfo.Key - ConsoleKey.F1);
							break;

						case ConsoleKey.F:
							Freeze = !Freeze;
							break;

						case ConsoleKey.Escape:
							quit = true;
							break;

						default:
							worker.KeyDown(keyInfo);
							break;
					}
				};

				Console.KeyUp += (sender, keyInfo) =>
				{
					switch (keyInfo.Key)
					{
						case (ConsoleKey)17: //control
							Console.MouseInput = worker.UseMouse;
							break;
					}
				};

				Console.MouseDown += (sender, position) =>
				{
					worker.MouseDown(position.x, position.y);
				};

				Console.MouseMove += (sender, position) =>
				{
					worker.MouseMove(position.x, position.y);
				};

				Console.MouseWheel += (sender, delta) =>
				{
					worker.MouseWheel(delta);
				};
			}
		}

		static void SearchDosBox()
		{
			int processId = DosBox.SearchProcess();
			if (processId != -1)
			{
				Process = new ProcessMemory(processId);
				Process.BaseAddress = Process.SearchFor16MRegion();
				if (Process.BaseAddress == -1)
				{
					CloseReader();
				}
			}
		}

		static void SearchEntryPoint()
		{
			if (Process.Read(Memory, 0, Memory.Length) > 0 &&
				DosBox.GetExeEntryPoint(Memory, out EntryPoint))
			{
				//check if CDROM/floppy version
				byte[] cdPattern = Encoding.ASCII.GetBytes("CD Not Found");
				GameVersion = Shared.Tools.IndexOf(Memory, cdPattern) != -1 ? GameVersion.AITD1 : GameVersion.AITD1_FLOPPY;
				if (GameVersion == GameVersion.AITD1_FLOPPY)
				{
					if (Shared.Tools.IndexOf(Memory, Encoding.ASCII.GetBytes("USA.PAK")) != -1)
					{
						GameVersion = GameVersion.AITD1_DEMO;
					}
				}
			}
			else
			{
				CloseReader();
			}
		}

		static void CloseReader()
		{
			Process.Close();
			Process = null;
		}

		static void SetView(int view)
		{
			worker = workers[view].Value;
			Console.MouseInput = worker.UseMouse;
			System.Console.Title = $"AITD {new string[] { "vars", "cache", "actors", "objects" }[view]} viewer";
		}
	}
}