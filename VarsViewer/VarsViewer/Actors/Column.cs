
using System.Collections.Generic;

namespace VarsViewer
{
	public class Column
	{
		public string Name;
		public ColumnType Type;
		public int Offset;
		public Column[] Columns;
		public IDictionary<int, string> Values;
		public bool IncludeZero;
		public int Condition;

		public int Width;
		public int ExtraWidth;
		public bool Visible;
		public long Timer;
		public bool Hidden;
	}
}
