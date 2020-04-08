using System.Collections.Generic;

namespace CacheViewer
{
	public class Cache
	{
		public string Name;
		public Dictionary<int, CacheEntry> Entries = new Dictionary<int, CacheEntry>();
		public long Address;
		public byte[] Pattern;
		
		public int MaxFreeData;
		public int SizeFreeData;
		public int NumMaxEntry;
		public int NumUsedEntry;		
	}
}
