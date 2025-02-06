using System;

namespace VarsViewer
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

		public long TouchedTicks; //time != lasTtime
		public long Ticks; //last time seen
		public long StartTicks; //time added
		public int Slot; //for sorting
		public int Index; //for sorting
	}
}
