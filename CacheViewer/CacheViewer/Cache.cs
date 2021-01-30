using System.Collections.Generic;

namespace CacheViewer
{
	public class Cache
	{
		public string Name;
		public LinkedList<CacheEntry> Entries = new LinkedList<CacheEntry>();
		public int[] Address;

		public int MaxFreeData;
		public int SizeFreeData;
		public int NumMaxEntry;
		public int NumUsedEntry;
	}
}
