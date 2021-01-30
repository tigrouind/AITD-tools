using System;
using System.Linq;
using System.Text;
using System.Threading;
using Shared;

namespace CacheViewer
{
	class Program
	{
		static ProcessMemoryReader processReader;
		static byte[] memory = new byte[640 * 1024];
		static Cache[] cache;
		static long memoryAddress;
		static int entryPoint = -1;
		static int gameVersion;

		public static void Main()
		{
			System.Console.Title = "AITD cache viewer";

			cache = new []
			{
				new Cache { Address = new [] { 0x218CB, 0x2053E } }, // ListSamp
				new Cache { Address = new [] { 0x218CF, 0x2049C } }, // ListLife
				new Cache { Address = new [] { 0x218D7, 0x20494 } }, // ListBody/ListBod2
				new Cache { Address = new [] { 0x218D3, 0x20498 } }, // ListAnim/ListAni2
				new Cache { Address = new [] { 0x218C7, 0x204AA } }, // ListTrak
				new Cache { Address = new [] { 0x218BF, 0x20538 } }  // _MEMORY_
			};

			while (true)
			{
				if (processReader == null)
				{
					SearchDosBox();
				}

				if (processReader != null && entryPoint == -1)
				{
					SearchEntryPoint();
				}

				if (processReader != null)
				{
					ReadMemory();
				}

				if (processReader != null)
				{
					Render();
					Thread.Sleep(15);
				}
				else
				{
					Thread.Sleep(1000);
				}
			}
		}

		static void SearchDosBox()
		{
			int processId = DosBox.SearchProcess();
			if (processId != -1)
			{
				processReader = new ProcessMemoryReader(processId);
				memoryAddress = processReader.SearchFor16MRegion();
				if (memoryAddress == -1)
				{
					CloseReader();
				}
			}
		}

		static void SearchEntryPoint()
		{
			if (processReader.Read(memory, memoryAddress, memory.Length) > 0 && 
			     DosBox.GetExeEntryPoint(memory, out entryPoint))
			{						
				//check if CDROM/floppy version
				byte[] cdPattern = Encoding.ASCII.GetBytes("CD Not Found");
				gameVersion = Tools.IndexOf(memory, cdPattern) != -1 ? 0 : 1;
			} 
			else
			{
				CloseReader();
			}
		}

		static void ReadMemory()
		{
			bool readSuccess = true;

			int ticks = Environment.TickCount;
			foreach (var ch in cache)
			{
				if (readSuccess &= (processReader.Read(memory, memoryAddress + entryPoint + ch.Address[gameVersion], 4) > 0))
				{
					int cachePointer = memory.ReadFarPointer(0);	
					if(cachePointer != 0 && (readSuccess &= (processReader.Read(memory, memoryAddress + cachePointer - 16, 4096) > 0))) 
					{
						DosMCB block = DosBox.ReadMCB(memory, 0);
						if((block.Tag == 0x4D || block.Tag == 0x5A) && block.Owner != 0 && block.Size < 4096) //block is still allocated
						{
							UpdateCache(ch, ticks, 16);
						}
						else
						{
							ch.Name = null;
						}
					}
					else
					{
						ch.Name = null;
					}
				}
			}

			if (!readSuccess)
			{
				CloseReader();
			}
		}
		
		static void UpdateCache(Cache ch, int ticks, int offset)
		{
			if(ch.Name == null) ch.Name = Encoding.ASCII.GetString(memory, offset, 8);
			ch.MaxFreeData = memory.ReadUnsignedShort(offset + 10);
			ch.SizeFreeData = memory.ReadUnsignedShort(offset + 12);
			ch.NumMaxEntry = memory.ReadUnsignedShort(offset + 14);
			ch.NumUsedEntry = memory.ReadUnsignedShort(offset + 16);
			
			for (int i = 0 ; i < Math.Min(ch.NumUsedEntry, 100) ; i++)
			{
				int addr = 22 + i * 10 + offset;
				int id = memory.ReadUnsignedShort(addr);

				//find entry
				CacheEntry entry = null;
				for(int j = 0 ; j < ch.Entries.Count ; j++)
				{
					if (ch.Entries[j].Id == id)
					{
						entry = ch.Entries[j];
						break;
					}
				}
				
				if (entry == null)
				{
					entry = new CacheEntry();
					entry.Id = id;
					entry.StartTicks = ticks;
					ch.Entries.Add(entry);
				}
				else if (entry.Removed)
				{
					entry.StartTicks = ticks;
				}
				
				entry.Size = memory.ReadUnsignedShort(addr+4);
				entry.Ticks = ticks;
								
				if (ch.Name != "_MEMORY_")
				{
					entry.Time = memory.ReadUnsignedInt(addr+6);

					if (entry.Time != entry.LastTime)
					{
						entry.TouchedTicks = ticks;
						entry.LastTime = entry.Time;
					}
				}
			}
			
			bool needRemove = false;
			for(int i = 0 ; i < ch.Entries.Count ; i++)
			{
				var entry = ch.Entries[i];
				entry.Touched = (ticks - entry.TouchedTicks) < 2000;
				entry.Added = (ticks - entry.StartTicks) < 3000;
				entry.Removed = (ticks - entry.Ticks) > 0;				
				needRemove |= (ticks - entry.Ticks) > 3000;
			}
		
			if (needRemove)
			{
				RemoveEntries(ch, ticks);
			}
		}
		
		static void RemoveEntries(Cache ch, int ticks)
		{
			ch.Entries.RemoveAll(x => (ticks - x.Ticks) > 3000);
		}

		static void CloseReader()
		{
			entryPoint = -1;
			processReader.Close();
			processReader = null;
		}

		static void Render()
		{
			Console.Clear();

			int column = 0;
			for(int i = 0 ; i < cache.Length ; i++)
			{
				var ch = cache[i];
				if (ch.Name != null)
				{
					Console.Write(column * 20 + 6, 0, ConsoleColor.Gray, ch.Name);
					Console.Write(column * 20 + 0, 1, ConsoleColor.Gray, "{0,5:D}/{1,5:D} {2,3:D}/{3,3:D}",
									ch.MaxFreeData - ch.SizeFreeData, ch.MaxFreeData, ch.NumUsedEntry, ch.NumMaxEntry);

					int row = 0;
					for(int j = 0 ; j < ch.Entries.Count ; j++)
					{
						var entry = ch.Entries[j];
						var color = ConsoleColor.DarkGray;

						if (entry.Touched)
						{
							color = ConsoleColor.DarkYellow;
						}

						if (entry.Added)
						{
							color = ConsoleColor.Black | ConsoleColor.BackgroundDarkGreen;
						}

						if (entry.Removed)
						{
							color = ConsoleColor.Black | ConsoleColor.BackgroundDarkGray;
						}

						Console.Write(column * 20, row + 3, color, "{0,6:D} {1,6:S} {2,5:D}", entry.Id, entry.Size, (int)(entry.Time / 60));
						row++;
					}
				}

				column++;
			}

			Console.Flush();
		}
	}
}