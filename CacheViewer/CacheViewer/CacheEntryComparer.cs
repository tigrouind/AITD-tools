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
				int result = 0;
				if (!(x.Touched && !x.Removed && y.Touched && !y.Removed))
				{
					result = -x.Time.CompareTo(y.Time);
				}
				
				if (result == 0)
				{
					return -GetScore(x).CompareTo(GetScore(y));
				}
				
				return result;
			}
			
			return x.Index.CompareTo(y.Index);
		}
		
		#endregion
		
		int GetScore(CacheEntry x)
		{
			if (x.Added) return 2;
			if (x.Removed) return 0;
			return 1;
		}
	}
}
