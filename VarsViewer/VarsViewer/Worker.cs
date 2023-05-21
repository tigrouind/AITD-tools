using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shared;

namespace VarsViewer
{
	public class Worker
	{
		ProcessMemory process;

		int entryPoint = -1;
		GameVersion gameVersion;
		readonly byte[] memory = new byte[640 * 1024];

		int varsPointer;
		GameConfig gameConfig;
		Dictionary<GameVersion, GameConfig> gameConfigs = new Dictionary<GameVersion, GameConfig>
		{
			{ GameVersion.AITD1,        new GameConfig(0x2184B, 0x22074) },
			{ GameVersion.AITD1_FLOPPY, new GameConfig(0x2048E, 0x204B8) },
			{ GameVersion.AITD1_DEMO  , new GameConfig(0x20456, 0x20480) }
		};

		public readonly List<Var> vars;
		public readonly List<Var> cvars;

		public bool Compare;
		public bool IgnoreDifferences = true;
		public bool Freeze;

		public Worker(List<Var> vars, List<Var> cvars)
		{
			this.vars = vars;
			this.cvars = cvars;
		}

		void InitVars(List<Var> data, int length, VarEnum type)
		{
			if (data.Count != length)
			{
				data.Clear();
				for (int i = 0 ; i < length ; i++)
				{
					var var = new Var();
					var.Index = i;
					var.Type = type;
					var.Text = string.Empty;
					data.Add(var);
				}
			}
		}

		public bool IsRunning
		{
			get
			{
				return process != null && entryPoint != -1;
			}
		}

		public bool Update()
		{
			if (process == null)
			{
				int processId = DosBox.SearchProcess();
				if (processId != -1)
				{
					process = new ProcessMemory(processId);
					process.BaseAddress = process.SearchFor16MRegion();
					if (process.BaseAddress == -1)
					{
						CloseReader();
					}
				}
			}

			if (process != null && entryPoint == -1)
			{
				if (process.Read(memory, 0, memory.Length) > 0 &&
					DosBox.GetExeEntryPoint(memory, out entryPoint))
				{
					//check if CDROM/floppy version
					byte[] cdPattern = Encoding.ASCII.GetBytes("CD Not Found");
					gameVersion = Tools.IndexOf(memory, cdPattern) != -1 ? GameVersion.AITD1 : GameVersion.AITD1_FLOPPY;
					if (gameVersion == GameVersion.AITD1_FLOPPY)
					{
						if (Tools.IndexOf(memory, Encoding.ASCII.GetBytes("USA.PAK")) != -1)
						{
							gameVersion = GameVersion.AITD1_DEMO;
						}
					}

					gameConfig = gameConfigs[gameVersion];
				}
				else
				{
					CloseReader();
				}
			}

			if (process != null && !Freeze)
			{
				bool needRefresh = false;
				int time = Environment.TickCount;

				bool result = true;
				if (result &= (process.Read(memory, gameConfig.VarsAddress + entryPoint, 4) > 0))
				{
					varsPointer = memory.ReadFarPointer(0);
					if (varsPointer == 0)
					{
						InitVars(vars, 0, VarEnum.VARS);
					}
					else
					{
						InitVars(vars, gameVersion == GameVersion.AITD1_DEMO ? 22 : 207, VarEnum.VARS);
						if (result &= (process.Read(memory, varsPointer, vars.Count * 2) > 0))
						{
							needRefresh |= CheckDifferences(vars, time);
						}
					}
				}

				InitVars(cvars, 16, VarEnum.CVARS);
				if (result &= (process.Read(memory, gameConfig.CvarAddress + entryPoint, cvars.Count * 2) > 0))
				{
					needRefresh |= CheckDifferences(cvars, time);
				}

				if (!result)
				{
					CloseReader();
				}

				IgnoreDifferences = false;

				return needRefresh;
			}

			return false;
		}

		void CloseReader()
		{
			entryPoint = -1;
			process.Close();
			process = null;
		}

		bool CheckDifferences(List<Var> data, int time)
		{
			bool needRefresh = false;
			for (int i = 0; i < data.Count; i++)
			{
				Var var = data[i];
				int value;

				if (Compare)
				{
					value = var.SaveState;
				}
				else
				{
					value = memory.ReadShort(i * 2 + 0);
				}

				if (IgnoreDifferences)
				{
					var.Time = 0;
				}
				else if (value != var.Value)
				{
					if (Compare)
					{
						var.Time = int.MaxValue;
					}
					else
					{
						var.Time = time;
					}
				}

				//check differences
				bool difference = Tools.GetTimeSpan(time, var.Time) < TimeSpan.FromSeconds(5);
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
					var.Refresh = true;
					needRefresh = true;
				}
			}

			return needRefresh;
		}

		public void SaveState()
		{
			SaveState(vars);
			SaveState(cvars);
		}

		void SaveState(List<Var> data)
		{
			foreach (Var var in data)
			{
				var.SaveState = var.Value;
			}
		}

		public void Write(Var var, short value)
		{
			if (process != null)
			{
				int memoryAddress = (var.Type == VarEnum.VARS ? varsPointer : gameConfig.CvarAddress + entryPoint);
				memory.Write(value, 0);
				process.Write(memory, memoryAddress + var.Index * 2, 2);
			}
		}
	}
}
