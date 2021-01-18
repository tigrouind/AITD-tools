using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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

		public static IEnumerable<DosMCB> GetMCBs(byte[] memory, int offset)
		{			
			int firstMCB = memory.ReadUnsignedShort(0x0824 + offset) * 16; //sysvars (list of lists) (0x80) + firstMCB (0x24) (see DOSBox/dos_inc.h)
			
			//scan DOS memory control block (MCB) chain
			int pos = firstMCB + offset;
			while (pos <= (memory.Length - 16))
			{
				var blockTag = memory[pos];
				var blockOwner = memory.ReadUnsignedShort(pos + 1);
				var blockSize = memory.ReadUnsignedShort(pos + 3);
				#if DEBUG
				var blockName = Encoding.ASCII.GetString(memory, pos + 8, 8).Replace("\0", string.Empty);
				#endif

				if (blockTag != 0x4D && blockTag != 0x5A)
				{
					break;
				}

				yield return new DosMCB
				{
					Position = pos + 16 - offset,
					Size = blockSize * 16,
					Owner = blockOwner * 16,
					#if DEBUG
					Name = blockName
					#endif
				};

				if(blockTag == 0x5A) //last tag should be 0x5A
				{
					break;
				}

				pos += blockSize * 16 + 16;
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
