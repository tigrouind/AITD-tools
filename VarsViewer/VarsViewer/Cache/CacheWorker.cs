using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VarsViewer
{
	public class CacheWorker : IWorker
	{
		bool IWorker.UseMouse => false;

		int[] gameConfig => gameConfigs[Program.GameVersion];
		readonly Dictionary<GameVersion, int[]> gameConfigs = new Dictionary<GameVersion, int[]>
		{
			// ListSamp / ListLife / ListBody / ListAnim / ListTrak / ListMus / _MEMORY_
			{ GameVersion.AITD1,        new [] { 0x218CB, 0x218CF, 0x218D7, 0x218D3, 0x218C7, 0x218C3, 0x218BF } },
			{ GameVersion.AITD1_FLOPPY, new [] { 0x2053E, 0x2049C, 0x20494, 0x20498, 0x204AA, 0x204AE, 0x20538 } },
			{ GameVersion.AITD1_DEMO,   new [] { 0x20506, 0x20464, 0x2045C, 0x20460, 0x20472, 0x20476, 0x20500 } },
		};

		IEnumerable<Cache> cache => Program.GameVersion == GameVersion.AITD1 ? cacheConfig.Where(x => x.Section != VarEnum.MUSIC) : cacheConfig;
		readonly Cache[] cacheConfig = {
			new Cache(0, VarEnum.SOUNDS),
			new Cache(1, VarEnum.LIFES),
			new Cache(2, VarEnum.BODYS),
			new Cache(3, VarEnum.ANIMS),
			new Cache(4, VarEnum.TRACKS),
			new Cache(5, VarEnum.MUSIC),
			new Cache(6, VarEnum.NONE)
		};
		bool clearCache, showTimestamp;
		int uniqueId;

		bool IWorker.ReadMemory()
		{
			int ticks = Environment.TickCount;
			bool readSuccess = true;
			foreach (var ch in cache)
			{
				if (readSuccess &= Program.Process.Read(Program.Memory, Program.EntryPoint + gameConfig[ch.Index], 4) > 0)
				{
					int cachePointer = Program.Memory.ReadFarPointer(0);
					if (cachePointer != 0 && (readSuccess &= Program.Process.Read(Program.Memory, cachePointer - 16, 4096) > 0))
					{
						DosMCB block = DosBox.ReadMCB(Program.Memory, 0);
						if ((block.Tag == 0x4D || block.Tag == 0x5A) && block.Owner != 0 && block.Size < 4096) //block is still allocated
						{
							UpdateCache(ch, 16);
							UpdateEntries(ch);

							if (clearCache)
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
				return false;
			}

			if (Sort.SortMode != SortMode.Default)
			{
				Sort.SortEntries(cache);
			}

			return true;

			void UpdateCache(Cache ch, int offset)
			{
				if (!Tools.StringEquals(Program.Memory, offset, 8, ch.Name))
				{
					ch.Name = Encoding.ASCII.GetString(Program.Memory, offset, 8);
				}

				ch.MaxFreeData = Program.Memory.ReadUnsignedShort(offset + 10);
				ch.SizeFreeData = Program.Memory.ReadUnsignedShort(offset + 12);
				ch.NumMaxEntry = Program.Memory.ReadUnsignedShort(offset + 14);
				ch.NumUsedEntry = Program.Memory.ReadUnsignedShort(offset + 16);

				for (int i = 0; i < Math.Min(ch.NumUsedEntry, 100); i++)
				{
					int addr = 22 + i * 10 + offset;
					int id = Program.Memory.ReadUnsignedShort(addr);

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
						entry = new CacheEntry
						{
							Id = id,
							StartTicks = ticks,
							Index = uniqueId++
						};

						ch.Entries.AddLast(entry);
					}
					else if (entry.Removed)
					{
						entry.StartTicks = ticks; //entry removed then added should appears as added
					}

					entry.Size = Program.Memory.ReadUnsignedShort(addr + 4);
					entry.Ticks = ticks;
					entry.Slot = i;

					if (ch.Name != "_MEMORY_")
					{
						entry.Time = Program.Memory.ReadUnsignedInt(addr + 6);

						if (entry.Time != entry.LastTime)
						{
							entry.TouchedTicks = ticks;
							entry.LastTime = entry.Time;
						}
					}
				}
			}

			void UpdateEntries(Cache ch)
			{
				var node = ch.Entries.First;
				while (node != null)
				{
					var next = node.Next;
					var entry = node.Value;
					if (Shared.Tools.GetTimeSpan(ticks, entry.Ticks) > TimeSpan.FromSeconds(2))
					{
						ch.Entries.Remove(node);
					}
					else
					{
						entry.Touched = Shared.Tools.GetTimeSpan(ticks, entry.TouchedTicks) < TimeSpan.FromSeconds(1);
						entry.Added = Shared.Tools.GetTimeSpan(ticks, entry.StartTicks) < TimeSpan.FromSeconds(2);
						entry.Removed = Shared.Tools.GetTimeSpan(ticks, entry.Ticks) > TimeSpan.Zero;
					}
					node = next;
				}
			}
		}

		void IWorker.KeyDown(ConsoleKeyInfo keyInfo)
		{
			switch (keyInfo.Key)
			{
				case ConsoleKey.F5:
					clearCache = true;
					break;

				case ConsoleKey.Spacebar:
					showTimestamp = !showTimestamp;
					break;

				case ConsoleKey.S:
					Sort.SortMode = (SortMode)(((int)Sort.SortMode + 1) % 3);
					Sort.SortEntries(cache);
					break;
			}
		}

		void ClearCache(Cache ch, int cachePointer)
		{
			Program.Memory.Write((ushort)ch.MaxFreeData, 0);
			Program.Process.Write(Program.Memory, cachePointer + 12, 2); //size free data

			Program.Memory.Write(0, 0);
			Program.Process.Write(Program.Memory, cachePointer + 16, 2); //num used entries

			ch.SizeFreeData = ch.MaxFreeData;
			ch.NumUsedEntry = 0;
			ch.Entries.Clear();
		}

		void WriteStats(int done, int total)
		{
			Console.BackgroundColor = ConsoleColor.Black;
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.Write("{0,5}/{1,5}", done, total);

			Console.CursorLeft++;
			int value = Tools.RoundToNearest(done * 6, total);

			Console.ForegroundColor = ConsoleColor.DarkGray;
			for (int i = 0; i < 6; i++)
			{
				bool selected = i < value;
				Console.BackgroundColor = selected ? ConsoleColor.DarkGray : ConsoleColor.Black;
				Console.Write(selected ? ' ' : '.');
			}
		}

		void WriteEntry(CacheEntry entry, Cache ch)
		{
			Console.Write("{0,3} ", entry.Id);
			if (ch.Name == "_MEMORY_")
			{
				Console.Write("        ");
			}
			else if (showTimestamp || Program.VarParser == null)
			{
				TimeSpan time = TimeSpan.FromSeconds(entry.Time / 60);
				Console.Write("{0:D2}:{1:D2}.{2:D2}", time.Minutes, time.Seconds, entry.Time % 60);
			}
			else
			{
				Console.Write("{0,-8}", Program.VarParser.GetText(ch.Section, entry.Id, 8));
			}

			int entrySize = entry.Size;
			if (entrySize >= 1000 && entrySize < 1024)
			{
				entrySize = 1024; //make sure entry size always fit 3 digits
			}
			bool kilobyte = entrySize >= 1024;
			Console.Write(" {0,3} {1}", kilobyte ? entrySize /= 1024 : entrySize, kilobyte ? 'K' : 'B');
		}

		void RenderCache(Cache ch, int column)
		{
			(Console.BackgroundColor, Console.ForegroundColor) = (ConsoleColor.Black, ConsoleColor.Gray);

			Console.SetCursorPosition(column * 19 + 5, 0);
			Console.Write(ch.Name);

			switch (Sort.SortMode)
			{
				case SortMode.Memory:
					Console.Write(" ▼mem");
					break;
				case SortMode.LRU:
					Console.Write(" ▼lru");
					break;
			}

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
					(Console.BackgroundColor, Console.ForegroundColor) = (ConsoleColor.DarkGray, ConsoleColor.Black);
				}
				else if (entry.Added)
				{
					(Console.BackgroundColor, Console.ForegroundColor) = (ConsoleColor.DarkGreen, ConsoleColor.Black);
				}
				else if (entry.Touched)
				{
					(Console.BackgroundColor, Console.ForegroundColor) = (ConsoleColor.Black, ConsoleColor.DarkYellow);
				}
				else
				{
					(Console.BackgroundColor, Console.ForegroundColor) = (ConsoleColor.Black, ConsoleColor.DarkGray);
				}

				Console.SetCursorPosition(column * 19, row + 4);
				WriteEntry(entry, ch);
				row++;
			}
		}

		void IWorker.Render()
		{
			int column = 0;
			foreach (var ch in cache)
			{
				if (ch.Name != null)
				{
					RenderCache(ch, column);
				}

				column++;
			}
		}

		void IWorker.MouseMove(int x, int y)
		{
		}

		void IWorker.MouseDown(int x, int y)
		{
		}
	}
}
