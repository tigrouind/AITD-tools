using System;

namespace CacheViewer
{
	public class Entry
	{
		public int id;
		public int key; //from AITD
		public int time; //from AITD
		public int lasttime;
		public bool touched; //time != lasttime
		public int size; //from AITD
		public int frame; //last frame
		public int framestart; //frame added 
	}
}
