using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace VarsViewer
{
	public class VarsWorker : IWorker
	{
		(int VarsAddress, int CvarAddress) gameConfig => gameConfigs[Program.GameVersion];

		bool IWorker.UseMouse => true;

		readonly Dictionary<GameVersion, (int, int)> gameConfigs = new Dictionary<GameVersion, (int, int)>
		{
			//Vars, Cvars
			{ GameVersion.AITD1,        (0x2184B, 0x22074) },
			{ GameVersion.AITD1_FLOPPY, (0x2048E, 0x204B8) },
			{ GameVersion.AITD1_DEMO,   (0x20456, 0x20480) },
		};

		readonly VarsCollection vars = new VarsCollection(VarEnum.VARS);
		readonly VarsCollection cvars = new VarsCollection(VarEnum.CVARS);

		int varsPointer;

		bool compare;
		bool ignoreDifferences = true;
		bool freeze;
		bool edit;

		Var highlightedCell;
		string inputText;
		string toolTip;

		const int CELLSIZE = 5;

		void IWorker.Render()
		{
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
					Console.Write(new string(' ', CELLSIZE));
					for (int i = 0; i < 20; i++)
					{
						Console.SetCursorPosition((i + 1) * CELLSIZE, posY);
						Console.Write(i.ToString().PadLeft(CELLSIZE));
					}

					for (int i = 0; i < rows; i++)
					{
						Console.SetCursorPosition(0, i + 1 + posY);
						Console.Write((i * 20).ToString().PadLeft(CELLSIZE));
					}
				}

				void DrawCells()
				{
					for (int i = 0; i < rows * 20; i++)
					{
						Console.SetCursorPosition((i % 20 + 1) * CELLSIZE, i / 20 + 1 + posY);

						if (i < vars.Count)
						{
							var var = vars[i];
							SetCellColor(var);
							var text = var == highlightedCell && inputText != null ? inputText : var.Text;

							if (var == highlightedCell && edit)
							{
								Console.Write(new string(' ', Math.Max(0, CELLSIZE - text.Length)));
								Console.BackgroundColor = ConsoleColor.DarkCyan;
							}
							else
							{
								text = text.PadLeft(CELLSIZE);
							}

							Console.Write(text.Length > CELLSIZE ? (text.Substring(0, CELLSIZE - 1) + "…") : text);
						}
						else
						{
							SetHeaderColor();
							Console.Write(new string(' ', CELLSIZE));
						}
					}
				}

				void SetHeaderColor()
				{
					(Console.BackgroundColor, Console.ForegroundColor) = (freeze ? ConsoleColor.Blue : ConsoleColor.DarkGray, ConsoleColor.Black);
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
					(Console.BackgroundColor, Console.ForegroundColor) = (ConsoleColor.DarkGreen, ConsoleColor.Gray);
					Console.SetCursorPosition(0, 14);
					Console.Write(toolTip);
				}
			}
		}

		bool IWorker.ReadMemory()
		{
			if (freeze)
			{
				return true;
			}

			long time = Stopwatch.GetTimestamp();

			bool result = true;
			if (result &= Program.Process.Read(Program.Memory, gameConfig.VarsAddress + Program.EntryPoint, 4) > 0)
			{
				varsPointer = Program.Memory.ReadFarPointer(0);
				if (varsPointer == 0)
				{
					vars.Count = 0;
				}
				else
				{
					vars.Count = Program.GameVersion == GameVersion.AITD1_DEMO ? 22 : 207;
					if (result &= Program.Process.Read(Program.Memory, varsPointer, vars.Count * 2) > 0)
					{
						CheckDifferences(vars);
					}
				}
			}

			cvars.Count = 16;
			if (result &= Program.Process.Read(Program.Memory, gameConfig.CvarAddress + Program.EntryPoint, cvars.Count * 2) > 0)
			{
				CheckDifferences(cvars);
			}

			ignoreDifferences = false;

			return result;

			void CheckDifferences(VarsCollection data)
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
						value = Program.Memory.ReadShort(i * 2 + 0);
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

				case ConsoleKey.F:
					Abort();
					freeze = !freeze;
					break;

				case ConsoleKey.S:
					SaveState();
					break;

				case ConsoleKey.Backspace:
					BeginEdit();
					if (inputText.Length > 0)
					{
						inputText = inputText.Remove(inputText.Length - 1);
					}
					break;

				case ConsoleKey.Enter:
					Commit();
					break;

				case ConsoleKey.Escape:
					Abort();
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
						int offset = highlightedCell.Index;
						switch (keyInfo.Key)
						{
							case ConsoleKey.DownArrow:
								offset += 20;
								break;

							case ConsoleKey.UpArrow:
								offset -= 20;
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

						var cells = highlightedCell.Type == VarEnum.VARS ? vars : cvars;
						if (offset >= 0 && offset < cells.Count)
						{
							Commit();
							highlightedCell = cells[offset];
						}
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
					if (inputText.Length < (inputText.Contains("-") ? 6 : 5))
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
			if (inputText == null)
			{
				inputText = string.Empty;
			}
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
					int memoryAddress = var.Type == VarEnum.VARS ? varsPointer : gameConfig.CvarAddress + Program.EntryPoint;
					Program.Memory.Write(value, 0);
					Program.Process.Write(Program.Memory, memoryAddress + var.Index * 2, 2);
				}
			}
		}

		#endregion

		#region Mouse

		void IWorker.MouseMove(int x, int y)
		{
			if (inputText == null && !edit && TryFindVarAtPosition(x, y, out highlightedCell))
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
			if (!freeze && !compare)
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
				.Where(c => Intersect(GetPosition(c), CELLSIZE, 1))
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
				return ((cellx + 1) * CELLSIZE, celly + rowIndex);
			}
		}

		#endregion
	}
}
