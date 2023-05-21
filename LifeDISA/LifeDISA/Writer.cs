using System;
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

		public Writer(string filePath)
			: base(filePath)
		{
		}

		void Init(LinkedList<Instruction> nodes, byte[] bytes)
		{
			padding = nodes.Any() ? GetNodes(nodes).Max(x => x.LineEnd - x.LineStart) / 2 : 0;
			allBytes = bytes;
		}

		public void DumpRaw(LinkedList<Instruction> nodes, byte[] bytes)
		{
			Init(nodes, bytes);
			int maxLength = nodes.Any() ? nodes.Max(x => x.LineStart).ToString().Length : 0;
			foreach (var ins in nodes)
			{
				WriteLine("{0} {1}{2}", ins.LineStart.ToString().PadLeft(maxLength), ins.Name, GetArguments(ins));
			}
		}

		public void DumpOptimized(LinkedList<Instruction> nodes, byte[] bytes)
		{
			Init(nodes, bytes);
			Dump(nodes);
		}

		void Dump(LinkedList<Instruction> nodes)
		{
			foreach (var ins in nodes)
			{
				WriteLine(ins, "{0}{1}", ins.Name, GetArguments(ins));
				WriteNodes(ins);
			}
		}

		void WriteNodes(Instruction ins)
		{
			if (ins.NodesB != null)
			{
				indent++;
				Dump(ins.NodesA);
				indent--;

				if (ins.IsElseIfCondition) //else if
				{
					WriteLine("else {0}{1}", ins.NodesB.First.Value.Name, GetArguments(ins.NodesB.First.Value));
					WriteNodes(ins.NodesB.First.Value);
				}
				else //else
				{
					WriteLine("else");

					indent++;
					Dump(ins.NodesB);
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
				Dump(ins.NodesA);
				indent--;

				WriteLine("end");
			}
		}

		void WriteLine(string format, params string[] args)
		{
			WriteLine(new Instruction(), format, args);
		}

		void WriteLine(Instruction ins, string format, params string[] args)
		{
			if (Program.Verbose)
			{
				var bytes = Enumerable.Range(0, (ins.LineEnd - ins.LineStart) / 2).Select(x => allBytes.ReadUnsignedShortSwap(x * 2).ToString("X4"));
				Write("|{0}|", string.Join(" ", bytes).PadLeft(padding * 4 + (padding - 1), ' '));
				Write('\t');
			}

			Write(new String('\t', indent));
			base.WriteLine(string.Format(format, args));
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