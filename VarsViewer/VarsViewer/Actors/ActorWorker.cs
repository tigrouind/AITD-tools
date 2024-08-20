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

		int GetAddress(int view) => view == 0 ? GameConfig.ActorAddress + Program.EntryPoint : Program.Memory.ReadFarPointer(GameConfig.ObjectAddress + Program.EntryPoint);
		(int Rows, int Columns) CellConfig(int view) => view == 0 ? (50, 80) : (Program.GameVersion == GameVersion.AITD1_DEMO ? 18 : 292, 26);

		static (int ActorAddress, int ObjectAddress) GameConfig => gameConfigs[Program.GameVersion];
		static readonly Dictionary<GameVersion, (int, int)> gameConfigs = new Dictionary<GameVersion, (int, int)>
		{
			{ GameVersion.AITD1,        (0x220CE, 0x2400E) },
			{ GameVersion.AITD1_FLOPPY, (0x20542, 0x18BF0) },
			{ GameVersion.AITD1_DEMO,   (0x2050A, 0x18BB8) },
		};

		readonly Buffer<(string Text, ConsoleColor Color)> cells = new Buffer<(string, ConsoleColor)>();
		int scroll;
		bool showAll, freeze, fullMode;
		readonly List<Column>[] configs = new List<Column>[2];
		readonly Stopwatch refreshStopwatch = Stopwatch.StartNew();

		public ActorWorker()
		{
			configs[0] = LoadConfig("Actor.json");
			configs[1] = LoadConfig("Object.json");

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

		void IWorker.Render(int view)
		{
			int rowsCount = 0;
			var timeStamp = Stopwatch.GetTimestamp();
			(int rows, int columns) = CellConfig(view);
			var config = configs[view];

			if (refreshStopwatch.Elapsed > TimeSpan.FromSeconds(5))
			{
				refreshStopwatch.Restart();
				HideColumns();
			}

			ReadActors();
			ResizeColumns();
			OutputToConsole();

			void ReadActors()
			{
				cells.Clear();
				int address = GetAddress(view);

				if (Enumerable.Range(0, rows)
					.All(x => Program.Memory.ReadShort(address + x * columns * 2) == 0)) //are actors initialized ?
				{
					return;
				}

				uint timer1 = Program.Memory.ReadUnsignedInt(Program.EntryPoint + 0x19D12);
				ushort timer2 = Program.Memory.ReadUnsignedShort(Program.EntryPoint + 0x242E0);

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

							foreach (var colGroup in group.Columns)
							{
								col = startCol;

								foreach (var column in colGroup.Columns)
								{
									var text = FormatField(column, startAddress, i);

									cells[rowsCount, col] = (text, id != -1 ? ConsoleColor.Gray : ConsoleColor.DarkGray);

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

				string FormatField(Column column, int startAddress, int i)
				{
					int pos = column.Offset;
					var value32 = Program.Memory.ReadUnsignedInt(startAddress + pos);
					var value = unchecked((short)value32);
					var next = unchecked((short)(value32 >> 16));
					var uValue = unchecked((ushort)value32);

					if (value == 0 && !column.IncludeZero)
					{
						return null;
					}

					if (column.Condition != 0)
					{
						int otherValue = Program.Memory.ReadShort(startAddress + column.Condition);
						if (otherValue == -1 || otherValue == 0)
						{
							return null;
						}
					}

					switch (column.Type)
					{
						case ColumnType.SLOT:
							return i.ToString();

						case ColumnType.ZVPOS:
							return $"{(value + next) / 2}";

						case ColumnType.ZVSIZE:
							return $"{next - value}";

						case ColumnType.BODY:
							return FormatVar(VarEnum.BODYS);

						case ColumnType.LIFE:
							return FormatVar(VarEnum.LIFES);

						case ColumnType.TRACK:
							return FormatVar(VarEnum.TRACKS);

						case ColumnType.ANIM:
							return FormatVar(VarEnum.ANIMS);

						case ColumnType.ANGLE:
							return $"{Math.Floor((value + 1024) % 1024 * 360.0f / 1024.0f)}";

						case ColumnType.ROOM:
							if (value != -1)
							{
								return $"E{value}R{next}";
							}
							break;

						case ColumnType.TIME:
							if (Program.GameVersion == GameVersion.AITD1)
							{
								var elapsed = (timer1 - (long)value32) / 60;
								if (elapsed > 0)
								{
									return $"{elapsed / 60}:{elapsed % 60:D2}";
								}
							}
							break;

						case ColumnType.TIME2:
							if (Program.GameVersion == GameVersion.AITD1)
							{
								var elapsed = timer2 - uValue;
								if (elapsed > 0 && elapsed < 60)
								{
									return elapsed.ToString();
								}
							}
							break;

						case ColumnType.TIME3:
							if (uValue > 60)
							{
								if (Program.GameVersion == GameVersion.AITD1)
								{
									var elapsed = unchecked((ushort)timer1) - uValue;
									if (elapsed > 0 && elapsed < 300)
									{
										return elapsed.ToString();
									}
								}

								return null;
							}

							return FormatVar(VarEnum.TRACKS);

						case ColumnType.FLAGS:
							if (value != -1)
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
							break;
					}

					return null;

					string FormatVar(VarEnum varType)
					{
						if (value != -1 && value != -2)
						{
							if (fullMode)
							{
								string name = Tools.SubString(Program.VarParser.GetText(varType, value), 6).Trim().Replace(" ", "_");
								if (!string.IsNullOrEmpty(name))
								{
									return $"{value}:{name}";
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
				Console.BackgroundColor = ConsoleColor.Black;
				int height = System.Console.WindowHeight - 2;

				for (int row = 0; row < Math.Min(height, rowsCount - scroll); row++)
				{
					Console.CursorLeft = 0;

					int col = 0;
					foreach (var group in config)
					{
						foreach (var column in group.Columns[0].Columns)
						{
							if (column.Visible)
							{
								var cell = cells[row + scroll, col];
								Console.ForegroundColor = cell.Color;
								Console.Write(Tools.PadBoth(cell.Text ?? "", column.Width + column.ExtraWidth));
								Console.CursorLeft++;
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
				foreach (var group in configs.SelectMany(x => x))
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