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
		readonly (int Rows, int Columns) cellConfig;
		readonly List<Column> config;

		static (int ActorAddress, int ObjectAddress) GameConfig => gameConfigs[Program.GameVersion];
		static readonly Dictionary<GameVersion, (int, int)> gameConfigs = new Dictionary<GameVersion, (int, int)>
		{
			{ GameVersion.AITD1,        (0x220CE, 0x2400E) },
			{ GameVersion.AITD1_FLOPPY, (0x20542, 0x18BF0) },
			{ GameVersion.AITD1_DEMO,   (0x2050A, 0x18BB8) },
		};

		readonly Buffer<(ConsoleColor Background, ConsoleColor Foreground)> rowColor = new Buffer<(ConsoleColor, ConsoleColor)>();
		readonly Buffer<(string Text, ConsoleColor Color)> cells = new Buffer<(string, ConsoleColor)>();

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
			for (int i = 0; i < actors.Length; i++)
			{
				actors[i] = new Actor
				{
					Id = -1,
					Values = new byte[cellConfig.Columns * 2],
					Updated = new bool[cellConfig.Columns],
					UpdateTime = new long[cellConfig.Columns],
				};
			}

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
			(int rows, int columns) = cellConfig;

			if (TimeSpan.FromTicks(timeStamp - refreshTime) > TimeSpan.FromSeconds(5))
			{
				refreshTime = timeStamp;
				HideColumns();
			}

			WriteCells();
			ResizeColumns();
			OutputToConsole();

			void WriteCells()
			{
				cells.Clear();
				for (int i = 0; i < rows; i++)
				{
					int col = 0;
					int maxRow = 0;

					Actor actor = actors[i];
					if (actor.Id != -1 || showAll || actor.Deleted)
					{
						(ConsoleColor, ConsoleColor) color;
						if (actor.Deleted)
						{
							color = (ConsoleColor.DarkGray, ConsoleColor.Black);
						}
						else if (actor.Created)
						{
							color = (ConsoleColor.DarkGreen, ConsoleColor.Black);
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
									var text = FieldFormatter.Format(actor.Values, column, i, fullMode);
									cells[rowsCount, col] = (text, FieldFormatter.GetSize(column) != 0 && actor.Updated[column.Offset / 2] ? ConsoleColor.DarkYellow : ConsoleColor.Black);

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
					var rowColor = this.rowColor[0, row + scroll];
					Console.BackgroundColor = rowColor.Background;

					int col = 0;
					foreach (var group in config)
					{
						foreach (var column in group.Columns[0].Columns)
						{
							if (column.Visible)
							{
								var cell = cells[row + scroll, col];
								if (col > 0) Console.Write(' ');

								if (cell.Color != ConsoleColor.Black && rowColor.Background == ConsoleColor.Black)
								{
									Console.ForegroundColor = cell.Color;
								}
								else
								{
									Console.ForegroundColor = rowColor.Foreground;
								}

								Console.Write(Tools.PadBoth(cell.Text ?? "", column.Width + column.ExtraWidth));
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
			if (!freeze)
			{
				if (Program.Process.Read(Program.Memory, 0, 640 * 1024) == 0)
				{
					return false;
				}

				timeStamp = Stopwatch.GetTimestamp();
				ReadActors();
			}

			void ReadActors()
			{
				(int rows, int columns) = cellConfig;
				int address = getAddress();

				if (Enumerable.Range(0, rows)
					.All(x => Program.Memory.ReadShort(address + x * columns * 2) == 0)) //are actors initialized ?
				{
					ClearTab();
					foreach (var actor in actors)
					{
						actor.Id = -1;
						actor.Created = false;
						actor.Deleted = false;
						Array.Clear(actor.Updated, 0, actor.Updated.Length);
						Array.Clear(actor.Values, 0, actor.Values.Length);
					}
					return;
				}

				FieldFormatter.Timer1 = Program.Memory.ReadUnsignedInt(Program.EntryPoint + 0x19D12);
				FieldFormatter.Timer2 = Program.Memory.ReadUnsignedShort(Program.EntryPoint + 0x242E0);

				for (int i = 0; i < rows; i++)
				{
					int startAddress = address + i * columns * 2;
					int id = Program.Memory.ReadShort(startAddress);
					var actor = actors[i];

					if ((actor.Id == -1 && id != -1) || actor.Id != id) //created
					{
						actor.CreationTime = timeStamp;
						actor.DeletionTime = 0;
						Array.Clear(actor.UpdateTime, 0, actor.UpdateTime.Length);
					}

					if (actor.Id != -1 && id == -1) //deleted
					{
						actor.DeletionTime = timeStamp;
						actor.CreationTime = 0;
						Array.Clear(actor.UpdateTime, 0, actor.UpdateTime.Length);
					}

					if ((id != -1 || showAll) && actor.Id == id) //compare
					{
						foreach (var column in config
							.SelectMany(x => x.Columns)
							.SelectMany(x => x.Columns))
						{
							switch (FieldFormatter.GetSize(column))
							{
								case 2:
									if (Program.Memory.ReadShort(startAddress + column.Offset) != actor.Values.ReadShort(column.Offset))
									{
										actor.UpdateTime[column.Offset / 2] = timeStamp;
									}
									break;

								case 4:
									if (Program.Memory.ReadInt(startAddress + column.Offset) != actor.Values.ReadInt(column.Offset))
									{
										actor.UpdateTime[column.Offset / 2] = timeStamp;
									}
									break;
							}
						}
					}

					actor.Id = id;

					for (int j = 0; j < actor.UpdateTime.Length; j++)
					{
						actor.Updated[j] = Shared.Tools.GetTimeSpan(timeStamp, actor.UpdateTime[j]) < TimeSpan.FromSeconds(2);
					}
					actor.Deleted = Shared.Tools.GetTimeSpan(timeStamp, actor.DeletionTime) < TimeSpan.FromSeconds(2);
					actor.Created = Shared.Tools.GetTimeSpan(timeStamp, actor.CreationTime) < TimeSpan.FromSeconds(2);

					Array.Copy(Program.Memory, startAddress, actor.Values, 0, actor.Values.Length);
				}
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

		void IWorker.MouseMove(int x, int y)
		{
		}

		void IWorker.MouseDown(int x, int y)
		{
		}
	}
}