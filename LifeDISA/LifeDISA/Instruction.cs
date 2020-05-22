
using System;
using System.Collections.Generic;

namespace LifeDISA
{
	public class Instruction
	{
		public LifeEnum Type;
		public string Name;
		public List<string> Arguments = new List<string>();
		public string Separator;

		public bool IndentInc;
		public bool IndentDec;

		public int Goto = -1;

		public void Add(string format, params object[] args)
		{
			Arguments.Add(string.Format(format, args));
		}

		public void Add(string value)
		{
			Arguments.Add(value);
		}

		public void Add(int value)
		{
			Arguments.Add(value.ToString());
		}
	}
}
