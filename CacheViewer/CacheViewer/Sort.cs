
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
			comparer.CompareMode = false;
			Tools.InsertionSort(ch.Entries, comparer);
			
			Reset(ch);
			Markdown(ch);			
			
			comparer.CompareMode = true;
			Tools.InsertionSort(ch.Entries, comparer);
		}
		
		static void Reset(Cache ch)
		{
			for (var node = ch.Entries.First; node != null; node = node.Next)
			{
				node.Value.Sort = -1;
			}
		}
		
		static void Markdown(Cache ch)
		{			
			int uniqueId = 0;
			bool anyItem = ch.Entries.Count > 0;
			while (anyItem)
			{			
				anyItem = false;				
				var minTime = uint.MaxValue;
				
				CacheEntry bestEntry = null;				
				for (var node = ch.Entries.First; node != null; node = node.Next)
				{
					if (!node.Value.Removed && node.Value.Sort == -1 && node.Value.Time < minTime)
					{
						anyItem = true;
						minTime = (bestEntry ?? node.Value).Time;
						bestEntry = node.Value;
					}
				}
			
				if (anyItem)
				{
					bestEntry.Sort = uniqueId++;
				}
			}
		}
	}
}
