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
		
		public static IEnumerable<DosMCB> GetMCBs(byte[] memory)
		{
			//scan DOS memory control blocks chain (MCB)
			int pos = 0x1190; 
			byte blockTag = memory[pos];
			
			while (blockTag == 0x4D && pos <= (memory.Length - 16)) //last tag should be 0x5A
			{
				var blockOwner = memory.ReadUnsignedShort(pos + 1);
				var blockSize = memory.ReadUnsignedShort(pos + 3);
				
				pos += 16;											
				if (blockOwner != 0 && blockOwner != 8) //not free or allocated for DOS
				{
					yield return new DosMCB
					{
						Position = pos,
						Size = blockSize,
						Owner = blockOwner
					};
				}
				
				pos += blockSize * 16;
				blockTag = memory[pos];
			}
		}
	}
}
