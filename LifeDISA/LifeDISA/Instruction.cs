
using System;
using System.Collections.Generic;

namespace LifeDISA
{
	public class Instruction
	{
		public LifeEnum Type;
		public EvalEnum EvalEnum; //first argument of switch
		public List<string> Arguments = new List<string>();
		public string Actor;
		public int Goto = -1;
		public bool ToRemove;
		public int Line;

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
		
		public bool IndentInc
		{
			get
			{
				switch(Type)
				{
					#if !NO_OPTIMIZE
					case LifeEnum.IF_EGAL:
					case LifeEnum.IF_DIFFERENT:
					case LifeEnum.IF_SUP_EGAL:
					case LifeEnum.IF_SUP:
					case LifeEnum.IF_INF_EGAL:
					case LifeEnum.IF_INF:
					case LifeEnum.ELSE:
					case LifeEnum.SWITCH:
					case LifeEnum.CASE:	
					case LifeEnum.MULTI_CASE:										
					case LifeEnum.DEFAULT:								
						return true;
					#endif	
					default:
						return false;
				}
			}
		}
		
		public bool IndentDec
		{
			get
			{
				switch(Type)
				{
					#if !NO_OPTIMIZE
					case LifeEnum.ELSE:						
					case LifeEnum.END:					
						return true;
					#endif		
					default:
						return false;
				}
			}
		}
		
		public string Separator
		{
			get
			{
				switch (Type)
				{
					#if !NO_OPTIMIZE
					case LifeEnum.IF_EGAL:
					case LifeEnum.IF_DIFFERENT:
					case LifeEnum.IF_SUP_EGAL:
					case LifeEnum.IF_SUP:
					case LifeEnum.IF_INF_EGAL:
					case LifeEnum.IF_INF:
						return " and ";
					#endif			
					default:
						return " ";
				}
			}
		}
		
		public string Name
		{
			get
			{
				#if !NO_OPTIMIZE
				switch (Type)
				{
					
					case LifeEnum.IF_EGAL:
					case LifeEnum.IF_DIFFERENT:
					case LifeEnum.IF_SUP_EGAL:
					case LifeEnum.IF_SUP:
					case LifeEnum.IF_INF_EGAL:
					case LifeEnum.IF_INF:
						return "if";
						
					case LifeEnum.MULTI_CASE:
						return "case";
						
					case LifeEnum.C_VAR:
						return "set";						
				}
				#endif	
				string name = Type.ToString().ToLowerInvariant();
				if (Actor != null) 
				{
					return Actor + "." + name;
				}
				
				return name;
			}
		}
	}
}
