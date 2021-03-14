using System;
using System.IO;
using System.Linq;
using System.Text;
using Shared;

namespace VarsViewer
{
	public class Worker
	{
		ProcessMemoryReader processReader;

		long memoryAddress;
		int entryPoint = -1;
		int gameVersion;
		readonly byte[] memory = new byte[640 * 1024];
		readonly VarParser varParser = new VarParser();
		
		int[] varAddress = { 0x2184B, 0x2048E };
		int[] cvarAddress = { 0x22074, 0x204B8 };

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
				return processReader != null && entryPoint != -1;
			}
		}

		public bool Update()
		{
			if (processReader == null)
			{
				int processId = DosBox.SearchProcess();
				if (processId != -1)
				{
					processReader = new ProcessMemoryReader(processId);
					memoryAddress = processReader.SearchFor16MRegion();
					if (memoryAddress == -1)
					{
						CloseReader();
					}
				}
			}

			if (processReader != null && entryPoint == -1)
			{		
				if (processReader.Read(memory, memoryAddress, memory.Length) > 0 && 
				      DosBox.GetExeEntryPoint(memory, out entryPoint))
				{						
					//check if CDROM/floppy version
					byte[] cdPattern = Encoding.ASCII.GetBytes("CD Not Found");
					gameVersion = Tools.IndexOf(memory, cdPattern) != -1 ? 0 : 1;
				} 
				else
				{
					CloseReader();
				}
			}

			if (processReader != null)
			{
				if (!Freeze)
				{
					bool needRefresh = false;
					int time = Environment.TickCount;
										
					long varsMemoryAddress = memoryAddress + entryPoint + varAddress[gameVersion];
					long cvarsMemoryAddress = memoryAddress + entryPoint + cvarAddress[gameVersion];

					bool result = true;
					if (result &= (processReader.Read(memory, varsMemoryAddress, 4) > 0))
					{
						int varsPointer = memory.ReadFarPointer(0);
						if(varsPointer == 0)
						{							
							InitVars(ref Vars, 0, VarEnum.VARS);
						}
						else 
						{
							InitVars(ref Vars, 207, VarEnum.VARS);
							if (result &= (processReader.Read(memory, memoryAddress + varsPointer, Vars.Length * 2) > 0))
							{								
								needRefresh |= CheckDifferences(Vars, memoryAddress + varsPointer, time);
							}
						}
					}

					InitVars(ref Cvars, 44, VarEnum.C_VARS);
					if (result &= (processReader.Read(memory, cvarsMemoryAddress, Cvars.Length * 2) > 0))
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
			}
			
			return false;
		}

		void CloseReader()
		{
			entryPoint = -1;
			processReader.Close();
			processReader = null;
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

				var.Value = value;

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
				string newText = string.Empty;
				if (value != 0 || difference)
				{
					newText = value.ToString();
				}

				if(var.Difference != difference)
				{
					var.Difference = difference;
					var.Refresh = true;
					needRefresh = true;
				}

				if(var.Text != newText)
				{
					var.Text = newText;
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
			if(processReader != null && var.MemoryAddress != -1)
			{
				memory.Write(value, 0);
				processReader.Write(memory, var.MemoryAddress + var.Index * 2, 2);
			}
		}
	}
}
