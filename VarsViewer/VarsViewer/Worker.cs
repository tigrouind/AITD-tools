using System;
using System.IO;
using System.Linq;
using System.Text;
using Shared;

namespace VarsViewer
{
	public class Worker
	{
		ProcessMemoryReader reader;

		int entryPoint = -1;
		int gameVersion;
		readonly byte[] memory = new byte[640 * 1024];
		readonly VarParser varParser = new VarParser();
		long varsMemoryAddress, cvarsMemoryAddress;
		
		int[] varAddress = { 0x2184B, 0x2048E, 0x20456 };
		int[] cvarAddress = { 0x22074, 0x204B8, 0x20480 };

		public Var[] Vars = new Var[0];
		public Var[] Cvars = new Var[0];

		public bool Compare;
		public bool IgnoreDifferences = true;
		public bool Freeze;

		public Worker()
		{
			const string varPath = @"GAMEDATA\vars.txt";
			if (File.Exists(varPath))
			{
				varParser.Load(varPath, VarEnum.VARS, VarEnum.C_VARS);
			}
		}
		
		void InitVars(ref Var[] data, int length, VarEnum section)
		{
			if(data.Length != length)
			{
				Array.Resize(ref data, length);
				for(int i = 0 ; i < length ; i++)
				{
					var var = new Var();
					var.Index = i;
					var.Text = string.Empty;
					var.Name = varParser.GetText(section, var.Index);
					data[i] = var;
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
					gameVersion = Tools.IndexOf(memory, cdPattern) != -1 ? 0 : 1;
					if (gameVersion == 1) //floppy
					{
						if (Tools.IndexOf(memory, Encoding.ASCII.GetBytes("USA.PAK")) != -1)
						{
							gameVersion = 2; //demo
						}
					}
					
					varsMemoryAddress = entryPoint + varAddress[gameVersion];
					cvarsMemoryAddress = entryPoint + cvarAddress[gameVersion];
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
				if (result &= (reader.Read(memory, varsMemoryAddress, 4) > 0))
				{
					int varsPointer = memory.ReadFarPointer(0);
					if(varsPointer == 0)
					{							
						InitVars(ref Vars, 0, VarEnum.VARS);
					}
					else 
					{
						InitVars(ref Vars, gameVersion == 2 ? 22 : 207, VarEnum.VARS);
						if (result &= (reader.Read(memory, varsPointer, Vars.Length * 2) > 0))
						{								
							needRefresh |= CheckDifferences(Vars, varsPointer, time);
						}
					}
				}

				InitVars(ref Cvars, 16, VarEnum.C_VARS);
				if (result &= (reader.Read(memory, cvarsMemoryAddress, Cvars.Length * 2) > 0))
				{
					needRefresh |= CheckDifferences(Cvars, cvarsMemoryAddress, time);
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
		
		bool CheckDifferences(Var[] data, long offset, int time)
		{
			bool needRefresh = false;
			for (int i = 0; i < data.Length; i++)
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

				var.MemoryAddress = offset;

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
			SaveState(Vars);
			SaveState(Cvars);
		}

		void SaveState(Var[] data)
		{
			foreach(Var var in data)
			{
				var.SaveState = var.Value;
			}
		}

		public void Write(Var var, short value)
		{
			if(reader != null && var.MemoryAddress != -1)
			{
				memory.Write(value, 0);
				reader.Write(memory, var.MemoryAddress + var.Index * 2, 2);
			}
		}
	}
}
