using Shared;

namespace VarsViewer
{
	public class Var
	{
		public VarEnum Type;
		public int Index;

		public int Value;
		public string Text;

		public bool Difference; //value has changed since last time
		public long Time; //time since last difference
		public int SaveState; //value copied here after SaveState
	}
}
