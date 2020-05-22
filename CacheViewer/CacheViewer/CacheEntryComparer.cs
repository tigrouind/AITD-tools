
using System;
using System.Collections.Generic;

namespace CacheViewer
{
	public class CacheEntryComparer : IComparer<CacheEntry>
	{
		public int Compare(CacheEntry x, CacheEntry y)
		{
			return -x.TimePerSecond.CompareTo(y.TimePerSecond);
		}
	}
}
