using System;
using System.Collections.Generic;

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
			CheckGoto();
			Optimize(nodes);
			RemoveEndLife();
		}

		void CheckGoto()
		{
			foreach (var ins in nodes)
			{
				if (ins.Goto != -1 && !nodesMap.ContainsKey(ins.Goto))
				{
					throw new Exception("Invalid goto: " + ins.Goto);
				}
			}
		}

		void Optimize(LinkedList<Instruction> list)
		{
			for (var node = list.First; node != null; node = node.Next)
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
			var previous = target.Previous;
			if (previous != null && previous.Value.Type == LifeEnum.GOTO) //else or elseif
			{
				previous.List.Remove(previous); //remove goto
				ins.NodesA = GetNodesBetween(node.Next, target, node);
				ins.NodesB = GetNodesBetween(target, nodesMap[previous.Value.Goto], node);

				Optimize(ins.NodesA);
				Optimize(ins.NodesB);
			}
			else //if
			{
				ins.NodesA = GetNodesBetween(node.Next, target, node);
				Optimize(ins.NodesA);
			}
		}

		#endregion

		#region Switch

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
			while (target.Next != null &&
				target.Value.Type != LifeEnum.CASE &&
				target.Value.Type != LifeEnum.MULTI_CASE)
			{
				target = target.Next;
			}

			if (target.Next == null) //end of local list reached, get next instruction another way
			{
				while (target.Value.Parent != null) //get first parent that has a non null next sibling
				{
					target = target.Value.Parent;
					if (target.Next != null)
					{
						target = target.Next;
						break;
					}
				}
			}

			if (target != node.Next) //default statement right after switch
			{
				if (target.Previous.Value.Type == LifeEnum.GOTO)
				{
					target.Previous.List.Remove(target.Previous); //remote goto
				}

				var def = new Instruction { Type = LifeEnum.CASE_DEFAULT, Goto = target.Value.Position };
				node.List.AddBefore(node.Next, def);
			}

			//follow all cases to detect end of switch
			LinkedListNode<Instruction> endOfSwitch = null;
			while (target.Value.Type == LifeEnum.CASE
				|| target.Value.Type == LifeEnum.MULTI_CASE)
			{
				target = nodesMap[target.Value.Goto];

				if (target.Previous != null && target.Previous.Value.Type == LifeEnum.GOTO)
				{
					endOfSwitch = nodesMap[target.Previous.Value.Goto];
					target.Previous.List.Remove(target.Previous); //remote goto before case
				}
			}

			if (endOfSwitch == null || target == endOfSwitch) //should be equal, otherwise there is a default case
			{
				node.Value.NodesA = GetNodesBetween(node.Next, target, node);
			}
			else
			{
				var def = new Instruction { Type = LifeEnum.CASE_DEFAULT, Goto = endOfSwitch.Value.Position };
				var defNode = node.List.AddBefore(target, def);
				nodesMap[target.Value.Position] = defNode;

				node.Value.NodesA = GetNodesBetween(node.Next, endOfSwitch, node);
			}

			OptimizeCase(node.Value.NodesA);
		}

		void OptimizeCase(LinkedList<Instruction> list)
		{
			for (var node = list.First; node != null; node = node.Next)
			{
				if (node.Value.Type == LifeEnum.CASE ||
					node.Value.Type == LifeEnum.MULTI_CASE ||
					node.Value.Type == LifeEnum.CASE_DEFAULT)
				{
					var target = nodesMap[node.Value.Goto];
					node.Value.NodesA = GetNodesBetween(node.Next, target, node);
					Optimize(node.Value.NodesA);
				}
			}
		}

		#endregion

		LinkedList<Instruction> GetNodesBetween(LinkedListNode<Instruction> start, LinkedListNode<Instruction> end, LinkedListNode<Instruction> parent)
		{
			//return a linked list with all nodes between start and end (exclusive), set their parent field, remove them from main list
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

		void RemoveEndLife()
		{
			if (nodes.Last != null && nodes.Last.Value.Type == LifeEnum.ENDLIFE)
			{
				nodes.Remove(nodes.Last); //remove ENDLIFE (if last instruction)
			}
		}
	}
}
