﻿using System.Runtime.InteropServices;
using System;
using System.Linq;
using System.Text;

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
		
		public long BaseAddress;

		public ProcessMemoryReader(int processId)
		{
			this.processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, processId);
		}

		~ProcessMemoryReader()
		{
			Close();
		}
		
		public unsafe long Read(byte[] buffer, long address, int count, int offset = 0)
		{
			if((offset + count) > buffer.Length)
			{
				throw new ArgumentOutOfRangeException();
			}
			
			IntPtr bytesRead;
			fixed (byte* ptr = buffer)
			{				
				if (ReadProcessMemory(processHandle, new IntPtr(BaseAddress + address), new IntPtr((void*)(ptr + offset)), count, out bytesRead))
				{
					return (long)bytesRead;
				}
			}
			return 0;
		}

		public long Write(byte[] buffer, long offset, int count)
		{
			IntPtr bytesWritten;
			if (WriteProcessMemory(processHandle, new IntPtr(BaseAddress + offset), buffer, count, out bytesWritten))
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

		public long SearchFor16MRegion()
		{
			MEMORY_BASIC_INFORMATION mem_info = new MEMORY_BASIC_INFORMATION();

			long min_address = 0;
			long max_address = 0x7FFFFFFF;
			byte[] memory = new byte[4096];

			//scan process memory regions
			while (min_address < max_address
				&& VirtualQueryEx(processHandle, (IntPtr)min_address, out mem_info, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) > 0)
			{
				//check if memory region is accessible
				//skip regions smaller than 16M (default DOSBOX memory size)
				if (mem_info.Protect == PAGE_READWRITE && mem_info.State == MEM_COMMIT && (mem_info.Type & MEM_PRIVATE) == MEM_PRIVATE
					&& (int)mem_info.RegionSize >= 1024 * 1024 * 16 
					&& Read(memory, (long)mem_info.BaseAddress, memory.Length) > 0
					&& Tools.IndexOf(memory, Encoding.ASCII.GetBytes("CON ")) != -1)
				{
					return (long)mem_info.BaseAddress + 32; //skip Windows 32-bytes memory allocation header
				}

				// move to next memory region
				min_address = (long)mem_info.BaseAddress + (long)mem_info.RegionSize;
			}

			return -1;
		}
	}
}