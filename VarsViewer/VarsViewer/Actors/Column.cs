
using System.Collections.Generic;

namespace VarsViewer
{
	public class Column
	{
		public string Name;
		public ColumnType Type;
		public int Width;
		public bool Visible;
		public int Offset;
		public string[] Values;
		public Column[] Columns;
	}
}
