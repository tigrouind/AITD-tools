using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace VarsViewer
{
	public class VarsWorker : IWorker
	{
		(int VarsAddress, int CvarAddress) GameConfig => gameConfigs[Program.GameVersion];

		bool IWorker.UseMouse => true;

		readonly Dictionary<GameVersion, (int, int)> gameConfigs = new()
		{
			//Vars, Cvars
			{ GameVersion.AITD1,        (0x2184B, 0x22074) },
			{ GameVersion.AITD1_FLOPPY, (0x2048E, 0x204B8) },
			{ GameVersion.AITD1_DEMO,   (0x20456, 0x20480) },
		};

		readonly VarsCollection vars = new(VarEnum.VARS);
		readonly VarsCollection cvars = new(VarEnum.CVARS);

		int varsPointer;

		bool compare;
		bool ignoreDifferences = true;
		bool edit;

		Var highlightedCell;
		string inputText;
		string toolTip;

		int cellSize;

		void IWorker.Render()
		{
			cellSize = Math.Max(3, Math.Min(Console.WindowWidth / 21, 7));
			RenderTab(vars, 11, 0);
			RenderTab(cvars, 1, 12);
			RenderToolTip();

			void RenderTab(VarsCollection vars, int rows, int posY)
			{
				DrawHeader();
				DrawCells();

				void DrawHeader()
				{
					SetHeaderColor();

					Console.SetCursorPosition(0, posY);
					Console.Write(new string(' ', cellSize));
					for (int i = 0; i < 20; i++)
					{
						Console.SetCursorPosition((i + 1) * cellSize, posY);
						Console.Write(Tools.PadBoth(i.ToString(), cellSize));
					}

					for (int i = 0; i < rows; i++)
					{
						Console.SetCursorPosition(0, i + 1 + posY);
						Console.Write((i * 20).ToString().PadLeft(cellSize));
					}
				}

				void DrawCells()
				{
					for (int i = 0; i < rows * 20; i++)
					{
						Console.SetCursorPosition((i % 20 + 1) * cellSize, i / 20 + 1 + posY);

						if (i < vars.Count)
						{
							var var = vars[i];
							SetCellColor(var);
							var text = var == highlightedCell && inputText != null ? inputText : var.Text;
							int center = (cellSize - text.Length) / 2;

							if (var == highlightedCell && edit)
							{
								Console.Write(new string(' ', Math.Max(0, center)));
								Console.BackgroundColor = ConsoleColor.DarkCyan;
							}
							else
							{
								text = Tools.PadBoth(text, cellSize);
							}

							Console.Write(Tools.SubString(text, cellSize, true));

							if (var == highlightedCell && edit)
							{
								SetCellColor(var);
								Console.Write(new string(' ', Math.Max(0, cellSize - center - text.Length)));
							}
						}
						else
						{
							SetHeaderColor();
							Console.Write(new string(' ', cellSize));
						}
					}
				}

				void SetHeaderColor()
				{
					(Console.BackgroundColor, Console.ForegroundColor) = (Program.Freeze ? ConsoleColor.Blue : ConsoleColor.DarkGray, ConsoleColor.Black);
				}

				void SetCellColor(Var var)
				{
					if (var == highlightedCell)
					{
						(Console.BackgroundColor, Console.ForegroundColor) = (ConsoleColor.DarkGray, ConsoleColor.Gray);
					}
					else if (var.Difference)
					{
						(Console.BackgroundColor, Console.ForegroundColor) = (ConsoleColor.Red, ConsoleColor.White);
					}
					else
					{
						(Console.BackgroundColor, Console.ForegroundColor) = (ConsoleColor.Black, ConsoleColor.Gray);
					}
				}
			}

			void RenderToolTip()
			{
				if (toolTip != null)
				{
					(Console.BackgroundColor, Console.ForegroundColor) = (ConsoleColor.DarkGreen, ConsoleColor.Black);
					Console.SetCursorPosition(0, 14);
					Console.Write(toolTip);

					if (highlightedCell != null)
					{
						if (highlightedCell.Text.Length > cellSize)
						{
							Console.SetCursorPosition(cellSize * 21 - highlightedCell.Text.Length, 14);
							Console.Write(highlightedCell.Text);
						}
					}
				}
			}
		}

		void IWorker.ReadMemory()
		{
			long time = Stopwatch.GetTimestamp();

			varsPointer = Program.Memory.ReadFarPointer(GameConfig.VarsAddress + Program.EntryPoint);
			if (varsPointer == 0)
			{
				vars.Count = 0;
			}
			else if ((varsPointer + vars.Count * 2) < Program.Memory.Length) //should never happen, unless game crashes
			{
				vars.Count = Program.GameVersion == GameVersion.AITD1_DEMO ? 22 : 207;
				CheckDifferences(varsPointer, vars);
			}

			CheckDifferences(GameConfig.CvarAddress + Program.EntryPoint, cvars);
			cvars.Count = 16;

			ignoreDifferences = false;

			void CheckDifferences(int address, VarsCollection data)
			{
				for (int i = 0; i < data.Count; i++)
				{
					Var var = data[i];
					int value;

					if (compare)
					{
						value = var.SaveState;
					}
					else
					{
						value = Program.Memory.ReadShort(address + i * 2 + 0);
					}

					if (ignoreDifferences)
					{
						var.Time = 0;
					}
					else if (value != var.Value)
					{
						if (compare)
						{
							var.Time = long.MaxValue;
						}
						else
						{
							var.Time = time;
						}
					}

					//check differences
					bool difference = Shared.Tools.GetTimeSpan(time, var.Time) < TimeSpan.FromSeconds(5);
					if (var.Value != value || var.Difference != difference)
					{
						string newText = string.Empty;
						if (value != 0 || difference)
						{
							newText = value.ToString();
						}

						var.Text = newText;
						var.Value = value;
						var.Difference = difference;
					}
				}
			}
		}

		#region Keyboard

		void IWorker.KeyDown(ConsoleKeyInfo keyInfo)
		{
			switch (keyInfo.Key)
			{
				case ConsoleKey.C:
					Abort();
					if (compare || vars.Any(x => x.SaveState != x.Value) || cvars.Any(x => x.SaveState != x.Value))
					{
						compare = !compare;
						ignoreDifferences = !compare;
					}
					break;

				case ConsoleKey.S:
					SaveState();
					break;

				case ConsoleKey.Backspace:
					BeginEdit();
					if (inputText.Length > 0)
					{
						inputText = inputText[..^1];
					}
					break;

				case ConsoleKey.Enter:
					Commit();
					break;

				case ConsoleKey.Escape:
					if (edit)
					{
						Abort();
					}
					else
					{
						highlightedCell = null;
					}
					break;

				case ConsoleKey.Delete:
					inputText = string.Empty;
					break;


				case ConsoleKey.DownArrow:
				case ConsoleKey.LeftArrow:
				case ConsoleKey.UpArrow:
				case ConsoleKey.RightArrow:
					if (highlightedCell != null)
					{
						var cells = highlightedCell.Type == VarEnum.VARS ? vars : cvars;
						int offset = highlightedCell.Index;
						switch (keyInfo.Key)
						{
							case ConsoleKey.DownArrow:
								offset += 20;

								if (offset >= vars.Count && highlightedCell.Type == VarEnum.VARS)
								{
									offset %= 20;
									cells = cvars;
								}
								break;

							case ConsoleKey.UpArrow:
								offset -= 20;

								if (offset < 0 && highlightedCell.Type == VarEnum.CVARS)
								{
									int x = (offset + 20) % 20;
									int y = (vars.Count - 1 - x ) / 20;
									offset = x + y * 20;
									cells = vars;
								}
								break;

							case ConsoleKey.LeftArrow:
								if (offset % 20 != 0)
								{
									offset -= 1;
								}
								break;

							case ConsoleKey.RightArrow:
								if (offset % 20 != 19)
								{
									offset += 1;
								}
								break;
						}

						if (offset >= 0 && offset < cells.Count)
						{
							Commit();
							highlightedCell = cells[offset];
							SetToolTip();
						}
					}
					else
					{
						highlightedCell = vars[0];
						SetToolTip();
					}
					break;
			}

			switch (keyInfo.KeyChar)
			{
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
					BeginEdit();
					if (inputText.Length < (inputText.Contains('-') ? 6 : 5))
					{
						inputText += keyInfo.KeyChar;
					}
					break;

				case '-':
					BeginEdit();
					if (inputText.Length == 0)
					{
						inputText = "-";
					}
					break;
			}

			void SaveState()
			{
				Save(vars);
				Save(cvars);

				void Save(VarsCollection data)
				{
					foreach (Var var in data)
					{
						var.SaveState = var.Value;
					}
				}
			}
		}

		#endregion

		#region Cell edit

		void StartEdit()
		{
			inputText = null;
			edit = true;
		}

		void BeginEdit()
		{
			inputText ??= string.Empty;
			edit = true;
		}

		void Abort()
		{
			inputText = null;
			edit = false;
		}

		void Commit()
		{
			if (highlightedCell != null)
			{
				if (inputText != null)
				{
					int value = 0;
					if ((inputText == string.Empty || int.TryParse(inputText, out value)) && value != highlightedCell.Value)
					{
						value = Math.Min(value, short.MaxValue);
						value = Math.Max(value, short.MinValue);
						Write(highlightedCell, (short)value);
					}
				}

				Abort();
			}

			void Write(Var var, short value)
			{
				if (Program.Process != null)
				{
					int memoryAddress = var.Type == VarEnum.VARS ? varsPointer : GameConfig.CvarAddress + Program.EntryPoint;
					Program.Memory.Write(value, 0);
					Program.Process.Write(Program.Memory, memoryAddress + var.Index * 2, 2);
				}
			}
		}

		#endregion

		#region Mouse

		void IWorker.MouseMove(int x, int y)
		{
			if (inputText == null && !edit)
			{
				TryFindVarAtPosition(x, y, out highlightedCell);
			}

			SetToolTip();
		}

		void SetToolTip()
		{
			if (highlightedCell != null)
			{
				var text = Program.VarParser.GetText(highlightedCell.Type, highlightedCell.Index);
				if (!string.IsNullOrEmpty(text))
				{
					toolTip = string.Format("#{0}: {1}", highlightedCell.Index, text);
				}
				else
				{
					toolTip = string.Format("#{0}", highlightedCell.Index);
				}
			}
			else
			{
				toolTip = null;
			}
		}

		void IWorker.MouseDown(int x, int y)
		{
			if (!Program.Freeze && !compare)
			{
				Commit();
				if (Program.Process != null && TryFindVarAtPosition(x, y, out highlightedCell))
				{
					StartEdit();
				}
			}
		}

		bool TryFindVarAtPosition(int x, int y, out Var result)
		{
			result = vars.Concat(cvars)
				.Where(c => Intersect(GetPosition(c), cellSize, 1))
				.FirstOrDefault();

			return result != null;

			bool Intersect((int x, int y) pos, int width, int height)
			{
				return x >= pos.x && x < (pos.x + width) && y >= pos.y && y < (pos.y + height);
			}

			(int x, int y) GetPosition(Var var)
			{
				int cellx = var.Index % 20;
				int celly = var.Index / 20;
				int rowIndex = var.Type == VarEnum.VARS ? 1 : 13;
				return ((cellx + 1) * cellSize, celly + rowIndex);
			}
		}

		void IWorker.MouseWheel(int delta)
		{
		}

		#endregion
	}
}
