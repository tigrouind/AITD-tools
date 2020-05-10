using System.Runtime.InteropServices;
using System;
using System.Linq;

namespace Shared
{
	public class ProcessMemoryReader
	{
		const int PROCESS_QUERY_INFORMATION = 0x0400;
		const int PROCESS_VM_READ = 0x0010;
		const int PROCESS_VM_WRITE = 0x0020;
		const int PROCESS_VM_OPERATION = 0x0008;
		const int MEM_COMMIT = 0x00001000;
		const int MEM_PRIVATE = 0x20000;
		const int PAGE_READWRITE = 0x04;
	
		[StructLayout(LayoutKind.Sequential)]
		struct MEMORY_BASIC_INFORMATION
		{
			public IntPtr BaseAddress;
			public IntPtr AllocationBase;
			public uint AllocationProtect;
			public IntPtr RegionSize;
			public int State;
			public int Protect;
			public int Type;
		}
	
		[DllImport("kernel32.dll")]
		static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
	
		[DllImport("kernel32.dll")]
		static extern bool CloseHandle(IntPtr hObject);
	
		[DllImport("kernel32.dll")]
		static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);
		
		[DllImport("kernel32.dll")]
		static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);
	
		[DllImport("kernel32.dll")]
		static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);
	
		IntPtr processHandle;
	
		public ProcessMemoryReader(int processId)
		{
			this.processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, processId);
		}
	
		~ProcessMemoryReader()
		{
			Close();
		}
	
		public long Read(byte[] buffer, long address, int count)
		{
			return Read(buffer, address, count, 0);
		}
		
		public long Read(byte[] buffer, long address, int count, int offset)
		{
			if((offset + count) > buffer.Length)
			{
				throw new ArgumentOutOfRangeException();
			}
			
			IntPtr bytesRead;
			unsafe
			{	
				fixed (byte* ptr = buffer)
				{				
					if (ReadProcessMemory(processHandle, new IntPtr(address), new IntPtr((void*)(ptr+offset)), count, out bytesRead))
					{
						return (long)bytesRead;
					}
				}
			}
			return 0;
		}
		
		public long Write(byte[] buffer, long offset, int count)
		{
			IntPtr bytesWritten;
			if (WriteProcessMemory(processHandle, new IntPtr(offset), buffer, count, out bytesWritten))
			{
				return (long)bytesWritten;
			}
			return 0;
		}
	
		public void Close()
		{
			if (processHandle != IntPtr.Zero)
			{
				CloseHandle(processHandle);
				processHandle = IntPtr.Zero;
			}
		}
		
		bool Get16MRegion(out long baseAddress, out long regionSize)
		{
			MEMORY_BASIC_INFORMATION mem_info = new MEMORY_BASIC_INFORMATION();
	
			long min_address = 0;
			long max_address = 0x7FFFFFFF;
	
			//scan process memory regions
			while (min_address < max_address
				&& VirtualQueryEx(processHandle, (IntPtr)min_address, out mem_info, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) > 0)
			{
				//check if memory region is accessible
				//skip regions smaller than 16M (default DOSBOX memory size)
				if (mem_info.Protect == PAGE_READWRITE && mem_info.State == MEM_COMMIT && (mem_info.Type & MEM_PRIVATE) == MEM_PRIVATE
				    && (int)mem_info.RegionSize >= 1024 * 1024 * 16)
				{
					baseAddress = (long)mem_info.BaseAddress;
					regionSize = (long)mem_info.RegionSize;
					return true;
				}
	
				// move to next memory region
				min_address = (long)mem_info.BaseAddress + (long)mem_info.RegionSize;
			}
			
			baseAddress = -1;
			regionSize = -1;
			return false;
		}
	
		public long SearchFor16MRegion()
		{
			long baseAddress, regionSize;
			if (Get16MRegion(out baseAddress, out regionSize))
			{
				return baseAddress;	
			}
			
			return -1;		
		}
		
		public bool SearchForBytePattern(byte[] pattern, Action<byte[], int, int, long> found)
		{
			byte[] buffer = new byte[81920];
			long baseAddress, regionSize;
	
			if (Get16MRegion(out baseAddress, out regionSize))
			{
				long readPosition = baseAddress;
				int bytesToRead = Math.Min((int)regionSize, 1024 * 640); //scan first 640K only
				
				long bytesRead;
				while (bytesToRead > 0 && (bytesRead = Read(buffer, readPosition, Math.Min(buffer.Length, bytesToRead))) > 0)
				{
					//search bytes pattern
					for (int index = 0; index < bytesRead - pattern.Length + 1; index++)
					{
						if (IsMatch(buffer, pattern, index))
						{
							found(buffer, (int)bytesRead, index, readPosition + index);
						}
					}
					
					readPosition += bytesRead;
					bytesToRead -= (int)bytesRead;
				}
				
				return true;
			}
			
			return false;
		}
	
		public bool IsMatch(byte[] x, byte[] y, int index)
		{
			for (int j = 0; j < y.Length; j++)
				if (x[j + index] != y[j])
					return false;
			return true;
		}
	}
}