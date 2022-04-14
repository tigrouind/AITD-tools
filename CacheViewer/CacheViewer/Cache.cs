﻿using System.Collections.Generic;
using Shared;

namespace CacheViewer
{
	public class Cache
	{
		public string Name;
		public readonly LinkedList<CacheEntry> Entries = new LinkedList<CacheEntry>();
		public readonly VarEnum Section;

		public int MaxFreeData;
		public int SizeFreeData;
		public int NumMaxEntry;
		public int NumUsedEntry;
		
		public Cache(VarEnum section)
		{
			Section = section;
		}
	}
}
