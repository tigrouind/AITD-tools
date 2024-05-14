using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LifeDISA
{
	public class Writer : StreamWriter
	{
		int indent;

		public Writer(string filePath)
			: base(filePath)
		{
		}

		public void Dump(LinkedList<Instruction> nodes, bool raw, bool verbose, byte[] bytes)
		{
			if (raw)
			{
				DumpRaw(nodes, verbose, bytes);
			}
			else
			{
				DumpOptimized(nodes);
			}
		}

		void DumpRaw(LinkedList<Instruction> nodes, bool verbose, byte[] bytes)
		{
			int padding = nodes.Any() ? GetNodes().Max(x => x.Size) / 2 : 0;
			int maxLength = nodes.Any() ? nodes.Max(x => x.Position).ToString().Length : 0;
			foreach (var ins in nodes)
			{
				if (verbose)
				{
					WriteHeader(ins);
				}

				string header = ins.Position.ToString().PadLeft(maxLength);
				WriteIndent($"{header}: {GetInstructionName(ins)} {string.Join(" ", ins.Arguments.ToArray())}");
			}

			void WriteHeader(Instruction ins)
			{
				var bytesData = Enumerable.Range(0, ins.Size / 2)
					.Select(x => bytes.ReadUnsignedShortSwap(ins.Position + x * 2)
					.ToString("x4"));

				var byteInfo = string.Join("_", bytesData).PadLeft(padding * 4 + (padding - 1), ' ');
				Write($"{byteInfo} ");
			}

			IEnumerable<Instruction> GetNodes()
			{
				for (var node = nodes.First; node != null; node = node.Value.Next)
				{
					yield return node.Value;
				}
			}
		}

		#region Optimized

		void DumpOptimized(LinkedList<Instruction> nodes)
		{
			foreach (var ins in nodes)
			{
				switch (ins.Type)
				{
					case LifeEnum.VAR:
					case LifeEnum.C_VAR:
						WriteIndent($"{ins.Arguments[0]} = {ins.Arguments[1]}");
						break;

					case LifeEnum.ADD:
						WriteIndent($"{ins.Arguments[0]} += {ins.Arguments[1]}");
						break;

					case LifeEnum.SUB:
						WriteIndent($"{ins.Arguments[0]} -= {ins.Arguments[1]}");
						break;

					case LifeEnum.INC:
						WriteIndent($"{ins.Arguments[0]} += 1");
						break;

					case LifeEnum.DEC:
						WriteIndent($"{ins.Arguments[0]} -= 1");
						break;

					case LifeEnum.IF_EGAL:
					case LifeEnum.IF_DIFFERENT:
					case LifeEnum.IF_SUP_EGAL:
					case LifeEnum.IF_SUP:
					case LifeEnum.IF_INF_EGAL:
					case LifeEnum.IF_INF:
					case LifeEnum.IF_IN:
					case LifeEnum.IF_OUT:
						var (nextIns, parameters) = GetIfAndConditions(ins);
						WriteIndent($"if {parameters} then");
						WriteNodes(nextIns);
						WriteIndent("end if");
						break;


					case LifeEnum.SWITCH:
						WriteIndent($"select case {ins.Arguments[0]}");
						WriteNodes(ins);
						WriteIndent("end select");
						break;

					case LifeEnum.CASE:
					case LifeEnum.MULTI_CASE:
						WriteIndent($"case {string.Join(", ", ins.Arguments)}");
						WriteNodes(ins);
						break;

					case LifeEnum.CASE_DEFAULT:
						WriteIndent("case else");
						WriteNodes(ins);
						break;

					case LifeEnum.RETURN:
						WriteIndent("return");
						break;

					default: //function call
						WriteIndent($"{GetInstructionName(ins)}({string.Join(", ", ins.Arguments)})");
						break;
				}
			}
		}

		void WriteNodes(Instruction ins)
		{
			if (ins.NodesA != null) //if, switch, case
			{
				indent++;
				DumpOptimized(ins.NodesA);
				indent--;

				if (ins.NodesB != null) //else, elseif
				{
					if (ins.IsElseIfCondition) //elseif
					{
						var (nextIns, parameters) = GetIfAndConditions(ins.NodesB.First.Value);
						WriteIndent($"elseif {parameters} then");
						WriteNodes(nextIns);
					}
					else //else
					{
						WriteIndent("else");
						indent++;
						DumpOptimized(ins.NodesB);
						indent--;
					}
				}
			}
		}

		void WriteIndent(string text)
		{
			Write(new string('\t', indent));
			WriteLine(text);
		}

		string GetIfArguments(Instruction ins)
		{
			switch (ins.Type)
			{
				case LifeEnum.IF_EGAL:
					return $"{ins.Arguments[0]} = {ins.Arguments[1]}";

				case LifeEnum.IF_DIFFERENT:
					return $"{ins.Arguments[0]} <> {ins.Arguments[1]}";

				case LifeEnum.IF_SUP_EGAL:
					return $"{ins.Arguments[0]} >= {ins.Arguments[1]}";

				case LifeEnum.IF_SUP:
					return $"{ins.Arguments[0]} > {ins.Arguments[1]}";

				case LifeEnum.IF_INF_EGAL:
					return $"{ins.Arguments[0]} <= {ins.Arguments[1]}";

				case LifeEnum.IF_INF:
					return $"{ins.Arguments[0]} < {ins.Arguments[1]}";

				case LifeEnum.IF_IN:
					return $"{ins.Arguments[0]} >= {ins.Arguments[1]} and {ins.Arguments[0]} <= {ins.Arguments[2]}";

				case LifeEnum.IF_OUT:
					return $"not({ins.Arguments[0]} >= {ins.Arguments[1]} and {ins.Arguments[0]} <= {ins.Arguments[2]})";

				default:
					throw new NotSupportedException();
			}
		}

		(Instruction, string) GetIfAndConditions(Instruction ins)
		{
			var conditions = GetNodes().ToArray();
			return (conditions.Last(), conditions.Length == 1 ? GetIfArguments(conditions[0]) : string.Join(" and ", conditions.Select(x => $"({GetIfArguments(x)})")));

			IEnumerable<Instruction> GetNodes()
			{
				yield return ins;
				while (ins.IsAndCondition)
				{
					ins = ins.NodesA.First.Value;
					yield return ins;
				}
			}
		}

		#endregion

		string GetInstructionName(Instruction ins)
		{
			string name = ins.Type.ToString().ToLowerInvariant();
			if (ins.Actor != null)
			{
				return ins.Actor + "." + name;
			}

			return name;
		}
	}
}