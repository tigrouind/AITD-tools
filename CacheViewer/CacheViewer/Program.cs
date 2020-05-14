using System;
using System.Linq;
using System.Text;
using System.Threading;
using Shared;

namespace CacheViewer
{
	class Program
	{
		static ProcessMemoryReader memoryReader;
		static byte[] buffer = new byte[640 * 1024];
		static Cache[] cache;
		static long address;
		
		public static void Main(string[] args)
		{					
			if(args.Length == 0)
			{
				args = new [] { "ListSamp", "ListBod2", "ListAni2", "ListLife", "ListTrak" };
			}
			
			if(args.Length > 5)
			{
				args = args.Take(5).ToArray();
			}								
					
			cache = args.Select(x => new Cache
            {
            	Name = x, 
            	Pattern = Encoding.ASCII.GetBytes(x) 
            }).ToArray();
					
			int frame = 0;
			while (true)
			{			
				if(frame % 4 == 0)
				{
					if (memoryReader == null)
					{
						int processId = DosBox.SearchProcess();
						if(processId != -1)
						{
							memoryReader = new ProcessMemoryReader(processId);
							address = memoryReader.SearchFor16MRegion();			
							if(address == -1)
							{
								memoryReader.Close();
								memoryReader = null;
							}
						}
					}
					
					if (memoryReader != null && cache.Any(x => x.Address == -1))
					{
						SearchPatterns();		
					}
				}
						
				if (memoryReader != null)
				{
					ReadMemory();
					Render();
				}				
									
				Thread.Sleep(250);
				frame++;
			}
		}
		
		static void SearchPatterns()
		{			
			if(memoryReader.Read(buffer, address, buffer.Length) > 0) //640K
			{
				foreach(var block in DosBox.GetMCBs(buffer))
				{
					int position = block.Position;						
					foreach(var ch in cache)
					{	
						var pattern = ch.Pattern;
						if (pattern.SequenceEqual(buffer.Skip(position).Take(pattern.Length)))
						{
							ch.Address = address + position;		
						}
					}
				}
			}			
			else
			{
				memoryReader.Close();
				memoryReader = null;
			}
		}
		
		static void ReadMemory()
		{
			int ticks = Environment.TickCount;
			foreach(var ch in cache.Where(x => x.Address != -1))
			{
				bool readSuccess;
				
				if ((readSuccess = memoryReader.Read(buffer, ch.Address - 16, 4) > 0) &&
				    buffer.ReadUnsignedShort(1) != 0 && //block is still allocated
				    (readSuccess = memoryReader.Read(buffer, ch.Address, 4096) > 0) &&
				    ch.Pattern.SequenceEqual(buffer.Take(ch.Pattern.Length))) //pattern is matching
				{
					ch.MaxFreeData = buffer.ReadUnsignedShort(10);
					ch.SizeFreeData = buffer.ReadUnsignedShort(12);
					ch.NumMaxEntry = buffer.ReadUnsignedShort(14);
					ch.NumUsedEntry = Math.Min((int)buffer.ReadUnsignedShort(16), 100);
										
					for (int i = 0 ; i < ch.NumUsedEntry ; i++)
					{			
						int addr = 22 + i * 10;		
						int key = buffer.ReadUnsignedShort(addr);
						
						CacheEntry entry;
						if(!ch.Entries.TryGetValue(key, out entry))
						{
							entry = new CacheEntry();		
							entry.StartTicks = ticks;
							ch.Entries.Add(key, entry);
						}								
						
						entry.Key = key;
						entry.Id = i;
						entry.Size = buffer.ReadUnsignedShort(addr+4);									
						entry.Time = buffer.ReadInt(addr+6);
						
						entry.Touched = entry.Time != entry.LastTime;						
						entry.Added = (ticks - entry.StartTicks) < 3000;
						entry.LastTime = entry.Time;
						entry.Ticks = ticks;
					}
					
					foreach (int key in ch.Entries.Keys.ToArray())
					{
						var entry = ch.Entries[key];
						
						int removedSince = ticks - entry.Ticks;
						entry.Removed = removedSince > 0;						
						if (removedSince >= 3750)
						{
							ch.Entries.Remove(key);
						}
					}											
				}
				else if (readSuccess)
				{
					ch.Address = -1;
				}
				else
				{
					memoryReader.Close();
					memoryReader = null;
					return;
				}
			}
		}
		
		static void Render()
		{			
			Console.Clear();
				
			int column = 0;
			foreach(var ch in cache.Where(x => x.Address != -1))
			{
				Console.Write(column * 22 + 6, 0, ConsoleColor.Gray, "{0}", ch.Name);
				Console.Write(column * 22 + 0, 1, ConsoleColor.Gray, "{0,5:D}/{1,5:D} {2,3:D}/{3,3:D}", ch.MaxFreeData - ch.SizeFreeData, ch.MaxFreeData, ch.NumUsedEntry, ch.NumMaxEntry);
				
				int row = 0;
				foreach (var entry in ch.Entries.Values
				         .OrderByDescending(x => x.Time / 60)
				         .ThenByDescending(x => x.StartTicks)
				         .ThenByDescending(x => x.Id))
				{
					var color = ConsoleColor.Gray;								
						
					if (entry.Touched)
					{
						color = ConsoleColor.DarkYellow;
					}
					
					if (entry.Removed)
					{
						color = ConsoleColor.Black | ConsoleColor.BackgroundDarkGray;
					}
					
					if (entry.Added)
					{
						color = ConsoleColor.Black | ConsoleColor.BackgroundDarkGreen;
					}
					
					Console.Write(column * 22 + 1, row + 3, color, "{0,5:D} {1} {2,5:D}", entry.Key, FormatSize(entry.Size).PadLeft(6), entry.Time / 60);
					row++;
				}		
				
				column++;
			}
			
			Console.Flush();			
		}
	
		static string FormatSize(int length)
		{
			if (length > 1024)
			{
				return length/1024 + " K";
			}
			
			return length + " B";
		}
	}
}