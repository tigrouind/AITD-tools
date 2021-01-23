using System.Collections.Generic;

namespace CacheViewer
{
	public class Cache
	{
		public string Name;
		public List<CacheEntry> Entries = new List<CacheEntry>();
		public int[] Address;

		public int MaxFreeData;
		public int SizeFreeData;
		public int NumMaxEntry;
		public int NumUsedEntry;
	}
}
