using System;
using System.Collections.Generic;

namespace CacheViewer
{
	public class CacheEntryComparer : IComparer<CacheEntry>
	{
		#region IComparer implementation

		public int Compare(CacheEntry x, CacheEntry y)
		{
			switch (Sort.SortMode)
			{
				case SortMode.Default:
					return x.Index.CompareTo(y.Index);

				case SortMode.Memory:
					return x.Slot.CompareTo(y.Slot);

				case SortMode.LRU:
					if (x.Removed && !y.Removed) return 1;
					if (!x.Removed && y.Removed) return -1;
					return x.Slot.CompareTo(y.Slot);

				default:
					throw new NotSupportedException();
			}
		}

		#endregion
	}
}
