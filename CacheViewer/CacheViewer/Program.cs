using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Shared;

namespace CacheViewer
{
	class Program
	{
		static ProcessMemoryReader memoryReader;
		static byte[] buffer = new byte[4096];
		static Cache[] cache;
		static int frame;
		
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
								
			while (true)
			{			
				if (memoryReader == null)
				{
					memoryReader = ProcessMemoryReader.SearchDosBox();
				}
				
				if (memoryReader != null && cache.Any(x => x.Address < 0))
				{
					SearchPatterns();						
				}
						
				if (memoryReader != null && cache.All(x => x.Address >= 0))
				{
					ReadMemory();
					
					if (cache.All(x => x.Address >= 0)) 
					{
						Render();
					}

					Thread.Sleep(250);
					frame++;
				}											
				else
				{
					Thread.Sleep(1000);	
			    }
			}
		}
		
		static void SearchPatterns()
		{
			var listPattern = Encoding.ASCII.GetBytes("List");
			if(!memoryReader.SearchForBytePattern(listPattern, (buf, len, index, readPosition) => 
            {
              	foreach(var ch in cache)
				{
					var pat = ch.Pattern;						
					if (index < (len - pat.Length + 1) && memoryReader.IsMatch(buf, pat, index) && !IsString(buf, len, index + pat.Length))
					{
						ch.Address = readPosition;		
					}
				}                                        	                   		
      		}))
			{
				memoryReader.Close();
				memoryReader = null;
			}
		}
		
		static void ReadMemory()
		{
			foreach(var ch in cache)
			{
				if (memoryReader.Read(buffer, ch.Address, 4096) != 0 && 
				    memoryReader.IsMatch(buffer, ch.Pattern, 0))
				{
					ch.MaxFreeData = buffer.ReadUnsignedShort(10);
					ch.SizeFreeData = buffer.ReadUnsignedShort(12);
					ch.NumMaxEntry = buffer.ReadUnsignedShort(14);
					ch.NumUsedEntry =  Math.Min((int)buffer.ReadUnsignedShort(16), 100);
					
					for (int i = 0 ; i < ch.NumUsedEntry ; i++)
					{			
						int addr = 22 + i * 10;		
						int key = buffer.ReadUnsignedShort(addr);
						
						CacheEntry entry;
						if(!ch.Entries.TryGetValue(key, out entry))
						{
							entry = new CacheEntry();		
							entry.FrameStart = frame;
							ch.Entries.Add(key, entry);
						}								
						
						entry.Key = key;
						entry.Id = i;
						entry.Size = buffer.ReadUnsignedShort(addr+4);									
						entry.Time = buffer.ReadInt(addr+6);
						entry.Touched = entry.Time != entry.LastTime;
						entry.LastTime = entry.Time;
						entry.Frame = frame;
					}
					
					foreach (int key in ch.Entries.Keys.ToArray())
					{
						var entry = ch.Entries[key];
						if ((frame - entry.Frame) >= 15)
						{
							ch.Entries.Remove(key);
						}
					}											
				}
				else
				{
					ch.Address = -1;
				}
			}
		}
		
		static void Render()
		{			
			Console.Clear();
				
			int column = 0;
			foreach(var ch in cache)
			{
				Console.Write(column * 22 + 6, 0, ConsoleColor.Gray, "{0}", ch.Name);
				Console.Write(column * 22 + 0, 1, ConsoleColor.Gray, "{0,5:D}/{1,5:D} {2,3:D}/{3,3:D}", ch.MaxFreeData - ch.SizeFreeData, ch.MaxFreeData, ch.NumUsedEntry, ch.NumMaxEntry);
				
				//Array.Sort(entries, 0, numUsedEntry, comparer);
				int row = 0;
				foreach (var entry in ch.Entries.Values
				         .OrderByDescending(x => x.Time)
				         .ThenByDescending(x => x.Id))
				{
					var color = ConsoleColor.Gray;		
						
					if (entry.Touched)
					{
						color = ConsoleColor.DarkYellow;
					}
					
					if (frame - entry.Frame > 0) //removed
					{
						color = ConsoleColor.Black | ConsoleColor.BackgroundDarkGray;
					}
					
					if (frame - entry.FrameStart < 12) //added
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
		
		static bool IsString(byte[] buf, int length, int index)
		{
			return (index+0) < length && char.IsLetter((char)buf[index+1])
				&& (index+1) < length && char.IsLetter((char)buf[index+2])
				&& (index+2) < length && char.IsLetter((char)buf[index+3]);
		}
	}
}