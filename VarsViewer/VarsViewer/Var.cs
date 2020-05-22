using System;
using System.Drawing;

namespace VarsViewer
{
	public class Var
	{
		public string Text;
		public bool Difference;

		public RectangleF Rectangle;
		public bool Refresh;

		public int Index;
		public string Name;
		public short Value;
		public short SaveState; //value set there when using SaveState button
		public int Time;	//time since last difference
		public long MemoryAddress = -1;
	}
}
