
using System;
using System.Collections.Generic;

namespace CacheViewer
{
	public static class Sort
	{
		static readonly CacheEntryComparer comparer = new CacheEntryComparer();

		public static void SortEntries(Cache[] cache)
		{
			for(int i = 0 ; i < cache.Length ; i++)
			{
				var ch = cache[i];		
				if (ch.Name != "_MEMORY_")
				{				
					SortEntries(ch);
				}
			}
		}
		
		static void SortEntries(Cache ch)
		{
			Tools.InsertionSort(ch.Entries, comparer);
			SelectionSort(ch.Entries);
		}
				
		static void SelectionSort(LinkedList<CacheEntry> entries)
		{			
			var node = entries.Last;
			while (node != null && node.Value.Removed)
			{
				node = node.Previous;
			}
			
			while (node != null)
			{
				var minValue = uint.MaxValue;
				var min = entries.First;
				
		        for (var comp = entries.First; comp != node.Next; comp = comp.Next)
		        {
		            if (comp.Value.Time < minValue)
		            {
		            	minValue = min.Value.Time;
		                min = comp;
		            }
		        }				
		        
		        if (min != node)
		        {
		        	entries.Remove(min);
		        	entries.AddAfter(node, min);
		        }
				
				node = min.Previous;
			}
		}
	}
}
