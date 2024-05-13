using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared;

namespace LifeDISA
{
	public class Writer : StreamWriter
	{
		byte[] allBytes;
		int padding;
		int indent;
		readonly bool noOptimize;
		readonly bool verbose;

		public Writer(string filePath, bool verbose, bool noOptimize)
			: base(filePath)
		{
			this.verbose = verbose;
			this.noOptimize = noOptimize;
		}

		public void Dump(LinkedList<Instruction> nodes, byte[] bytes)
		{
			padding = nodes.Any() ? GetNodes(nodes).Max(x => x.Size) / 2 : 0;
			allBytes = bytes;

			if (noOptimize)
			{
				DumpRaw(nodes);
			}
			else
			{
				DumpOptimized(nodes);
			}
		}

		void DumpRaw(LinkedList<Instruction> nodes)
		{
			int maxLength = nodes.Any() ? nodes.Max(x => x.Position).ToString().Length : 0;
			foreach (var ins in nodes)
			{
				WriteLine(ins, "{0} {1}{2}", ins.Position.ToString().PadLeft(maxLength), GetInstructionName(ins), GetArguments(ins));
			}
		}

		void DumpOptimized(LinkedList<Instruction> nodes)
		{
			foreach (var ins in nodes)
			{
				WriteLine(ins, "{0}{1}", GetInstructionName(ins), GetArguments(ins));
				WriteNodes(ins);
			}
		}

		void WriteNodes(Instruction ins)
		{
			if (ins.NodesB != null)
			{
				indent++;
				DumpOptimized(ins.NodesA);
				indent--;

				if (ins.IsElseIfCondition) //else if
				{
					WriteLine("else {0}{1}", GetInstructionName(ins.NodesB.First.Value), GetArguments(ins.NodesB.First.Value));
					WriteNodes(ins.NodesB.First.Value);
				}
				else //else
				{
					WriteLine("else");

					indent++;
					DumpOptimized(ins.NodesB);
					indent--;

					WriteLine("end");
				}
			}
			else if (ins.NodesA != null) //if
			{
				if (ins.IsAndCondition) //if ... AND ..
				{
					indent++;
					while (ins.IsAndCondition)
					{
						ins = ins.NodesA.First.Value;
						WriteLine("and{0}", GetArguments(ins));
					}
					indent--;
					WriteLine("begin");
				}

				indent++;
				DumpOptimized(ins.NodesA);
				indent--;

				WriteLine("end");
			}
		}

		void WriteLine(string format, params string[] args)
		{
			WriteLine(null, format, args);
		}

		void WriteLine(Instruction ins, string format, params string[] args)
		{
			if (verbose)
			{
				if (ins != null)
				{
					var bytes = Enumerable.Range(0, ins.Size / 2).Select(x => allBytes.ReadUnsignedShortSwap(ins.Position + x * 2).ToString("x4"));
					Write("[{0}]", string.Join(" ", bytes).PadLeft(padding * 4 + (padding - 1), ' '));
				}
				else
				{
					Write(new string(' ', padding * 4 + (padding - 1) + 2));
				}

				Write('\t');
			}

			Write(new string('\t', indent));
			base.WriteLine(string.Format(format, args));
		}

		string GetInstructionName(Instruction ins)
		{
			if (!noOptimize)
			{
				if (ins.IsIfCondition)
				{
					return "if";
				}

				switch (ins.Type)
				{
					case LifeEnum.MULTI_CASE:
						return "case";

					case LifeEnum.C_VAR:
						return "set";
				}
			}

			string name = ins.Type.ToString().ToLowerInvariant();
			if (ins.Actor != null)
			{
				return ins.Actor + "." + name;
			}

			return name;
		}

		string GetArguments(Instruction ins)
		{
			if (ins.Arguments.Any())
			{
				return " " + string.Join(" ", ins.Arguments.ToArray());
			}

			return string.Empty;
		}

		IEnumerable<Instruction> GetNodes(LinkedList<Instruction> nodes)
		{
			for (var node = nodes.First; node != null; node = node.Value.Next)
			{
				yield return node.Value;
			}
		}
	}
}