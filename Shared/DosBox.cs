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
			//scan DOS memory control block (MCB) chain 
			int pos = 0x1190; 
			
			while (pos <= (memory.Length - 16)) 
			{
				var blockTag = memory[pos];
				var blockOwner = memory.ReadUnsignedShort(pos + 1);
				var blockSize = memory.ReadUnsignedShort(pos + 3);
				
				if (blockTag != 0x4D && blockTag != 0x5A) //last tag should be 0x5A
				{
					break;
				}
					
				pos += 16;											
				yield return new DosMCB
				{
					Position = pos,
					Size = blockSize,
					Owner = blockOwner
				};
				
				pos += blockSize * 16;
			}
		}
	}
}
