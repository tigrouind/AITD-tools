using System.Collections.Generic;

namespace VarsViewer
{
	public class CacheEntryRemovedComparer : IComparer<CacheEntry>
	{
		public int Compare(CacheEntry x, CacheEntry y)
		{
			if (x.Removed && !y.Removed) return 1;
			if (!x.Removed && y.Removed) return -1;
			return x.Slot.CompareTo(y.Slot);
		}
	}
}
