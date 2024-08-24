using System;
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
		public static ProcessMemory Process;
		public static int EntryPoint;
		public static GameVersion GameVersion;
		static readonly Stopwatch dosboxTimer = new Stopwatch();

		static readonly IWorker[] workers = new IWorker[] { new VarsWorker(), new CacheWorker(), new ActorWorker(0), new ActorWorker(1) };
		static int view = -1;
		static IWorker Worker => workers[view];

		public static void Main(string[] args)
		{
			ParseArguments();

			Directory.CreateDirectory("GAMEDATA");
			if (File.Exists("GAMEDATA/vars.txt"))
			{
				VarParser.Load("GAMEDATA/vars.txt", VarEnum.SOUNDS, VarEnum.LIFES, VarEnum.BODYS, VarEnum.ANIMS, VarEnum.TRACKS, VarEnum.MUSIC, VarEnum.VARS, VarEnum.CVARS);
			}
			SetupConsole();

			while (true)
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

				if (Process != null)
				{
					if (!Worker.ReadMemory())
					{
						CloseReader();
					}
				}

				Console.Clear();
				Worker.Render();
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
					Console.SetWindowSize(width, height);
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

						default:
							Worker.KeyDown(keyInfo);
							break;
					}
				};

				Console.KeyUp += (sender, keyInfo) =>
				{
					switch (keyInfo.Key)
					{
						case (ConsoleKey)17: //control
							Console.MouseInput = Worker.UseMouse;
							break;
					}
				};

				Console.MouseDown += (sender, position) =>
				{
					Worker.MouseDown(position.x, position.y);
				};

				Console.MouseMove += (sender, position) =>
				{
					Worker.MouseMove(position.x, position.y);
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
			if (Program.view != view)
			{
				Program.view = view;
				Console.MouseInput = Worker.UseMouse;
				System.Console.Title = $"AITD {new string[] { "vars", "cache", "actors", "objects" }[view]} viewer";
			}
		}
	}
}