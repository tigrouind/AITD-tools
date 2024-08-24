using Newtonsoft.Json;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using VarsViewer.Actors;

namespace VarsViewer
{
	public class ActorWorker : IWorker
	{
		bool IWorker.UseMouse => false;
		readonly Func<int> getAddress;
		(int Rows, int Columns) cellConfig;
		readonly List<Column> config;

		static (int ActorAddress, int ObjectAddress) GameConfig => gameConfigs[Program.GameVersion];
		static readonly Dictionary<GameVersion, (int, int)> gameConfigs = new Dictionary<GameVersion, (int, int)>
		{
			{ GameVersion.AITD1,        (0x220CE, 0x2400E) },
			{ GameVersion.AITD1_FLOPPY, (0x20542, 0x18BF0) },
			{ GameVersion.AITD1_DEMO,   (0x2050A, 0x18BB8) },
		};

		readonly Buffer<(ConsoleColor, ConsoleColor)> rowColor = new Buffer<(ConsoleColor, ConsoleColor)>();
		readonly Buffer<string> cells = new Buffer<string>();

		readonly Actor[] actors;
		int scroll;
		bool showAll, fullMode;
		static bool freeze;
		long timeStamp, refreshTime;

		public ActorWorker(int view)
		{
			switch (view)
			{
				case 0:
					config = LoadConfig("Actor.json");
					getAddress = () => GameConfig.ActorAddress + Program.EntryPoint;
					cellConfig = (50, 80);
					break;

				case 1:
					config = LoadConfig("Object.json");
					getAddress = () => Program.Memory.ReadFarPointer(GameConfig.ObjectAddress + Program.EntryPoint);
					cellConfig = (Program.GameVersion == GameVersion.AITD1_DEMO ? 18 : 292, 26);
					break;
			}

			actors = new Actor[cellConfig.Rows];

			List<Column> LoadConfig(string fileName)
			{
				var config = LoadJSON();
				Populate();
				return config;

				List<Column> LoadJSON()
				{
					var assembly = Assembly.GetExecutingAssembly();
					string ressourceName = $"VarsViewer.Actors.Config.{fileName}";
					using (var stream = assembly.GetManifestResourceStream(ressourceName))
					using (var reader = new StreamReader(stream))
					{
						return JsonConvert.DeserializeObject<List<Column>>(reader.ReadToEnd(), new JsonSerializerSettings
						{
							DefaultValueHandling = DefaultValueHandling.Populate,
							MissingMemberHandling = MissingMemberHandling.Error
						});
					}
				}

				void Populate()
				{
					for (int i = 0; i < config.Count; i++)
					{
						var column = config[i];
						if (column.Columns == null)
						{
							config[i] = new Column
							{
								Columns = new Column[]
								{
									new Column
									{
										Columns = new Column[]
										{
											column
										}
									}
								}
							};
						}
						else
						{
							if (column.Columns.All(x => x.Columns == null))
							{
								column.Columns = new Column[]
								{
									new Column
									{
										Columns = column.Columns
									}
								};
							}
						}
					}
				}
			}
		}

