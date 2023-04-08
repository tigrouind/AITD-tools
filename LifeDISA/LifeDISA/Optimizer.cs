using System;
using System.Collections.Generic;
using System.Linq;

namespace LifeDISA
{
	public class Optimizer
	{
		readonly LinkedList<Instruction> nodes;
		readonly Dictionary<int, LinkedListNode<Instruction>> nodesMap = new Dictionary<int, LinkedListNode<Instruction>>();

		public Optimizer(LinkedList<Instruction> nodes, Dictionary<int, LinkedListNode<Instruction>> nodesMap)
		{
			this.nodes = nodes;
			this.nodesMap = nodesMap;
		}

		public void Run()
		{
			CheckGotos();
			Optimize(nodes);
			Cleanup();
		}

		void CheckGotos()
		{
			for (var node = nodes.First; node != null; node = node.Next)
			{
				var ins = node.Value;
				if (ins.Goto != -1)
				{
					if (!nodesMap.ContainsKey(ins.Goto))
					{
						throw new Exception("Invalid goto: " + ins.Goto);
					}
				}
			}
		}

		void Optimize(LinkedList<Instruction> list)
		{
			for(var node = list.First; node != null; node = node.Next)
			{
				var ins = node.Value;
				if (ins.IsIfCondition)
				{
					DetectIfElse(node);
				}

				if (ins.Type == LifeEnum.SWITCH)
				{
					DetectSwitch(node);
				}
			}
		}

		#region If

		void DetectIfElse(LinkedListNode<Instruction> node)
		{
			// if COND goto A       if COND
			//   ...                  ...
			//   goto B
			// A:                   else
			//   ...                  ...
			// B:                   end

			var ins = node.Value;
			var target = nodesMap[ins.Goto];
			var previous = target.Value.Previous;
			if (previous.Value.Type == LifeEnum.GOTO) //else or elseif
			{
				RemoveNode(previous); //remove goto
				ins.NodesA = GetNodesBetween(node.Next, target, ins);
				ins.NodesB = GetNodesBetween(target, nodesMap[previous.Value.Goto], ins);

				Optimize(ins.NodesA);
				Optimize(ins.NodesB);
			}
			else //if
			{
				ins.NodesA = GetNodesBetween(node.Next, target, ins);
				Optimize(ins.NodesA);
			}
		}

		#endregion

		#region Switch

		void OptimizeCase(LinkedList<Instruction> list)
		{
			for (var node = list.First; node != null; node = node.Next)
			{
				if (node.Value.Type == LifeEnum.CASE ||
					node.Value.Type == LifeEnum.MULTI_CASE ||
					node.Value.Type == LifeEnum.DEFAULT)
				{
					var target = nodesMap[node.Value.Goto];
					node.Value.NodesA = GetNodesBetween(node.Next, target, node.Value);
					Optimize(node.Value.NodesA);
				}
			}
		}

		void DetectSwitch(LinkedListNode<Instruction> node)
		{
			//switch             switch
			//                      default
			//    ...                  ...
			//    goto B            end
			//  case 1 goto A       case 1
			//    ...                 ...
			//    goto B      =>    end
			//  A:                  default
			//    ...                 ...
			//                      end
			//  B:               end

			//instruction after switch should be CASE or MULTICASE
			//but if could be instructions (eg: DEFAULT after switch)
			LinkedListNode<Instruction> target = node.Next;

			//skip code before after switch, before first case
			while (target != null && target.Value.Next != null &&
				target.Value.Type != LifeEnum.CASE &&
				target.Value.Type != LifeEnum.MULTI_CASE)
			{
				target = target.Value.Next;
			}

			if (target != node.Next) //default statement right after switch
			{
				if (target.Value.Previous.Value.Type == LifeEnum.GOTO)
				{
					RemoveNode(target.Value.Previous); //remote goto
				}

				var def = new Instruction { Type = LifeEnum.DEFAULT, Goto = target.Value.Position };
				var defNode = node.List.AddBefore(node.Next, def);
			}

			//follow all cases to detect end of switch
			LinkedListNode<Instruction> endOfSwitch = null;
			while (target.Value.Type == LifeEnum.CASE
				|| target.Value.Type == LifeEnum.MULTI_CASE)
			{
				target = nodesMap[target.Value.Goto];

				if (target.Value.Previous.Value.Type == LifeEnum.GOTO)
				{
					endOfSwitch = nodesMap[target.Value.Previous.Value.Goto];
					RemoveNode(target.Value.Previous); //remote goto
				}
			}

			if(endOfSwitch != null && target != endOfSwitch) //should be equal, otherwise there is a default case
			{
				var def = new Instruction { Type = LifeEnum.DEFAULT, Goto = endOfSwitch.Value.Position };
				var defNode = node.List.AddBefore(target, def);
				nodesMap[target.Value.Position] = defNode;

				node.Value.NodesA = GetNodesBetween(node.Next, endOfSwitch, node.Value);
			}
			else
			{
				node.Value.NodesA = GetNodesBetween(node.Next, target, node.Value);
			}

			OptimizeCase(node.Value.NodesA);
		}

		#endregion

		LinkedList<Instruction> GetNodesBetween(LinkedListNode<Instruction> start, LinkedListNode<Instruction> end, Instruction parent)
		{
			LinkedList<Instruction> result = new LinkedList<Instruction>();
			while (start != null && start.Value != end.Value)
			{
				var next = start.Next;
				start.List.Remove(start);
				start.Value.Parent = parent;
				result.AddLast(start);
				start = next;
			}

			return result;
		}

		void RemoveNode(LinkedListNode<Instruction> node)
		{
			node.Value.Previous.Value.Next = node.Value.Next;
			node.Value.Next.Value.Previous = node.Value.Previous;
			node.List.Remove(node);
		}

		void Cleanup()
		{
			//remove endlife (if last instruction)
			if (nodes.Last != null && nodes.Last.Value.Type == LifeEnum.ENDLIFE)
			{
				nodes.Remove(nodes.Last);
			}
		}
	}
}
