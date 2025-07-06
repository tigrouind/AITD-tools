using System.Collections.Generic;

namespace VarsViewer
{
	public static class Sort
	{
		public static SortMode SortMode;
		static readonly CacheEntryIndexComparer comparerIndex = new();
		static readonly CacheEntrySlotComparer comparerSlot = new();
		static readonly CacheEntryRemovedComparer comparerRemoved = new();

		public static void SortEntries(IEnumerable<Cache> cache)
		{
			foreach (var ch in cache)
			{
				if (ch.Name != "_MEMORY_")
				{
					SortEntries(ch);
				}
			}

			static void SortEntries(Cache ch)
			{
				switch (SortMode)
				{
					case SortMode.Default:
						Tools.InsertionSort(ch.Entries, comparerIndex);
						break;

					case SortMode.Memory:
						Tools.InsertionSort(ch.Entries, comparerSlot);
						break;

					case SortMode.LRU:
						Tools.InsertionSort(ch.Entries, comparerRemoved);
						SelectionSort(ch.Entries);
						break;
				}
			}
		}

		static void SelectionSort(LinkedList<CacheEntry> entries)
		{
			var node = entries.Last;
			while (node != null && node.Value.Removed) //skip removed items
			{
				node = node.Previous;
			}

			while (node != null) //sort same way as AITD buggy LRU
			{
				var minValue = uint.MaxValue;
				var min = entries.First;

				for (var comp = entries.First; comp != node.Next; comp = comp.Next)
				{
					if (comp.Value.Time < minValue)
					{
						minValue = min.Value.Time; //should be comp.Value.Time
						min = comp;
					}
				}

				if (min != node) //put node at end of list
				{
					entries.Remove(min);
					entries.AddAfter(node, min);
				}

				node = min.Previous;
			}
		}
	}
}