		void IWorker.Render()
		{
			int rowsCount = 0;
			if (!freeze)
			{
				timeStamp = Stopwatch.GetTimestamp();
			}

			(int rows, int columns) = cellConfig;

			if (TimeSpan.FromTicks(timeStamp - refreshTime) > TimeSpan.FromSeconds(5))
			{
				refreshTime = timeStamp;
				HideColumns();
			}

			if (ReadActors())
			{
				WriteCells();
			}

			ResizeColumns();
			OutputToConsole();

			bool ReadActors()
			{
				int address = getAddress();

				if (Enumerable.Range(0, rows)
					.All(x => Program.Memory.ReadShort(address + x * columns * 2) == 0)) //are actors initialized ?
				{
					return false;
				}

				FieldFormatter.Timer1 = Program.Memory.ReadUnsignedInt(Program.EntryPoint + 0x19D12);
				FieldFormatter.Timer2 = Program.Memory.ReadUnsignedShort(Program.EntryPoint + 0x242E0);
				FieldFormatter.FullMode = fullMode;

				for (int i = 0; i < rows; i++)
				{
					int startAddress = address + i * columns * 2;
					int id = Program.Memory.ReadShort(startAddress);
					var actor = actors[i];

					if (actor == null)
					{
						actor = new Actor
						{
							Id = -1,
							Values = new byte[columns * 2]
						};
						actors[i] = actor;
					}

					if ((actor.Id == -1 && id != -1) || actor.Id != id) //created
					{
						actor.CreationTime = timeStamp;
						actor.DeletionTime = 0;
						actor.UpdateTime = 0;
					}

					if (actor.Id != -1 && id == -1) //deleted
					{
						actor.DeletionTime = timeStamp;
						actor.CreationTime = 0;
						actor.UpdateTime = 0;
					}

					if (id != -1 && actor.Id == id)
					{
						for (int j = 0; j < actor.Values.Length; j++)
						{
							if (Program.Memory[j + startAddress] != actor.Values[j])
							{
								actor.UpdateTime = timeStamp;
								break;
							}
						}
					}

					actor.Id = id;
					Array.Copy(Program.Memory, startAddress, actor.Values, 0, actor.Values.Length);
				}

				return true;
			}

			void WriteCells()
			{
				cells.Clear();
				for (int i = 0; i < rows; i++)
				{
					int col = 0;
					int maxRow = 0;

					Actor actor = actors[i];
					var deleted = Shared.Tools.GetTimeSpan(timeStamp, actor.DeletionTime) < TimeSpan.FromSeconds(2);
					var added = Shared.Tools.GetTimeSpan(timeStamp, actor.CreationTime) < TimeSpan.FromSeconds(2);
					var updated = Shared.Tools.GetTimeSpan(timeStamp, actor.UpdateTime) < TimeSpan.FromSeconds(1);

					if (actor != null && (actor.Id != -1 || showAll || deleted))
					{
						(ConsoleColor, ConsoleColor) color;
						if (deleted)
						{
							color = (ConsoleColor.DarkGray, ConsoleColor.Black); //removed
						}
						else if (added)
						{
							color = (ConsoleColor.DarkGreen, ConsoleColor.Black); //added
						}
						else if (updated)
						{
							color = (ConsoleColor.Black, ConsoleColor.DarkYellow); //updated
						}
						else
						{
							color = (ConsoleColor.Black, actor.Id != -1 ? ConsoleColor.Gray : ConsoleColor.DarkGray);
						}

						foreach (var group in config)
						{
							int startRow = rowsCount;
							int startCol = col;

							foreach (var colGroup in group.Columns)
							{
								col = startCol;
								rowColor[0, rowsCount] = color;

								foreach (var column in colGroup.Columns)
								{
									var text = FieldFormatter.Format(actor.Values, column, i);
									cells[rowsCount, col] = text;

									if (text != null)
									{
										var headerCol = group.Columns[0].Columns[col - startCol];
										headerCol.Width = Math.Max(text.Length, headerCol.Width);
										headerCol.Timer = timeStamp;
										headerCol.Visible |= true;
										group.Visible |= true;

										maxRow = Math.Max(maxRow, rowsCount);
									}

									col++;
								}

								rowsCount++;
							}

							rowsCount = startRow;
						}

						rowsCount = maxRow + 1;
					}
				}
			}

			void HideColumns()
			{
				foreach (var group in config)
				{
					foreach (var column in group.Columns[0].Columns)
					{
						if (column.Visible && TimeSpan.FromTicks(timeStamp - column.Timer) > TimeSpan.FromSeconds(20))
						{
							column.Visible = false;
							column.Width = 0;
							group.Visible = group.Columns[0].Columns.Any(x => x.Visible);
						}
					}
				}
			}

			void ResizeColumns()
			{
				foreach (var group in config.Where(x => x.Visible))
				{
					if (fullMode)
					{
						FitLabel();
					}

					FitChilds();

					void FitLabel()
					{
						//make columns large enough to contain label
						group.Width = (group.Name ?? "").Length;
						foreach (var col in group.Columns[0].Columns
							.Where(x => x.Visible))
						{
							col.Width = Math.Max((col.Name ?? "").Length, col.Width);
						}
					}

					void FitChilds()
					{
						//make sure group column is large enough to contain childs
						int childWidth = group.Columns[0].Columns
							.Where(x => x.Visible)
							.Sum(x => x.Width + 1) - 1;

						group.Width = Math.Max(fullMode ? group.Width : 0, childWidth);

						foreach (var col in group.Columns[0].Columns)
						{
							col.ExtraWidth = 0;
						}

						//enlarge first child column if needed
						group.Columns[0].Columns.First(x => x.Visible).ExtraWidth = group.Width - childWidth;
					}
				}
			}

			void OutputToConsole()
			{
				Console.SetCursorPosition(0, 0);
				(Console.BackgroundColor, Console.ForegroundColor) = (freeze ? ConsoleColor.Blue : ConsoleColor.DarkGray, ConsoleColor.Black);

				//header (groups)
				foreach (var group in config.Where(x => x.Visible))
				{
					Console.Write(Tools.PadBoth(Tools.SubString(group.Name ?? "", group.Width, true), group.Width));
					Console.CursorLeft++;
				}

				//header (childs)
				Console.CursorTop++;
				Console.CursorLeft = 0;

				foreach (var group in config.Where(x => x.Visible))
				{
					bool first = true;
					foreach (var col in group.Columns[0].Columns
						.Where(x => x.Visible))
					{
						if (!first)
						{
							Console.Write("|");
						}

						first = false;
						Console.Write(Tools.PadBoth(Tools.SubString(col.Name ?? "", col.Width + col.ExtraWidth, true), col.Width + col.ExtraWidth));
					}

					Console.CursorLeft++;
				}

				//body
				Console.CursorTop++;
				int height = System.Console.WindowHeight - 2;

				for (int row = 0; row < Math.Min(height, rowsCount - scroll); row++)
				{
					Console.CursorLeft = 0;
					(Console.BackgroundColor, Console.ForegroundColor) = rowColor[0, row + scroll];

					int col = 0;
					foreach (var group in config)
					{
						foreach (var column in group.Columns[0].Columns)
						{
							if (column.Visible)
							{
								if (col > 0) Console.Write(' ');
								var text = cells[row + scroll, col];
								Console.Write(Tools.PadBoth(text ?? "", column.Width + column.ExtraWidth));
							}

							col++;
						}
					}

					Console.CursorTop++;
				}
			}
		}

