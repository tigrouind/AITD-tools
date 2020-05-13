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
		public int Frame; //last frame
		public int FrameStart; //frame added 
	}
}
