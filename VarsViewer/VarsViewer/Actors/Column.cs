
using System.Collections.Generic;

namespace VarsViewer
{
	public class Column
	{
		public string Name;
		public ColumnType Type;
		public int Width;
		public int ExtraWidth;
		public bool Visible;
		public long Timer;
		public int Offset;
		public IDictionary<int, string> Values;
		public Column[] Columns;
	}
}
