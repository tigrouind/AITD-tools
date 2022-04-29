using System;
using System.Collections.Generic;

namespace CacheViewer
{
	public class CacheEntryComparer : IComparer<CacheEntry>
	{
		#region IComparer implementation
		
		public int Compare(CacheEntry x, CacheEntry y)
		{
			if (x.Removed && !y.Removed) return 1;
			if (!x.Removed && y.Removed) return -1;
			
			return x.Index.CompareTo(y.Index);
		}
		
		#endregion
	}
}
