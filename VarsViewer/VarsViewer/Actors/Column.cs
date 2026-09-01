
using System.Collections.Generic;

namespace VarsViewer
{
	public class Column
	{
		public string Name;
		public ColumnType Type;
		public int Offset; //memory offset
		public Column[] Columns;
		public Dictionary<int, string> Values; //to map values to a string, for display
		public bool IncludeZero; //should be shown if value is zero
		public int Condition; //the value at that memory offset should be non zero for column to be visible

		public int TextWidth; //width without column header
		public int Width; //to contains text
		public int ExtraWidth; //might be more to contains childs columns
		public bool Visible; //at least one value exists
		public long Timer;
		public bool Hidden; //explicitly hidden by user
	}
}
