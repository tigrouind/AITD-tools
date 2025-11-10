using MoviePlayer;
using Shared;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{
	[DllImport("user32.dll")]
	static extern short GetAsyncKeyState(ConsoleKey key);

	[DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
	static extern void TimeBeginPeriod(uint uMilliseconds);

	[DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
	static extern void TimeEndPeriod(uint uMilliseconds);

	static readonly Stopwatch dosBoxTimer = new();
	static ProcessMemory? process;

	static void Main(string[] args)
	{
		using var heap = new Heap(1024 * 1024 * 16);
		using var movie = new Movie();

		var quit = false;
		var title = string.Empty;

		while (!quit)
		{
			while (Console.KeyAvailable)
			{
				var key = Console.ReadKey(true);
				switch (key.Key)
				{
					case ConsoleKey.D1: //check points
					case ConsoleKey.D2:
					case ConsoleKey.D3:
					case ConsoleKey.D4:
					case ConsoleKey.D5:
					case ConsoleKey.D6:
					case ConsoleKey.D7:
					case ConsoleKey.D8:
					case ConsoleKey.D9:
						int index = key.Key - ConsoleKey.D1;
						if ((key.Modifiers & ConsoleModifiers.Shift) != 0)
						{
							if (movie.SaveState(index))
							{
								Console.WriteLine($"Saved state {index + 1}");
							}
						}
						else
						{
							if (movie.RestoreState(index))
							{
								Console.WriteLine($"Restored state {index + 1}");
								heap.Flush(movie.Memory);
							}
						}
						break;

					case ConsoleKey.F5:
						if ((key.Modifiers & ConsoleModifiers.Shift) != 0)
						{
							if (!movie.IsRecording && !movie.IsLoaded)
							{
								TimeBeginPeriod(1);
								string filePath = $"movie {DateTime.Now:yyyyMMddHHmmss}.dat";
								Console.WriteLine($"Record movie '{filePath}'");
								movie.Save(filePath);
							}
						}
						else
						{
							if (!movie.IsRecording && !movie.IsLoaded)
							{
								string? filePath;
								if (args.Length > 0)
								{
									filePath = args[0];
								}
								else
								{
									var directory = new DirectoryInfo(".");
									filePath = directory.EnumerateFiles("*.dat")
										.OrderByDescending(x => x.LastWriteTime)
										.Select(x => x.FullName)
										.FirstOrDefault();
								}

								if (filePath != null && File.Exists(filePath))
								{
									TimeBeginPeriod(1);
									Console.WriteLine($"Load movie '{Path.GetFileName(filePath)}'");
									movie.Load(filePath);
								}
							}
							else if (!movie.IsRunning)
							{
								movie.Resume();
							}
							else
							{
								movie.Pause();
							}
						}
						break;

					case ConsoleKey.F6:
						movie.SingleStep();
						break;

					case ConsoleKey.F8:
						if (movie.Stop())
						{
							TimeEndPeriod(1);
							Console.WriteLine($"Stop movie");
							heap.Clear();
						}
						break;

					case ConsoleKey.Escape:
						quit = true;
						break;
				}
			}

			void UpdateTitle()
			{
				string formatTime(TimeSpan time) => $"{time:m\\:ss}.{(int)Math.Floor(time.Milliseconds / (1000.0 / 60.0)):D2}";
				var newTitle = string.Empty;
				if (movie.IsLoaded)
				{
					newTitle = $"{formatTime(movie.CurrentTime)}/{formatTime(movie.TotalTime)} {movie.CurrentFrame}/{movie.TotalFrames}";
				}
				else if (movie.IsRecording)
				{
					newTitle = $"{formatTime(movie.CurrentTime)} {movie.CurrentFrame}";
				}

				if (title != newTitle)
				{
					Console.Title = newTitle;
					title = newTitle;
				}
			}

			UpdateTitle();

			if (movie.IsLoaded)
			{
				const int multiplier = 4;
				if (GetAsyncKeyState(ConsoleKey.F7) != 0) // fast forward
				{
					if (movie.PlaybackSpeed == 1)
					{
						movie.PlaybackSpeed = multiplier;
						movie.Resume();
					}
				}
				else
				{
					if (movie.PlaybackSpeed == multiplier)
					{
						movie.PlaybackSpeed = 1;
						movie.Pause();
					}
				}
			}

			if (movie.IsRecording)
			{
				if (process == null && (!dosBoxTimer.IsRunning || dosBoxTimer.Elapsed > TimeSpan.FromSeconds(1)))
				{
					process = DosBox.SearchDosBox();
					if (process != null)
					{
						if (!DosBox.TrySearchEntryPoint(process, movie.Memory, out _, out _))
						{
							process.Close();
							process = null;
						}
					}
					dosBoxTimer.Restart();
				}

				if (process != null)
				{
					if (process.Read(movie.Memory, 0, 640 * 1024) != 0)
					{
						movie.WriteFrame();
					}
					else
					{
						if (movie.Stop())
						{
							Console.WriteLine($"Stop movie");
						}

						process.Close();
						process = null;
					}
				}
			}

			if (movie.IsLoaded)
			{
				while (movie.ReadFrame())
				{
					//allow movies made with PCem to read properly
					var mcb = movie.Memory.ReadUnsignedShort(0xDA6 - 2); //sysvars - 0x02
					if (mcb != 0)
					{
						movie.Memory.Write(mcb, 0x0826 - 2);
					}

					var psp = movie.Memory.ReadUnsignedShort(0x10A0 + 0x10); //SDA + 0x10
					if (psp != 0)
					{
						movie.Memory.Write(psp, 0x0B20 + 0x10);
					}

					heap.Flush(movie.Memory);
				}
			}

			Thread.Sleep(1);
		}
	}
}

