using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shared;

namespace VarsViewer
{
	public class Worker
	{
		ProcessMemoryReader reader;

		int entryPoint = -1;
		GameVersion gameVersion;
		readonly byte[] memory = new byte[640 * 1024];
		
		int varsPointer;
		GameConfig gameConfig;
		Dictionary<GameVersion, GameConfig> gameConfigs = new Dictionary<GameVersion, GameConfig>
		{
			{ GameVersion.CD_ROM, new GameConfig(0x2184B, 0x22074) },
			{ GameVersion.FLOPPY, new GameConfig(0x2048E, 0x204B8) },
			{ GameVersion.DEMO  , new GameConfig(0x20456, 0x20480) }
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
			if(data.Count != length)
			{
				data.Clear();
				for(int i = 0 ; i < length ; i++)
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
				return reader != null && entryPoint != -1;
			}
		}

		public bool Update()
		{
			if (reader == null)
			{
				int processId = DosBox.SearchProcess();
				if (processId != -1)
				{
					reader = new ProcessMemoryReader(processId);
					reader.BaseAddress = reader.SearchFor16MRegion();
					if (reader.BaseAddress == -1)
					{
						CloseReader();
					}
				}
			}

			if (reader != null && entryPoint == -1)
			{		
				if (reader.Read(memory, 0, memory.Length) > 0 && 
				      DosBox.GetExeEntryPoint(memory, out entryPoint))
				{						
					//check if CDROM/floppy version
					byte[] cdPattern = Encoding.ASCII.GetBytes("CD Not Found");
					gameVersion = Tools.IndexOf(memory, cdPattern) != -1 ? GameVersion.CD_ROM : GameVersion.FLOPPY;
					if (gameVersion == GameVersion.FLOPPY) 
					{
						if (Tools.IndexOf(memory, Encoding.ASCII.GetBytes("USA.PAK")) != -1)
						{
							gameVersion = GameVersion.DEMO;
						}
					}
					
					gameConfig = gameConfigs[gameVersion];
					gameConfig.VarsAddress += entryPoint;
					gameConfig.CvarAddress += entryPoint;
				} 
				else
				{
					CloseReader();
				}
			}

			if (reader != null && !Freeze)
			{
				bool needRefresh = false;
				int time = Environment.TickCount;

				bool result = true;
				if (result &= (reader.Read(memory, gameConfig.VarsAddress, 4) > 0))
				{
					varsPointer = memory.ReadFarPointer(0);
					if(varsPointer == 0)
					{							
						InitVars(vars, 0, VarEnum.VARS);
					}
					else 
					{
						InitVars(vars, gameVersion == GameVersion.DEMO ? 22 : 207, VarEnum.VARS);
						if (result &= (reader.Read(memory, varsPointer, vars.Count * 2) > 0))
						{								
							needRefresh |= CheckDifferences(vars, time);
						}
					}
				}

				InitVars(cvars, 16, VarEnum.CVARS);
				if (result &= (reader.Read(memory, gameConfig.CvarAddress, cvars.Count * 2) > 0))
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
			reader.Close();
			reader = null;
		}
		
		bool CheckDifferences(List<Var> data, int time)
		{
			bool needRefresh = false;
			for (int i = 0; i < data.Count; i++)
			{
				Var var = data[i];
				int oldValue = var.Value;
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
				else if (value != oldValue)
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
				bool difference = (time - var.Time) < 5000;
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
			foreach(Var var in data)
			{
				var.SaveState = var.Value;
			}
		}

		public void Write(Var var, short value)
		{
			if(reader != null)
			{
				int memoryAddress = var.Type == VarEnum.VARS ? varsPointer : gameConfig.CvarAddress;
				memory.Write(value, 0);
				reader.Write(memory, memoryAddress + var.Index * 2, 2);
			}
		}
	}
}
