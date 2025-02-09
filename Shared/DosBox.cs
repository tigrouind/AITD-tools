using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Shared
{
	public class DosBox
	{
		static IEnumerable<int> GetProcesses()
		{
			return Process.GetProcesses()
				.Where(x => GetProcessName(x).StartsWith("DOSBOX", StringComparison.InvariantCultureIgnoreCase))
				.Select(x => x.Id);
		}

		public static ProcessMemory SearchDosBox(bool onlyAITD = true)
		{
			var processes = new List<ProcessMemory>();
			foreach (var processId in GetProcesses())
			{
				var proc = new ProcessMemory(processId);
				proc.BaseAddress = proc.SearchFor16MRegion();
				if (proc.BaseAddress != -1)
				{
					processes.Add(proc);
				}
				else
				{
					proc.Close();
				}
			}

			var process = onlyAITD ? processes.FirstOrDefault(IsAITDProcess) :
				processes.OrderByDescending(IsAITDProcess) //AITD as preference
					.FirstOrDefault();

			foreach (var proc in processes.Where(x => x != process))
			{
				proc.Close();
			}

			return process;
		}

		static string GetProcessName(Process process)
		{
			try
			{
				return process.ProcessName;
			}
			catch
			{
				return string.Empty;
			}
		}

		public static bool IsAITDProcess(ProcessMemory process)
		{
			var mcbData = new byte[16384];
			return process.Read(mcbData, 0, mcbData.Length) > 0 && DosMCB.GetMCBs(mcbData)
				.Any(x => IsAITDProcess(x.Name));
		}

		public static bool IsAITDProcess(string name)
		{
			return name.StartsWith("AITD") || name.StartsWith("INDARK") || name.StartsWith("TIMEGATE") || name.StartsWith("TATOU");
		}

		public static bool GetExeEntryPoint(byte[] memory, out int entryPoint)
		{
			int psp = DosMCB.GetMCBs(memory)
				.Where(x => IsAITDProcess(x.Name)) //is AITD exe loaded yet?
				.OrderByDescending(x => x.Size)
				.Select(x => x.Owner)
				.LastOrDefault();

			if (psp > 0)
			{
				entryPoint = psp + 0x100;
				return true;
			}

			entryPoint = -1;
			return false;
		}
	}
}
