using System.Collections.Generic;

namespace VarsViewer
{
	public class CacheEntryIndexComparer : IComparer<CacheEntry>
	{
		public int Compare(CacheEntry x, CacheEntry y)
		{
			return x.Index.CompareTo(y.Index);
		}
	}
}
