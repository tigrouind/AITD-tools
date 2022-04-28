using System;
using System.Collections.Generic;

namespace CacheViewer
{
	public class CacheEntryComparer : IComparer<CacheEntry>
	{
		public bool CompareMode;
		
		#region IComparer implementation
		
		public int Compare(CacheEntry x, CacheEntry y)
		{
			if (CompareMode)
			{		
				return -x.Sort.CompareTo(y.Sort);
			}
			
			return x.Index.CompareTo(y.Index);
		}
		
		#endregion
	}
}
