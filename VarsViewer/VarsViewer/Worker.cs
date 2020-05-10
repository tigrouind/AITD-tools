using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Shared;

namespace VarsViewer
{
	public class Worker
	{
		ProcessMemoryReader processReader;		
		Thread thread;
		readonly Action needRefreshCallback;		
		bool running = true;		
				
		long varsMemoryAddress = -1;
		long cvarsMemoryAddress = -1;		
		readonly byte[] memory = new byte[512];					
		readonly byte[] varsMemoryPattern = { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2E, 0x00, 0x2F, 0x00, 0x00, 0x00, 0x00 };
		readonly byte[] cvarsMemoryPattern = { 0x31, 0x00, 0x0E, 0x01, 0xBC, 0x02, 0x12, 0x00, 0x06, 0x00, 0x13, 0x00, 0x14, 0x00, 0x01 };
			
		public readonly Var[] Vars = new Var[207];		
		public readonly Var[] Cvars = new Var[44];		
		readonly VarParser varParser = new VarParser();		
						
		public bool Compare;
		public bool IgnoreDifferences = true;
		public bool Freeze;
		
		public Worker(Action needRefreshCallback)
		{
			//parse vars.txt file
			const string varPath = @"GAMEDATA\vars.txt";
			if (File.Exists(varPath))
			{	
				varParser.Parse(varPath, "VARS", "C_VARS");
			}
						
			InitVars(Vars, "VARS");
			InitVars(Cvars, "C_VARS");
			this.needRefreshCallback = needRefreshCallback;
		}
						
		void InitVars(Var[] data, string sectionName)
		{
			for(int i = 0 ; i < data.Length ; i++)
			{
				var var = new Var();
				var.Index = i;
				var.Name = varParser.GetText(sectionName, var.Index);		
				data[i] = var;
			}
		}
		
		public void Start()
		{
			thread = new Thread(Run);
			thread.Start();
		}
		
		public void Run()
		{
			while (running)
			{
				if (processReader == null)
				{
					processReader = ProcessMemoryReader.SearchDosBox();
				}
								
				if (processReader != null && (varsMemoryAddress == -1 || cvarsMemoryAddress == -1))
				{
					if (!processReader.SearchForBytePattern(varsMemoryPattern, (buf, len, index, readPosition) => 
						{ 
							varsMemoryAddress = readPosition;
						}) || 
						!processReader.SearchForBytePattern(cvarsMemoryPattern, (buf, len, index, readPosition) => 
						{	
                           	cvarsMemoryAddress = readPosition; 
                       	})
					)
					{
						processReader.Close();
						processReader = null;
					}
				}	
				
				if (processReader != null && varsMemoryAddress != -1 && cvarsMemoryAddress != -1)
				{		
					if (!Freeze)
					{
						bool needRefresh = false;
						if (processReader.Read(memory, varsMemoryAddress, 207 * 2) > 0)
						{					
							needRefresh |= CheckDifferences(Vars, varsMemoryAddress);
						}
						else
						{
							varsMemoryAddress = -1;
						}
						
						if (processReader.Read(memory, cvarsMemoryAddress, 44 * 2) > 0)
						{
							needRefresh |=CheckDifferences(Cvars, cvarsMemoryAddress);
						}
						else
						{
							cvarsMemoryAddress = -1;
						}
						
						IgnoreDifferences = false; 
											
						if (needRefresh && running)
						{
							needRefreshCallback();
						}
					}
					
					Thread.Sleep(16); //60 Hz
				}
				else
				{
					Thread.Sleep(1000);
				}				
			}
		}	
				
		public void Shutdown()
		{
			running = false;
		}
		
		bool CheckDifferences(Var[] data, long offset)
		{
			bool needRefresh = false;
			int currenttime = Environment.TickCount;
			
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
						var.Time = currenttime;
					}
				}
	
				var.MemoryAddress = offset + i * 2;
	
				//check differences
				bool difference = (currenttime - var.Time) < 5000;	
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
			if(processReader != null && varsMemoryAddress != -1 && cvarsMemoryAddress != -1)
			{
				memory.Write(value, 0);
				processReader.Write(memory, var.MemoryAddress, 2);
			}
		}				
	}
}
