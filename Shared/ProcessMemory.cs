using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using System.Text;

namespace Shared
{
	public class ProcessMemory(int processId)
	{
		const uint PROCESS_QUERY_INFORMATION = 0x0400;
		const uint PROCESS_VM_READ = 0x0010;
		const uint PROCESS_VM_WRITE = 0x0020;
		const uint PROCESS_VM_OPERATION = 0x0008;
		const uint MEM_COMMIT = 0x00001000;
		const uint MEM_PRIVATE = 0x20000;
		const uint MEM_IMAGE = 0x1000000;
		const uint PAGE_READWRITE = 0x04;

		[StructLayout(LayoutKind.Sequential)]
		struct MEMORY_BASIC_INFORMATION
		{
			public IntPtr BaseAddress;
			public IntPtr AllocationBase;
			public uint AllocationProtect;
			public IntPtr RegionSize;
			public uint State;
			public uint Protect;
			public uint Type;
		}

		[DllImport("kernel32.dll")]
		static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

		[DllImport("kernel32.dll")]
		static extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32.dll")]
		static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

		[DllImport("kernel32.dll")]
		static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

		[DllImport("kernel32.dll")]
		static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);

		[DllImport("kernel32.dll")]
		static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

		IntPtr processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, processId);

		public long BaseAddress;

		~ProcessMemory()
		{
			Close();
		}

		public unsafe long Read(byte[] buffer, long address, int count, int offset = 0)
		{
			if ((offset + count) > buffer.Length)
			{
				throw new ArgumentOutOfRangeException();
			}

			fixed (byte* ptr = buffer)
			{
				if (ReadProcessMemory(processHandle, new IntPtr(BaseAddress + address), new IntPtr(ptr + offset), count, out IntPtr bytesRead))
				{
					return bytesRead;
				}
			}
			return 0;
		}

		public long Write(byte[] buffer, long offset, int count)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length);

			if (WriteProcessMemory(processHandle, new IntPtr(BaseAddress + offset), buffer, count, out IntPtr bytesWritten))
			{
				return bytesWritten;
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
			byte[] memory = new byte[4096];

			//scan process memory regions
			foreach (var mem_info in GetMemoryRegions())
			{
				//check if memory region is accessible
				//skip regions smaller than 16M (default DOSBOX memory size)
				if (mem_info.Protect == PAGE_READWRITE && mem_info.State == MEM_COMMIT && (mem_info.Type & MEM_PRIVATE) == MEM_PRIVATE
					&& (int)mem_info.RegionSize >= 1024 * 1024 * 16
					&& Read(memory, mem_info.BaseAddress, memory.Length) > 0
					&& Tools.IndexOf(memory, Encoding.ASCII.GetBytes("CON ")) != -1)
				{
					return (long)mem_info.BaseAddress + 32; //skip Windows 32-bytes memory allocation header
				}
			}

			return -1;
		}

		IEnumerable<MEMORY_BASIC_INFORMATION> GetMemoryRegions(long min_address = 0, long max_address = 0x7FFFFFFF)
		{
			//scan process memory regions
			while (min_address < max_address
				&& VirtualQueryEx(processHandle, (IntPtr)min_address, out MEMORY_BASIC_INFORMATION mem_info, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) > 0)
			{
				yield return mem_info;

				// move to next memory region
				min_address = mem_info.BaseAddress + (long)mem_info.RegionSize;
			}
		}

		public int SearchForBytePattern(Func<byte[], int> searchFunction)
		{
			byte[] buffer = new byte[81920];

			//scan process memory regions
			foreach (var mem_info in GetMemoryRegions(0, 0x0FFFFFFF))
			{
				if (mem_info.Protect == PAGE_READWRITE && mem_info.State == MEM_COMMIT && (mem_info.Type & MEM_IMAGE) == MEM_IMAGE)
				{
					long readPosition = mem_info.BaseAddress;
					int bytesToRead = (int)mem_info.RegionSize;

					while (bytesToRead > 0 && ReadProcessMemory(processHandle, new IntPtr(readPosition), buffer, Math.Min(buffer.Length, bytesToRead), out IntPtr bytesRead))
					{
						//search bytes pattern
						int index = searchFunction(buffer);
						if (index != -1)
						{
							return (int)((readPosition + index) - BaseAddress);
						}

						readPosition += (int)bytesRead;
						bytesToRead -= (int)bytesRead;
					}
				}
			}

			return -1;
		}
	}
}