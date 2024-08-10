using Newtonsoft.Json;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace VarsViewer
{
	public class ActorWorker : IWorker
	{
		bool IWorker.UseMouse => false;

		(int ActorAddress, int ObjectAddress) gameConfig => gameConfigs[Program.GameVersion];
		(int Rows, int Columns) cellConfig => view == 0 ? (50, 80) : (Program.GameVersion == GameVersion.AITD1_DEMO ? 18 : 292, 26);
		readonly Dictionary<GameVersion, (int, int)> gameConfigs = new Dictionary<GameVersion, (int, int)>
		{
			{ GameVersion.AITD1,        (0x220CE, 0x2400E) },
			{ GameVersion.AITD1_FLOPPY, (0x20542, 0x18BF0) },
			{ GameVersion.AITD1_DEMO,   (0x2050A, 0x18BB8) },
		};

		readonly Buffer<(string Text, ConsoleColor Color)> cells = new Buffer<(string, ConsoleColor)>();
		readonly int view;
		int scroll;
		bool showAll;
		bool fullMode;
		readonly List<Column> config = new List<Column>();

		public ActorWorker(int view)
		{
			this.view = view;

			LoadConfig();

			void LoadConfig()
			{
				var assembly = Assembly.GetExecutingAssembly();
				string ressourceName = $"VarsViewer.Actors.Config.{(view == 0 ? "Actor.json" : "Object.json")}";
				using (var stream = assembly.GetManifestResourceStream(ressourceName))
				using (var reader = new StreamReader(stream))
				{
					JsonConvert.PopulateObject(reader.ReadToEnd(), config, new JsonSerializerSettings
					{
						DefaultValueHandling = DefaultValueHandling.Populate
					});
				}

				foreach (var column in config)
				{
					if (column.Columns == null)
					{
						column.Columns = new Column[]
						{
							new Column() { Type = column.Type, Values = column.Values, Offset = column.Offset }
						};
					}
				}
			}
		}

		void IWorker.Render()
		{
			int rowsCount = 0;
			var timeStamp = Stopwatch.GetTimestamp();
			(int rows, int columns) = cellConfig;

			if (Program.EntryPoint != -1)
			{
				HideColumns();
				ReadActors();
				ResizeColumns();
			}

			OutputToConsole();

			void ReadActors()
			{
				cells.Clear();
				int address = GetAddress();

				if (Enumerable.Range(0, rows)
					.All(x => Program.Memory.ReadShort(address + x * columns * 2) == 0)) //are actors initialized ?
				{
					return;
				}

				uint timer = Program.Memory.ReadUnsignedInt(Program.EntryPoint + 0x19D12);

				int page = 0;
				int height = System.Console.WindowHeight - 2;

				for (int i = 0; i < rows; i++)
				{
					int col = 0;
					int maxRow = 0;

					int startAddress = address + i * columns * 2;
					int id = Program.Memory.ReadShort(startAddress);

					if (id != -1 || showAll)
					{
						foreach (var group in config)
						{
							int startRow = rowsCount;
							int startCol = col;

							foreach (var column in group.Columns)
							{
								switch (column.Type)
								{
									case ColumnType.FILLER:
										col++;
										continue;

									case ColumnType.NEXT:
										col = startCol;
										rowsCount++;
										continue;
								}

								var text = FormatField(column, startAddress, i);
								if (page == scroll)
								{
									cells[rowsCount, col] = (text, id != -1 ? ConsoleColor.Gray : ConsoleColor.DarkGray);
								}

								if (text != null)
								{
									if (page == scroll)
									{
										var subCol = group.Columns[col - startCol];
										subCol.Width = Math.Max(text.Length, subCol.Width);
										subCol.Timer = timeStamp;
										subCol.Visible |= true;
										group.Visible |= true;
									}

									maxRow = Math.Max(maxRow, rowsCount);
								}
								col++;
							}

							rowsCount = startRow;
						}

						rowsCount = maxRow + 1;

						if (rowsCount >= height)
						{
							if (page == scroll)
							{
								break;
							}

							rowsCount = 0;
							page++;
						}
					}
				}

				int GetAddress()
				{
					switch (view)
					{
						case 0: //actors
							return gameConfig.ActorAddress + Program.EntryPoint;

						case 1: //objects
							return Program.Memory.ReadFarPointer(gameConfig.ObjectAddress + Program.EntryPoint);

						default:
							throw new NotSupportedException();
					}
				}

				string FormatField(Column column, int startAddress, int i)
				{
					int pos = column.Offset;
					int value = Program.Memory.ReadShort(startAddress + pos);
					int next = Program.Memory.ReadShort(startAddress + (pos + 2));
					uint value32 = Program.Memory.ReadUnsignedInt(startAddress + pos);

					switch (column.Type)
					{
						case ColumnType.SLOT:
							return i.ToString();

						case ColumnType.ZVPOS:
							if ((value + next) != 0)
							{
								return $"{(value + next) / 2}";
							}
							break;

						case ColumnType.ZVSIZE:
							if ((next - value) != 0)
							{
								return $"{next - value}";
							}
							break;

						case ColumnType.BODY:
							return FormatVar(VarEnum.BODYS);

						case ColumnType.LIFE:
							return FormatVar(VarEnum.LIFES);

						case ColumnType.TRACK:
							return FormatVar(VarEnum.TRACKS);

						case ColumnType.ANIM:
							return FormatVar(VarEnum.ANIMS);

						case ColumnType.ROOM:
							if (value != 0 && value != -1)
							{
								return $"E{value}R{next}";
							}
							break;

						case ColumnType.TIME:
							if (value != 0)
							{
								if (Program.GameVersion == GameVersion.AITD1)
								{
									long time = (timer - (long)value32) / 60;
									if (time > 0)
									{
										return $"{time / 60}:{time % 60:D2}";
									}

									return null;
								}

								return (value32 / 60).ToString();
							}
							break;

						case ColumnType.USHORT:
							if (value != 0 && value != -1)
							{
								unchecked
								{
									if ((ushort)value > 60)
									{
										long time = (ushort)value / 60;
										return $"{time / 60}:{time % 60:D2}";
									}

									return value.ToString();
								}
							}
							break;

						case ColumnType.FLAGS:
							if (value != 0 && value != -1)
							{
								if (column.Values != null && fullMode)
								{
									return string.Join("|", column.Values
										.Where(x => (value & x.Key) != 0 && !string.IsNullOrEmpty(x.Value))
										.Select(x => x.Value));
								}

								unchecked
								{
									return $"{(ushort)value:X}";
								}
							}
							break;

						default:
							if (value != 0)
							{
								if (column.Values != null)
								{
									if (column.Values.TryGetValue(value, out string name) && (fullMode || (name != null && name.Length == 1)))
									{
										return name;
									}
								}

								if (value != -1)
								{
									return value.ToString();
								}
							}
							break;
					}

					return null;

					string FormatVar(VarEnum varType)
					{
						if (value != 0 && value != -1 && value != -2)
						{
							if (fullMode)
							{
								string name = Program.VarParser.GetText(varType, value, 8);
								if (!string.IsNullOrEmpty(name))
								{
									if (name.Length > 6) name = name.Substring(0, 6).Trim().Replace(" ", "_");
									return $"{value}:{name,-6}";
								}
							}

							return value.ToString();
						}

						return null;
					}
				}
			}

			void HideColumns()
			{
				foreach (var group in config)
				{
					foreach (var column in group.Columns)
					{
						var elapsed = TimeSpan.FromTicks(timeStamp - column.Timer);
						if (column.Visible && elapsed > TimeSpan.FromMilliseconds(100) && elapsed < TimeSpan.FromSeconds(5))
						{
							return; //more columns to be hidden soon
						}
					}
				}

				foreach (var group in config)
				{
					foreach (var column in group.Columns)
					{
						var elapsed = TimeSpan.FromTicks(timeStamp - column.Timer);
						if (column.Visible && elapsed > TimeSpan.FromSeconds(5))
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
							.TakeWhile(x => x.Type != ColumnType.NEXT)
							.Where(x => x.Visible))
						{
							col.Width = Math.Max((col.Name ?? "").Length, col.Width);
						}
					}

					void FitChilds()
					{
						//make sure group column is large enough to contain childs
						int childWidth = group.Columns
							.TakeWhile(x => x.Type != ColumnType.NEXT)
							.Where(x => x.Visible)
							.Sum(x => x.Width + 1) - 1;

						group.Width = Math.Max(fullMode ? group.Width : 0, childWidth);

						foreach (var col in group.Columns)
						{
							col.ExtraWidth = 0;
						}

						//enlarge first child column if needed
						group.Columns.First(x => x.Visible).ExtraWidth = group.Width - childWidth;
					}
				}
			}

			void OutputToConsole()
			{
				Console.SetCursorPosition(0, 0);
				(Console.BackgroundColor, Console.ForegroundColor) = (ConsoleColor.DarkGray, ConsoleColor.Black);

				//header (groups)
				foreach (var group in config.Where(x => x.Visible))
				{
					Console.Write(PadBoth(SubString(group.Name ?? "", group.Width), group.Width));
					Console.CursorLeft++;
				}

				//header (childs)
				Console.CursorTop++;
				Console.CursorLeft = 0;

				foreach (var group in config.Where(x => x.Visible))
				{
					bool first = true;
					foreach (var col in group.Columns
						.TakeWhile(x => x.Type != ColumnType.NEXT)
						.Where(x => x.Visible))
					{
						if (!first)
						{
							Console.Write("|");
						}

						first = false;
						Console.Write(PadBoth(SubString(col.Name ?? "", col.Width + col.ExtraWidth), col.Width + col.ExtraWidth));
					}

					Console.CursorLeft++;
				}

				//body
				Console.CursorTop++;
				Console.BackgroundColor = ConsoleColor.Black;

				for (int row = 0; row < rowsCount; row++)
				{
					Console.CursorLeft = 0;

					int col = 0;
					foreach (var group in config)
					{
						foreach (var column in group.Columns
							.TakeWhile(x => x.Type != ColumnType.NEXT))
						{
							if (column.Visible)
							{
								var cell = cells[row, col];
								Console.ForegroundColor = cell.Color;
								Console.Write((cell.Text ?? "").PadLeft(column.Width + column.ExtraWidth));
								Console.CursorLeft++;
							}

							col++;
						}
					}

					Console.CursorTop++;
				}
			}

			string SubString(string text, int length)
			{
				if (text.Length > length)
				{
					return text.Substring(0, length);
				}

				return text;
			}

			string PadBoth(string text, int length)
			{
				int spaces = length - text.Length;
				int padLeft = spaces / 2 + text.Length;
				return text.PadLeft(padLeft).PadRight(length);
			}
		}

		bool IWorker.ReadMemory()
		{
			if (Program.Process.Read(Program.Memory, 0, 640 * 1024) == 0)
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
					scroll++;
					break;

				case ConsoleKey.PageUp:
					ClearTab();
					scroll = Math.Max(scroll - 1, 0);
					break;

				case ConsoleKey.Spacebar:
					ClearTab();
					scroll = 0;
					showAll = !showAll;
					break;

				case ConsoleKey.F5:
					ClearTab();
					break;

				case ConsoleKey.Tab:
					ClearTab();
					fullMode = !fullMode;
					break;
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
		}

		void IWorker.MouseMove(int x, int y)
		{
		}

		void IWorker.MouseDown(int x, int y)
		{
		}
	}
}