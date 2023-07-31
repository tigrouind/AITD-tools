
using System;
using System.Collections.Generic;

namespace LifeDISA
{
	public class Instruction
	{
		public LifeEnum Type;
		public EvalEnum EvalEnum; //first argument of switch
		readonly List<string> arguments = new List<string>();
		public string Actor;
		public int Goto = -1;
		public int Position; //used to map gotos to a given instruction
		public int Size; //size in bytes of instruction

		public LinkedListNode<Instruction> Previous; //previous instruction (before optimisation)
		public LinkedListNode<Instruction> Next; //next instruction (before optimisation)

		public Instruction Parent; //node above instruction (eg: case -> switch)
		public LinkedList<Instruction> NodesA; //child nodes below instruction (eg: if -> { ... } )
		public LinkedList<Instruction> NodesB; //child nodes below instruction (eg: else -> { ... } )

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

		public bool IsElseIfCondition
		{
			get
			{
				return NodesB != null
					&& NodesB.Count == 1
					&& NodesB.First.Value.IsIfCondition;
			}
		}

		public bool IsAndCondition
		{
			get
			{
				return IsIfCondition
					&& NodesA != null
					&& NodesA.Count == 1
					&& NodesA.First.Value.IsIfCondition
					&& NodesA.First.Value.NodesB == null;
			}
		}

		public bool IsIfCondition
		{
			get
			{
				switch (Type)
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

		public IReadOnlyList<string> Arguments
		{
			get
			{
				return arguments;
			}
		}

		public override string ToString()
		{
			return Type.ToString();
		}
	}
}
