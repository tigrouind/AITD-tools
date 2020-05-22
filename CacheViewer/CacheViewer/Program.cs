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
		static byte[] buffer = new byte[640 * 1024];
		static Cache[] cache;
		static long address;
		static CacheEntryComparer comparer = new CacheEntryComparer();

		public static void Main(string[] args)
		{
			System.Console.Title = "AITD cache viewer";

			if (args.Length == 0)
			{
				args = new [] { "ListSamp", "ListBod2", "ListAni2", "ListLife", "ListTrak", "_MEMORY_" };
			}

			if (args.Length > 6)
			{
				args = args.Take(6).ToArray();
			}

			cache = args.Select(x => new Cache
			{
				Name = x,
				Pattern = Encoding.ASCII.GetBytes(x)
			}).ToArray();

			while (true)
			{
				if (processReader == null)
				{
					SearchDosBox();
				}

				if (processReader != null && cache.Any(x => x.Address == -1))
				{
					SearchPatterns();
				}

				if (processReader != null)
				{
					ReadMemory();
				}

				if (processReader != null)
				{
					Render();
					Thread.Sleep(250);
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
				address = processReader.SearchFor16MRegion();
				if (address == -1)
				{
					CloseReader();
				}
			}
		}

		static void SearchPatterns()
		{
			if (processReader.Read(buffer, address + 32, buffer.Length) > 0) //640K
			{
				foreach(var block in DosBox.GetMCBs(buffer)
						.Where(x => x.Owner != 0 && x.Owner != 8)) //free or owned by DOS
				{
					int position = block.Position;
					foreach(var ch in cache)
					{
						var pattern = ch.Pattern;
						if (buffer.IsMatch(pattern, position))
						{
							ch.Address = address + 32 + position;
						}
					}
				}
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
			foreach (var ch in cache.Where(x => x.Address != -1))
			{
				if ((readSuccess = processReader.Read(buffer, ch.Address - 16, 4096) > 0) &&
					buffer.ReadUnsignedShort(1) != 0 && //block is still allocated
					buffer.IsMatch(ch.Pattern, 16)) //pattern is still matching
				{
					const int offset = 16;
					ch.MaxFreeData = buffer.ReadUnsignedShort(offset + 10);
					ch.SizeFreeData = buffer.ReadUnsignedShort(offset + 12);
					ch.NumMaxEntry = buffer.ReadUnsignedShort(offset + 14);
					ch.NumUsedEntry = Math.Min((int)buffer.ReadUnsignedShort(offset + 16), 100);

					for (int i = 0 ; i < ch.NumUsedEntry ; i++)
					{
						int addr = 22 + i * 10 + offset;
						int id = buffer.ReadUnsignedShort(addr);

						CacheEntry entry = ch.Entries.Find(x => x.Id == id);
						if (entry == null)
						{
							entry = new CacheEntry();
							entry.StartTicks = ticks;
							ch.Entries.Insert(0, entry);
						}

						entry.Id = id;
						entry.Size = buffer.ReadUnsignedShort(addr+4);
						entry.Ticks = ticks;

						if (ch.Name != "_MEMORY_")
						{
							entry.Time = buffer.ReadUnsignedInt(addr+6);
							entry.TimePerSecond = entry.Time / 60;

							if (entry.Time != entry.LastTime)
							{
								entry.TouchedTicks = ticks;
								entry.LastTime = entry.Time;
							}
						}
					}

					ch.Entries.RemoveAll(x => (ticks - x.Ticks) > 3000);
					foreach (var entry in ch.Entries)
					{
						entry.Touched = (ticks - entry.TouchedTicks) < 1000;
						entry.Added = (ticks - entry.StartTicks) < 3000;
						entry.Removed = (ticks - entry.Ticks) > 0;
					}

					ch.Entries.InsertionSort(comparer);
				}
				else if (readSuccess)
				{
					ch.Address = -1;
				}
			}

			if (!readSuccess)
			{
				CloseReader();
			}
		}

		static void CloseReader()
		{
			foreach (var ch in cache)
			{
				ch.Address = -1;
			}

			processReader.Close();
			processReader = null;
		}

		static void Render()
		{
			Console.Clear();

			int column = 0;
			foreach (var ch in cache)
			{
				if (ch.Address != -1)
				{
					Console.Write(column * 20 + 6, 0, ConsoleColor.Gray, ch.Name);
					Console.Write(column * 20 + 0, 1, ConsoleColor.Gray, "{0,5:D}/{1,5:D} {2,3:D}/{3,3:D}",
									ch.MaxFreeData - ch.SizeFreeData, ch.MaxFreeData, ch.NumUsedEntry, ch.NumMaxEntry);

					int row = 0;
					foreach (var entry in ch.Entries)
					{
						var color = ConsoleColor.Gray;

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

						Console.Write(column * 20, row + 3, color, "{0,6:D} {1,6} {2,5:D}", entry.Id, FormatSize(entry.Size), entry.Time / 60);
						row++;
					}
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