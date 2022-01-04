using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Shared;

namespace CacheViewer
{
	class Program
	{
		static ProcessMemoryReader reader;
		static readonly byte[] memory = new byte[640 * 1024];
		static readonly Dictionary<GameVersion, int[]> gameConfigs = new Dictionary<GameVersion, int[]>
		{
			// ListSamp / ListLife / ListBody / ListAnim / ListTrak / _MEMORY_ 
			{ GameVersion.CD_ROM, new [] { 0x218CB, 0x218CF, 0x218D7, 0x218D3, 0x218C7, 0x218BF } },
			{ GameVersion.FLOPPY, new [] { 0x2053E, 0x2049C, 0x20494, 0x20498, 0x204AA, 0x20538 } },
			{ GameVersion.DEMO,   new [] { 0x20506, 0x20464, 0x2045C, 0x20460, 0x20472, 0x20500 } },
		};
		static int entryPoint = -1;
		static readonly Cache[] cache = { new Cache(), new Cache(), new Cache(), new Cache(), new Cache(), new Cache() };
		static int[] gameConfig;

		public static void Main()
		{
			System.Console.Title = "AITD cache viewer";
			
			while (true)
			{
				if (reader == null)
				{
					SearchDosBox();
				}

				if (reader != null && entryPoint == -1)
				{
					SearchEntryPoint();
				}

				if (reader != null)
				{
					ReadMemory();
				}

				if (reader != null)
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
				reader = new ProcessMemoryReader(processId);
				reader.BaseAddress = reader.SearchFor16MRegion();
				if (reader.BaseAddress == -1)
				{
					CloseReader();
				}
			}
		}

		static void SearchEntryPoint()
		{
			if (reader.Read(memory, 0, memory.Length) > 0 && 
			     DosBox.GetExeEntryPoint(memory, out entryPoint))
			{						
				//check if CDROM/floppy version
				byte[] cdPattern = Encoding.ASCII.GetBytes("CD Not Found");
				var gameVersion = Shared.Tools.IndexOf(memory, cdPattern) != -1 ? GameVersion.CD_ROM : GameVersion.FLOPPY;				
				if (gameVersion == GameVersion.FLOPPY) 
				{
					if (Shared.Tools.IndexOf(memory, Encoding.ASCII.GetBytes("USA.PAK")) != -1)
					{
						gameVersion = GameVersion.DEMO;
					}
				}	

				gameConfig = gameConfigs[gameVersion];	
			} 
			else
			{
				CloseReader();
			}
		}

