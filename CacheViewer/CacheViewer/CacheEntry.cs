using System;

namespace CacheViewer
{
	public class CacheEntry
	{
		public int Id;
		public int Key; //from AITD
		public int Time; //from AITD
		public int LastTime;
		public bool Touched; //time != lasttime
		public int Size; //from AITD
		public int Ticks; //last time
		public int StartTicks; //time added 
	}
}
