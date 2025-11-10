using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MoviePlayer
{
	public class Heap : IDisposable
	{
		[DllImport("kernel32.dll")]
		static extern IntPtr HeapCreate(uint flOptions, UIntPtr dwInitialSize, UIntPtr dwMaximumSize);

		[DllImport("kernel32.dll")]
		static extern IntPtr HeapAlloc(IntPtr hHeap, uint dwFlags, UIntPtr dwBytes);

		[DllImport("kernel32.dll")]
		static extern bool HeapFree(IntPtr hHeap, uint dwFlags, IntPtr lpMem);

		[DllImport("kernel32.dll")]
		static extern bool HeapDestroy(IntPtr hHeap);

		[DllImport("kernel32.dll")]
		static extern IntPtr HeapSize(IntPtr hHeap, uint dwFlags, IntPtr lpMem);

		[DllImport("Kernel32.dll", EntryPoint = "RtlZeroMemory")]
		static extern void ZeroMemory(IntPtr dest, IntPtr size);

		const uint HEAP_ZERO_MEMORY = 0x00000008;

		IntPtr hHeap, heapMemory;

		public Heap(uint size)
		{
			Debug.Assert(IntPtr.Size == 4); //x86 mode only

			hHeap = HeapCreate(0, 0, 0); //grow as needed
			if (hHeap == IntPtr.Zero)
			{
				throw new Exception("HeapCreate() failed");
			}

			heapMemory = HeapAlloc(hHeap, HEAP_ZERO_MEMORY, size);
			if (heapMemory == IntPtr.Zero)
			{
				throw new Exception("HeapAlloc() failed");
			}
		}

		public void Clear()
		{
			ZeroMemory(heapMemory, HeapSize(hHeap, 0, heapMemory));
		}

		public void Flush(byte[] memory)
		{
			Marshal.Copy(memory, 0, heapMemory, memory.Length); //write to heap memory
		}

		public void Dispose()
		{
			HeapFree(hHeap, 0, heapMemory);
			HeapDestroy(hHeap);
		}
	}
}
