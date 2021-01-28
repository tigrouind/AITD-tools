
using System;

namespace Shared
{
	public struct DosMCB
	{
		public int Position;
		public int Tag;
		public int Owner;
		public int Size;
		#if DEBUG
		public string Name;
		#endif
	}
}
