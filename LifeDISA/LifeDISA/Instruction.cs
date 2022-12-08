
using System;
using System.Collections.Generic;

namespace LifeDISA
{
	public class Instruction
	{
		public LifeEnum Type;
		public EvalEnum EvalEnum; //first argument of switch
		public Instruction Parent;
		readonly List<string> arguments = new List<string>();
		public string Actor;
		public int Goto = -1;
		public int LineStart, LineEnd;
		public bool IsIfElse;

		public void Add(string format, params object[] args)
		{
			arguments.Add(string.Format(format, args));
		}

		public void Add(string value)
		{
			arguments.Add(value);
		}

		public void Add(int value)
		{
			arguments.Add(value.ToString());
		}
		
		public void Set(int index, string value)
		{
			arguments[index] = value;
		}
		
		public bool IndentInc
		{
			get
			{
				if (!Program.NoOptimize)
				{
					if (IsIfCondition)
					{
						return true;
					}
						
					switch(Type)
					{
						case LifeEnum.ELSE:
						case LifeEnum.SWITCH:
						case LifeEnum.CASE:	
						case LifeEnum.MULTI_CASE:										
						case LifeEnum.DEFAULT:								
						case LifeEnum.BEGIN:
							return true;						
					}								
				}
				
				return false;
			}
		}
		
		public bool IndentDec
		{
			get
			{
				if (!Program.NoOptimize)
				{
					if (IsIfElse)
					{
						return true;
					}
														
					switch(Type)
					{
						case LifeEnum.ELSE:						
						case LifeEnum.END:					
						case LifeEnum.BEGIN:							
							return true;
					}
				}
				
				return false;
			}
		}
		
		public bool IsIfCondition
		{
			get
			{
				switch(Type)
				{
					case LifeEnum.IF_EGAL:
					case LifeEnum.IF_DIFFERENT:
					case LifeEnum.IF_SUP_EGAL:
					case LifeEnum.IF_SUP:
					case LifeEnum.IF_INF_EGAL:
					case LifeEnum.IF_INF:
					case LifeEnum.IF_IN:
					case LifeEnum.IF_OUT:
						return true;
						
					default:
						return false;
				}
			}
		}
		
		public string Name
		{
			get
			{
				if (!Program.NoOptimize)
				{
					if (IsIfCondition)
					{
						if (IsIfElse)
						{
							return "else if";
						}
						
						return "if";
					}
					
					switch (Type)
					{
						case LifeEnum.MULTI_CASE:
							return "case";
							
						case LifeEnum.C_VAR:
							return "set";						
					}
				}

				string name = Type.ToString().ToLowerInvariant();
				if (Actor != null) 
				{
					return Actor + "." + name;
				}
				
				return name;
			}
		}
		
		public IReadOnlyList<string> Arguments
		{
			get				
			{
				return arguments;
			}
		}
	}
}
