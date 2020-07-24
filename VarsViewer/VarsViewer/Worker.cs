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
		long varsMemoryAddress = -1;
		long cvarsMemoryAddress = -1;
		readonly byte[] memory = new byte[640 * 1024];

		public readonly Var[] Vars = new Var[207];
		public readonly Var[] Cvars = new Var[44];

		public bool Compare;
		public bool IgnoreDifferences = true;
		public bool Freeze;

		public Worker()
		{
			InitVars();
		}

		void InitVars()
		{
			var varParser = new VarParser();
			const string varPath = @"GAMEDATA\vars.txt";
			if (File.Exists(varPath))
			{
				varParser.Load(varPath, "VARS", "C_VARS");
			}

			InitVars(varParser, Vars, "VARS");
			InitVars(varParser, Cvars, "C_VARS");
		}

		void InitVars(VarParser varParser, Var[] data, string sectionName)
		{
			for(int i = 0 ; i < data.Length ; i++)
			{
				var var = new Var();
				var.Index = i;
				var.Name = varParser.GetText(sectionName, var.Index);
				data[i] = var;
			}
		}
		
		public bool IsRunning
		{
			get
			{
				return processReader != null && varsMemoryAddress != -1 && cvarsMemoryAddress != -1;
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

			if (processReader != null && (varsMemoryAddress == -1 || cvarsMemoryAddress == -1))
			{		
				int entryPoint;
				if (processReader.Read(memory, memoryAddress, memory.Length) > 0 && 
				   DosBox.GetExeEntryPoint(memory, out entryPoint))
				{						
					int varAddress, cvarAddress;
					GetMemoryAddresses(out varAddress, out cvarAddress);
					
					int varsPointer;
					if ((varsPointer = memory.ReadFarPointer(entryPoint + varAddress)) != 0)
					{
						varsMemoryAddress = memoryAddress + varsPointer;
						cvarsMemoryAddress = memoryAddress + entryPoint + cvarAddress;
					}													
					else
					{
						CloseReader();
					}
				} 
				else
				{
					CloseReader();
				}
			}

			if (processReader != null && varsMemoryAddress != -1 && cvarsMemoryAddress != -1)
			{
				if (!Freeze)
				{
					bool needRefresh = false;
					int time = Environment.TickCount;

					bool result;
					if (result = (processReader.Read(memory, varsMemoryAddress, 207 * 2) > 0))
					{
						needRefresh |= CheckDifferences(Vars, varsMemoryAddress, time);
					}

					if (result = (processReader.Read(memory, cvarsMemoryAddress, 44 * 2) > 0))
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
			varsMemoryAddress = cvarsMemoryAddress = -1;
			processReader.Close();
			processReader = null;
		}
		
		void GetMemoryAddresses(out int varAddress, out int cvarAddress)
		{
			//check if CDROM/floppy version
			byte[] cdPattern = Encoding.ASCII.GetBytes("CD Not Found");
			bool isCDROMVersion = Tools.IndexOf(memory, cdPattern) != -1;
			if (isCDROMVersion)
			{
				varAddress = 0x2184B;
				cvarAddress = 0x22074;
			}
			else
			{
				varAddress = 0x2048E;
				cvarAddress = 0x204B8;
			}
		}

		bool CheckDifferences(Var[] data, long offset, int time)
		{
			bool needRefresh = false;
			for (int i = 0; i < data.Length; i++)
			{
				Var var = data[i];
				int oldValue = var.Value;
				short value;

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

				var.MemoryAddress = offset + i * 2;

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
			if(processReader != null)
			{
				memory.Write(value, 0);
				processReader.Write(memory, var.MemoryAddress, 2);
			}
		}
	}
}