		bool IWorker.ReadMemory()
		{
			if (!freeze && Program.Process.Read(Program.Memory, 0, 640 * 1024) == 0)
			{
				return false;
			}

			return true;
		}

		void IWorker.KeyDown(ConsoleKeyInfo key)
		{
			switch (key.Key)
			{
				case ConsoleKey.PageDown:
					ClearTab();
					scroll += System.Console.WindowHeight - 2;
					break;

				case ConsoleKey.PageUp:
					if (scroll > 0)
					{
						ClearTab();
						scroll -= System.Console.WindowHeight - 2;
						scroll = Math.Max(0, scroll);
					}
					break;

				case ConsoleKey.Spacebar:
					ClearTab();
					scroll = 0;
					showAll = !showAll;
					break;

				case ConsoleKey.Tab:
					ClearTab();
					fullMode = !fullMode;
					break;

				case ConsoleKey.F:
					freeze = !freeze;
					break;
			}

			void ClearTab()
			{
				cells.Clear();
				foreach (var group in config)
				{
					group.Width = 0;
					group.Visible = false;
					foreach (var col in group.Columns[0].Columns)
					{
						col.Width = 0;
						col.Visible = false;
					}
				}
			}
		}

		void IWorker.MouseMove(int x, int y)
		{
		}

		void IWorker.MouseDown(int x, int y)
		{
		}
	}
}