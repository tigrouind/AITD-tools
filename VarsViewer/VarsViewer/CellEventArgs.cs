using System;
using System.Drawing;

namespace VarsViewer
{
	public class CellEventArgs : EventArgs
	{
		public Var Var;
		public RectangleF Rectangle;
		public short Value;
	}
}
