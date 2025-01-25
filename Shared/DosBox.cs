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
			int psp = memory.ReadUnsignedShort(0x0B30) * 16; // 0xB2 (dos swappable area) + 0x10 (current PSP) (see DOSBox/dos_inc.h)
			if (psp > 0)
			{
				int exeSize = memory.ReadUnsignedShort(psp - 16 + 3) * 16;
				if (exeSize > 100 * 1024 && exeSize < 200 * 1024) //is AITD exe loaded yet?
				{
					entryPoint = psp + 0x100;
					return true;
				}
			}

			entryPoint = -1;
			return false;
		}
	}
}
