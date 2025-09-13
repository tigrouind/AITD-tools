using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace VarsViewer
{
	public class ActorWorker : IWorker
	{
		bool IWorker.UseMouse => true;
		readonly Func<int> getActorAddress;
		readonly (int Rows, int Columns) cellConfig;
		readonly Column[] config;

		static (int ActorAddress, int ObjectAddress) GameConfig => gameConfigs[Program.GameVersion];
		static readonly Dictionary<GameVersion, (int, int)> gameConfigs = new()
		{
			{ GameVersion.AITD1,        (0x220CE, 0x2400E) },
			{ GameVersion.AITD1_FLOPPY, (0x20542, 0x18BF0) },
			{ GameVersion.AITD1_DEMO,   (0x2050A, 0x18BB8) },
		};

		readonly Buffer<(ConsoleColor Background, ConsoleColor Foreground)> rowColor = new();
		readonly Buffer<(string Text, ConsoleColor Color)> cells = new();

		readonly Actor[] actors;
		int scroll;
		bool showAll, fullMode;
		long timeStamp, refreshTime;
		string tooltip;

		public ActorWorker(int view)
		{
			switch (view)
			{
				case 0:
					config = Actors.Instance;
					Populate();
					getActorAddress = () => GameConfig.ActorAddress + Program.EntryPoint;
					cellConfig = (50, 80);
					break;

				case 1:
					config = Objects.Instance;
					Populate();
					getActorAddress = () => Program.Memory.ReadFarPointer(GameConfig.ObjectAddress + Program.EntryPoint);
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

			void Populate()
			{
				for (int i = 0; i < config.Length; i++)
				{
					var column = config[i];
					if (column.Columns == null)
					{
						config[i] = new Column
						{
							Columns =
							[
								column
							]
						};
					}
				}
			}
		}

		void IWorker.Render()
		{
			int rowCount = 0;
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
							foreach (var column in group.Columns)
							{
								rowColor[rowCount] = color;

								var text = FieldFormatter.Format(actor.Values, column, i, fullMode);
								cells[rowCount, col] = (text, FieldFormatter.GetSize(column) != 0 && actor.Updated[column.Offset / 2] ? ConsoleColor.DarkYellow : ConsoleColor.Black);

								if (text != null && !column.Hidden)
								{
									column.Width = Math.Max(text.Length, column.Width);
									column.Timer = timeStamp;
									column.Visible = true;
									if (!group.Hidden)
									{
										group.Visible = true;
									}
								}

								col++;
							}
						}

						rowCount++;
					}
				}
			}

			void HideColumns()
			{
				foreach (var group in config)
				{
					foreach (var column in group.Columns)
					{
						if (column.Visible && TimeSpan.FromTicks(timeStamp - column.Timer) > TimeSpan.FromSeconds(20))
						{
							column.Visible = false;
							column.Width = 0;
							group.Visible = group.Columns.Any(x => x.Visible);
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
						foreach (var col in group.Columns
							.Where(x => x.Visible))
						{
							col.Width = Math.Max((col.Name ?? "").Length, col.Width);
						}
					}

					void FitChilds()
					{
						//make sure group column is large enough to contain childs
						int childWidth = group.Columns
							.Where(x => x.Visible)
							.Sum(x => x.Width + 1) - 1;

						group.Width = Math.Max(fullMode ? group.Width : 0, childWidth);

						foreach (var col in group.Columns)
						{
							col.ExtraWidth = 0;
						}

						//enlarge first child column if needed
						var first = group.Columns.FirstOrDefault(x => x.Visible);
						if (first != null)
						{
							first.ExtraWidth = group.Width - childWidth;
						}
					}
				}
			}

			void OutputToConsole()
			{
				Console.SetCursorPosition(0, 0);
				(Console.BackgroundColor, Console.ForegroundColor) = (Program.Freeze ? ConsoleColor.Blue : ConsoleColor.DarkGray, ConsoleColor.Black);

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
					foreach (var col in group.Columns.Where(x => x.Visible))
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
				int height = Console.WindowHeight - 2;
				int totalWidth = config.Where(x => x.Visible).Sum(x => x.Width + 1) - 1;

				for (int row = 0; row < Math.Min(height, rowCount - scroll); row++)
				{
					Console.CursorLeft = 0;
					(ConsoleColor Background, ConsoleColor Foreground) = rowColor[row + scroll];
					Console.BackgroundColor = Background;

					int col = 0;
					bool anyColumn = false;
					foreach (var group in config)
					{
						foreach (var column in group.Columns)
						{
							if (group.Visible && column.Visible)
							{
								var cell = cells[row + scroll, col];
								if (anyColumn) Console.Write(' ');
								anyColumn = true;

								if (cell.Color != ConsoleColor.Black && Background == ConsoleColor.Black)
								{
									Console.ForegroundColor = cell.Color;
								}
								else
								{
									Console.ForegroundColor = Foreground;
								}

								Console.Write(Tools.PadBoth(cell.Text ?? "", column.Width + column.ExtraWidth));
							}

							col++;
						}
					}

					Console.CursorTop++;
				}

				if (tooltip != null)
				{
					Console.CursorLeft = 0;
					Console.CursorTop = Math.Min(Math.Min(height, rowCount - scroll) + 2, Console.WindowHeight - 1);
					(Console.BackgroundColor, Console.ForegroundColor) = (ConsoleColor.DarkGreen, ConsoleColor.Black);
					Console.Write(tooltip);
				}
			}
		}

		void IWorker.ReadMemory()
		{
			timeStamp = Stopwatch.GetTimestamp();
			ReadActors();

			void ReadActors()
			{
				(int rows, int columns) = cellConfig;
				int actorPointer = getActorAddress();

				if ((actorPointer + rows * columns * 2) >= Program.Memory.Length) //should never happen, unless game crashes
				{
					return;
				}

				if (Enumerable.Range(0, rows)
					.All(x => Program.Memory.ReadShort(actorPointer + x * columns * 2) == 0)) //are actors initialized ?
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
					int startAddress = actorPointer + i * columns * 2;
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
		}

		int RowCount => actors.Count(actor => actor.Id != -1 || showAll || actor.Deleted);
		static int PageHeight => Console.WindowHeight - 2;

		void Scroll(int delta)
		{
			scroll += delta;
			scroll = Math.Max(Math.Min(scroll, RowCount - PageHeight), 0); //do not scroll more that needed
		}

		void IWorker.KeyDown(ConsoleKeyInfo key)
		{
			switch (key.Key)
			{
				case ConsoleKey.PageDown:
					Scroll(PageHeight);
					break;

				case ConsoleKey.PageUp:
					Scroll(-PageHeight);
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

				case ConsoleKey.R:
					foreach (var group in config)
					{
						group.Hidden = false;
						foreach (var col in group.Columns)
						{
							col.Hidden = false;
						}
					}
					break;
			}
		}

		void IWorker.MouseMove(int x, int y)
		{
			int totalWidth = config.Where(c => c.Visible).Sum(c => c.Width + 1) - 1;

			if (!fullMode && x < totalWidth && y < (RowCount - scroll + 2) && TryFindColumn(x, 1, out (Column group, Column column) result))
			{
				tooltip = string.Join(".", (new string[] { result.group.Name, result.column.Name }).Where(c => c != null));
			}
			else
			{
				tooltip = null;
			}
		}

		void IWorker.MouseDown(int x, int y)
		{
			if (TryFindColumn(x, y, out (Column group, Column column) result))
			{
				if (result.column != null)
				{
					result.column.Hidden = true;
					result.column.Visible = false;

					if (result.group.Columns.All(c => c.Hidden))
					{
						result.group.Hidden = true;
						result.group.Visible = false;
					}
				}
				else
				{
					result.group.Hidden = true;
					result.group.Visible = false;
				}
			}
		}

		void IWorker.MouseWheel(int delta)
		{
			Scroll(-3 * Math.Sign(delta));
		}

		void ClearTab()
		{
			cells.Clear();
			foreach (var group in config)
			{
				group.Width = 0;
				group.Visible = false;
				foreach (var col in group.Columns)
				{
					col.Width = 0;
					col.Visible = false;
				}
			}
		}

		bool TryFindColumn(int x, int y, out (Column group, Column col) result)
		{
			int width = 0;
			switch (y)
			{
				case 0:
					foreach (var group in config.Where(c => c.Visible))
					{
						if (x >= width && x < (width + group.Width))
						{
							result = (group, null);
							return true;
						}

						width += group.Width + 1;
					}
					break;

				case 1:
					foreach (var group in config.Where(c => c.Visible))
					{
						foreach (var column in group.Columns.Where(c => c.Visible))
						{
							if (x >= width && x < (width + column.Width))
							{
								result = (group, column);
								return true;
							}

							width += column.Width + 1;
						}
					}
					break;
			}

			result = (null, null);
			return false;
		}

		public void Resize(int width, int height)
		{
			Scroll(0);
		}
	}
}