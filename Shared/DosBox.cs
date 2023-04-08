using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Shared
{
	public class DosBox
	{
		public static int SearchProcess()
		{
			int? processId = Process.GetProcesses()
				.Where(x => GetProcessName(x).StartsWith("DOSBOX", StringComparison.InvariantCultureIgnoreCase))
				.Select(x => (int?)x.Id)
				.FirstOrDefault();

			if(processId.HasValue)
			{
				return processId.Value;
			}

			return -1;
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

		public static DosMCB ReadMCB(byte[] memory, int offset)
		{
			return new DosMCB
			{
				Position = offset + 16,
				Tag = memory[offset],
				Owner = memory.ReadUnsignedShort(offset + 1) * 16,
				Size = memory.ReadUnsignedShort(offset + 3) * 16
			};
		}

		public static IEnumerable<DosMCB> GetMCBs(byte[] memory)
		{
			int firstMCB = memory.ReadUnsignedShort(0x0824) * 16; //sysvars (list of lists) (0x80) + firstMCB (0x24) (see DOSBox/dos_inc.h)

			//scan DOS memory control block (MCB) chain
			int pos = firstMCB;
			while (pos <= (memory.Length - 16))
			{
				DosMCB block = ReadMCB(memory, pos);
				if (block.Tag != 0x4D && block.Tag != 0x5A)
				{
					break;
				}

				yield return block;

				if (block.Tag == 0x5A) //last tag should be 0x5A
				{
					break;
				}

				pos += block.Size + 16;
			}
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
