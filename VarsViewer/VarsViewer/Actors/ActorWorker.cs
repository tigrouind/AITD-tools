using Newtonsoft.Json;
using Shared;
using System;
using System.Collections.Generic;
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
		int GridHeight => System.Console.WindowHeight - 2;
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
			(int rows, int columns) = cellConfig;

			if (Program.EntryPoint != -1)
			{
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

				int row = 0;
				int offset = 0;
				int height = GridHeight;

				for (int i = 0; i < rows && row < height; i++)
				{
					int col = 0;

					int startAddress = address + i * columns * 2;
					int id = Program.Memory.ReadShort(startAddress);

					if ((id != -1 || showAll) && offset++ >= scroll)
					{
						foreach (var group in config)
						{
							foreach (var column in group.Columns)
							{
								var text = FormatField(column, startAddress, i);
								cells[row, col] = (text, id != -1 ? ConsoleColor.Gray : ConsoleColor.DarkGray);

								if (text != null)
								{
									column.Width = Math.Max(text.Length, column.Width);
									column.Visible |= true;
									group.Visible |= true;
								}
								col++;
							}
						}
						row++;
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
								return $"E{value}R{next:D2}";
							}
							break;

						case ColumnType.TIME:
							if (value != 0)
							{
								long time = (timer - (long)value32) / 60;
								if (time > 0)
								{
									return $"{time / 60}:{time % 60:D2}";
								}
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
								if (column.Values != null)
								{
									return string.Join(" ", column.Values
										.Select((Name, Index) => (Name, Index))
										.Where(x => (value & 1 << x.Index) != 0 && !string.IsNullOrEmpty(x.Name))
										.Select(x => x.Name));
								}
								else
								{
									unchecked
									{
										return $"{(ushort)value:X}";
									}
								}
							}
							break;

						default:
							if (value != 0 && value != -1)
							{
								if (column.Values != null)
								{
									if (value >= 0 && value < column.Values.Length)
									{
										return column.Values[value];
									}
								}
								else
								{
									return value.ToString();
								}
							}
							break;
					}

					return null;

					string FormatVar(VarEnum varType)
					{
						if (value != 0 && value != -1)
						{
							string name = Program.VarParser.GetText(varType, value, 8);
							if (name.Length > 6) name = name.Substring(0, 6);
							return $"{value} {name,-6}";
						}

						return null;
					}
				}
			}

			void ResizeColumns()
			{
				foreach (var group in config.Where(x => x.Visible))
				{
					//make columns large enough to contain label
					group.Width = Math.Max(group.Width, (group.Name ?? "").Length);
					foreach (var col in group.Columns.Where(x => x.Visible))
					{
						col.Width = Math.Max((col.Name ?? "").Length, col.Width);
					}

					//make sure group column is large enough to contain childs
					int childWidth = group.Columns.Where(x => x.Visible).Sum(x => x.Width + 1) - 1;
					group.Width = Math.Max(group.Width, childWidth);

					if (group.Width > childWidth) //enlarge first child column if needed
					{
						group.Columns.First(x => x.Visible).Width += group.Width - childWidth;
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
					Console.Write(PadBoth(group.Name ?? "", group.Width));
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
							Console.Write(" ");
						}

						first = false;
						Console.Write(PadBoth(col.Name ?? "", col.Width));
					}

					Console.CursorLeft++;
				}

				//body
				Console.CursorTop++;
				Console.BackgroundColor = ConsoleColor.Black;

				for (int row = 0; row < cells.Height; row++)
				{
					Console.CursorLeft = 0;

					int col = 0;
					foreach (var group in config)
					{
						foreach (var column in group.Columns)
						{
							if (column.Visible)
							{
								var cell = cells[row, col];
								Console.ForegroundColor = cell.Color;
								int width = column.Width;
								Console.Write((cell.Text ?? "").PadLeft(width));
								Console.CursorLeft++;
							}

							col++;
						}
					}

					Console.CursorTop++;
				}
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
					scroll += GridHeight;
					break;

				case ConsoleKey.PageUp:
					ClearTab();
					scroll -= GridHeight;
					scroll = Math.Max(scroll, 0);
					break;

				case ConsoleKey.Spacebar:
					ClearTab();
					scroll = 0;
					showAll = !showAll;
					break;

				case ConsoleKey.F5:
					ClearTab();
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