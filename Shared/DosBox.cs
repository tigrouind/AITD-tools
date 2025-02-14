using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Shared
{
	public static class DosBox
	{
		static IEnumerable<int> GetProcesses()
		{
			return Process.GetProcesses()
				.Where(x => GetProcessName(x).StartsWith("DOSBOX", StringComparison.InvariantCultureIgnoreCase))
				.Select(x => x.Id);
		}

		static IEnumerable<ProcessMemory> GetProcessReaders()
		{
			foreach (var processId in GetProcesses())
			{
				var proc = new ProcessMemory(processId);
				proc.BaseAddress = proc.SearchFor16MRegion();
				if (proc.BaseAddress != -1)
				{
					yield return proc;
				}
			}
		}

		public static ProcessMemory SearchDosBox(bool onlyAITD = true)
		{
			var processes = GetProcessReaders()
				.ToArray();

			var process = onlyAITD ? processes
					.FirstOrDefault(IsAITDProcess) :
				processes
					.OrderByDescending(IsAITDProcess) //AITD as preference
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
			return process.BaseAddress != -1 && process.Read(mcbData, 0, mcbData.Length) > 0 && DosMCB.GetMCBs(mcbData)
				.Any(x => x.Name.StartsWith("AITD") || x.Name.StartsWith("INDARK") || x.Name.StartsWith("TIMEGATE") || x.Name.StartsWith("TATOU"));
		}

		public static bool GetExeEntryPoint(byte[] memory, out int entryPoint)
		{
			int psp = DosMCB.GetMCBs(memory)
				.Where(x => x.Size > 100 * 1024 && x.Size < 200 * 1024 && x.Owner != 0) //is AITD exe loaded yet?
				.Select(x => x.Owner)
				.FirstOrDefault();

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