		static void ReadMemory()
		{
			bool readSuccess = true;
			bool clearCache = ReadKey().Key == ConsoleKey.F5;
						
			int ticks = Environment.TickCount;
			for(int i = 0 ; i < cache.Length ; i++)
			{
				var ch = cache[i];
				if (readSuccess &= (reader.Read(memory, entryPoint + gameConfig[i], 4) > 0))
				{
					int cachePointer = memory.ReadFarPointer(0);	
					if(cachePointer != 0 && (readSuccess &= (reader.Read(memory, cachePointer - 16, 4096) > 0))) 
					{
						DosMCB block = DosBox.ReadMCB(memory, 0);
						if((block.Tag == 0x4D || block.Tag == 0x5A) && block.Owner != 0 && block.Size < 4096) //block is still allocated
						{
							UpdateCache(ch, ticks, 16);
							UpdateEntries(ch, ticks);
							
							if(clearCache) 
							{		
								ClearCache(ch, cachePointer);
							}
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
		
		static ConsoleKeyInfo ReadKey()
		{
			if (System.Console.KeyAvailable)
			{
				return System.Console.ReadKey(true);
			}
			
			return default(ConsoleKeyInfo);
		}
		
		static void UpdateCache(Cache ch, int ticks, int offset)
		{
			if (!Tools.StringEquals(memory, offset, 8, ch.Name))
			{
				ch.Name = Encoding.ASCII.GetString(memory, offset, 8);
			}
			
			ch.MaxFreeData = memory.ReadUnsignedShort(offset + 10);
			ch.SizeFreeData = memory.ReadUnsignedShort(offset + 12);
			ch.NumMaxEntry = memory.ReadUnsignedShort(offset + 14);
			ch.NumUsedEntry = memory.ReadUnsignedShort(offset + 16);
			
			for (int i = 0 ; i < Math.Min(ch.NumUsedEntry, 100) ; i++)
			{
				int addr = 22 + i * 10 + offset;
				int id = memory.ReadUnsignedShort(addr);

				//search entry
				CacheEntry entry = null;
				for (var node = ch.Entries.First; node != null; node = node.Next)
				{
					if (node.Value.Id == id)
					{
						entry = node.Value;
						break;
					}
				}
				
				if (entry == null)
				{
					entry = new CacheEntry();
					entry.Id = id;
					entry.StartTicks = ticks;
					ch.Entries.AddLast(entry);
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
		}
		
		static void UpdateEntries(Cache ch, int ticks)
		{
			var node = ch.Entries.First;
			while (node != null)
			{
				var next = node.Next;
				var entry = node.Value;
				if (Shared.Tools.GetTimeSpan(ticks, entry.Ticks) > TimeSpan.FromSeconds(3))
				{
					ch.Entries.Remove(node);
				}
				else
				{
					entry.Touched = Shared.Tools.GetTimeSpan(ticks, entry.TouchedTicks) < TimeSpan.FromSeconds(2);
					entry.Added = Shared.Tools.GetTimeSpan(ticks, entry.StartTicks) < TimeSpan.FromSeconds(3);
					entry.Removed = Shared.Tools.GetTimeSpan(ticks, entry.Ticks) > TimeSpan.Zero;				
				}				
				node = next;
			}
		}
		
		static void ClearCache(Cache ch, int cachePointer)
		{
			memory.Write((ushort)ch.MaxFreeData, 0);
			reader.Write(memory, cachePointer + 12, 2); //size free data 
			
			memory.Write(0, 0); 
			reader.Write(memory, cachePointer + 16, 2); //num used entries
			
			ch.SizeFreeData = ch.MaxFreeData;
			ch.NumUsedEntry = 0;			
			ch.Entries.Clear();
		}
				
		static void CloseReader()
		{
			entryPoint = -1;
			reader.Close();
			reader = null;
		}
		
		static void WriteStats(int done, int total)
		{				
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.Write("{0,5}/{1,5}", done, total);		
		
			Console.CursorLeft++;
			Console.ForegroundColor = ConsoleColor.DarkGray;			
			int value = Tools.RoundToNearest(done * 6, total);
			for(int i = 0 ; i < 6 ; i++) 
			{
				Console.Write(i < value ? '▓' : '░');
			}
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
					Console.ForegroundColor = ConsoleColor.Gray;
					Console.BackgroundColor = ConsoleColor.Black;
					
					Console.SetCursorPosition(column * 19 + 5, 0);						
					Console.Write(ch.Name);
										
					Console.SetCursorPosition(column * 19, 1);					
					WriteStats(ch.MaxFreeData - ch.SizeFreeData, ch.MaxFreeData);
															
					Console.SetCursorPosition(column * 19, 2);
					WriteStats(ch.NumUsedEntry, ch.NumMaxEntry);

					int row = 0;
					for (var node = ch.Entries.First; node != null; node = node.Next)
					{
						var entry = node.Value;

						if (entry.Removed)
						{
							Console.ForegroundColor = ConsoleColor.Black;
							Console.BackgroundColor = ConsoleColor.DarkGray;
						}
						else if (entry.Added)
						{
							Console.ForegroundColor = ConsoleColor.Black;
							Console.BackgroundColor = ConsoleColor.DarkGreen;
						}
						else if (entry.Touched)
						{
							Console.ForegroundColor = ConsoleColor.DarkYellow;
							Console.BackgroundColor = ConsoleColor.Black;
						}
						else
						{
							Console.ForegroundColor = ConsoleColor.DarkGray;
							Console.BackgroundColor = ConsoleColor.Black;
						}					

						bool kilobyte = entry.Size > 1024;
						Console.SetCursorPosition(column * 19, row + 4);
						Console.Write("{0,5} {1,4} {2} {3,5}", entry.Id, kilobyte ? entry.Size / 1024 : entry.Size, kilobyte ? 'K' : 'B', entry.Time / 60);
						row++;
					}
				}

				column++;
			}

			Console.Flush();
		}
	}
}