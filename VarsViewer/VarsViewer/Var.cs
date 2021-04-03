using System;
using Shared;

namespace VarsViewer
{
	public class Var
	{
		public string Text;
		public bool Difference;

		public bool Refresh;

		public int Index;
		public VarEnum Type;
		public int Value;
		public int SaveState; //value set there when using SaveState button
		public int Time;	//time since last difference
	}
}
