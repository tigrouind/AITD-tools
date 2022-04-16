using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Shared;

namespace CacheViewer
{
	class Program
	{
		static ProcessMemory process;
		static readonly byte[] memory = new byte[640 * 1024];
		static readonly Dictionary<GameVersion, int[]> gameConfigs = new Dictionary<GameVersion, int[]>
		{
			// ListSamp / ListLife / ListBody / ListAnim / ListTrak / _MEMORY_ 
			{ GameVersion.AITD1,        new [] { 0x218CB, 0x218CF, 0x218D7, 0x218D3, 0x218C7, 0x218BF } },
			{ GameVersion.AITD1_FLOPPY, new [] { 0x2053E, 0x2049C, 0x20494, 0x20498, 0x204AA, 0x20538 } },
			{ GameVersion.AITD1_DEMO,   new [] { 0x20506, 0x20464, 0x2045C, 0x20460, 0x20472, 0x20500 } },
		};
		static int entryPoint = -1;
		static readonly VarParserExt varParser = new VarParserExt();
		static readonly Cache[] cache = { 
			new Cache(VarEnum.SOUNDS), 
			new Cache(VarEnum.LIFES), 
			new Cache(VarEnum.BODYS), 
			new Cache(VarEnum.ANIMS), 
			new Cache(VarEnum.TRACKS), 
			new Cache(VarEnum.NONE) 
		};
		static int[] gameConfig;
		static bool clearCache, showTimestamp;
		static int uniqueIndex;
		static CacheEntryComparer comparer = new CacheEntryComparer();

		public static void Main()
		{
			System.Console.Title = "AITD cache viewer";
			
			Directory.CreateDirectory("GAMEDATA");
			if (File.Exists("GAMEDATA/vars.txt"))
			{
				varParser.Load("GAMEDATA/vars.txt", VarEnum.SOUNDS, VarEnum.LIFES, VarEnum.BODYS, VarEnum.ANIMS, VarEnum.TRACKS);
			}
			
			while (true)
			{
				if (process == null)
				{
					SearchDosBox();
				}

				if (process != null && entryPoint == -1)
				{
					SearchEntryPoint();
				}

				if (process != null)
				{										
					ReadInput();
					ReadMemory();
					if (comparer.SortByTimestamp)
					{
						SortEntries();
					}
				}

				if (process != null)
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
				process = new ProcessMemory(processId);
				process.BaseAddress = process.SearchFor16MRegion();
				if (process.BaseAddress == -1)
				{
					CloseReader();
				}
			}
		}

		static void SearchEntryPoint()
		{
			if (process.Read(memory, 0, memory.Length) > 0 && 
			     DosBox.GetExeEntryPoint(memory, out entryPoint))
			{						
				//check if CDROM/floppy version
				byte[] cdPattern = Encoding.ASCII.GetBytes("CD Not Found");
				var gameVersion = Shared.Tools.IndexOf(memory, cdPattern) != -1 ? GameVersion.AITD1 : GameVersion.AITD1_FLOPPY;				
				if (gameVersion == GameVersion.AITD1_FLOPPY) 
				{
					if (Shared.Tools.IndexOf(memory, Encoding.ASCII.GetBytes("USA.PAK")) != -1)
					{
						gameVersion = GameVersion.AITD1_DEMO;
					}
				}	

				gameConfig = gameConfigs[gameVersion];	
			} 
			else
			{
				CloseReader();
			}
		}
		
		static void SortEntries()
		{
			for(int i = 0 ; i < cache.Length ; i++)
			{
				var ch = cache[i];		
				if (ch.Name != "_MEMORY_")
				{				
					Tools.InsertionSort(ch.Entries, comparer);
				}
			}
		}
		
		static void ReadInput()
		{
			switch (ReadKey().Key)
			{
				case ConsoleKey.F5:
					clearCache = true;
					break;
					
				case ConsoleKey.Spacebar:
					showTimestamp = !showTimestamp;
					break;
					
				case ConsoleKey.S:
					comparer.SortByTimestamp = !comparer.SortByTimestamp;
					SortEntries();
					break;
			}
		}

		static void ReadMemory()
		{
			int ticks = Environment.TickCount;
			bool readSuccess = true;									
			for(int i = 0 ; i < cache.Length ; i++)
			{
				var ch = cache[i];
				if (readSuccess &= (process.Read(memory, entryPoint + gameConfig[i], 4) > 0))
				{
					int cachePointer = memory.ReadFarPointer(0);	
					if(cachePointer != 0 && (readSuccess &= (process.Read(memory, cachePointer - 16, 4096) > 0))) 
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
			
			clearCache = false; 
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
					entry.Index = uniqueIndex++;
					
					if (comparer.SortByTimestamp)
					{
						ch.Entries.AddFirst(entry);
					}
					else
					{
						ch.Entries.AddLast(entry);
					}
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
			process.Write(memory, cachePointer + 12, 2); //size free data 
			
			memory.Write(0, 0); 
			process.Write(memory, cachePointer + 16, 2); //num used entries
			
			ch.SizeFreeData = ch.MaxFreeData;
			ch.NumUsedEntry = 0;			
			ch.Entries.Clear();
		}
				
		static void CloseReader()
		{
			entryPoint = -1;
			process.Close();
			process = null;
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

						int entrySize = entry.Size;
						if (entrySize >= 1000 && entrySize < 1024) entrySize = 1024;
						bool kilobyte = entrySize >= 1024;
						
						TimeSpan time = TimeSpan.FromSeconds(entry.Time / 60);
						
						Console.SetCursorPosition(column * 19, row + 4);	
						Console.Write(showTimestamp ? "{0,3} {2:D2}:{3:D2}.{4:D2} {5,3} {6}" : "{0,3} {1,-8} {5,3} {6}",
							entry.Id, 
							varParser.GetText(ch.Section, entry.Id), 
							time.Minutes, time.Seconds, entry.Time % 60,
							kilobyte ? entrySize / 1024 : entrySize, kilobyte ? 'K' : 'B');
						row++;
					}
				}

				column++;
			}

			Console.Flush();
		}
	}
}