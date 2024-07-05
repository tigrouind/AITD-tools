using System;
using System.Collections.Generic;

namespace VarsViewer
{
	public class CacheEntrySlotComparer : IComparer<CacheEntry>
	{
		public int Compare(CacheEntry x, CacheEntry y)
		{
			return x.Slot.CompareTo(y.Slot);
		}
	}
}
