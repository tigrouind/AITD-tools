using System;

namespace CacheViewer
{
	public class CacheEntry
	{
		public int Id; //from AITD
		public int Size; //from AITD
		public uint Time; //from AITD
		public uint LastTime;

		public bool Touched;
		public bool Added;
		public bool Removed;

		public int TouchedTicks; //time != lasTtime
		public int Ticks; //last time seen
		public int StartTicks; //time added
		public int Index; //for sorting
		public int Sort; //for sorting
	}
}
