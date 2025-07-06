using System.Collections.Generic;
using Shared;

namespace VarsViewer
{
	public class Cache(int index, VarEnum section)
	{
		public string Name;
		public readonly LinkedList<CacheEntry> Entries = new();
		public readonly VarEnum Section = section;
		public readonly int Index = index;

		public int MaxFreeData;
		public int SizeFreeData;
		public int NumMaxEntry;
		public int NumUsedEntry;
	}
}
