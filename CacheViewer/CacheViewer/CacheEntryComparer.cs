using System;
using System.Collections.Generic;

namespace CacheViewer
{
	public class CacheEntryComparer : IComparer<CacheEntry>
	{
		public bool SortByTimestamp;
		
		#region IComparer implementation
		
		public int Compare(CacheEntry x, CacheEntry y)
		{
			if (SortByTimestamp)
			{
				return -x.Time.CompareTo(y.Time);
			}
			
			return x.Index.CompareTo(y.Index);
		}
		
		#endregion
	}
}
