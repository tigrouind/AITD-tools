using System;
using System.Collections.Generic;

namespace LifeDISA
{
	public class Optimizer
	{
		readonly LinkedList<Instruction> nodes;
		readonly Dictionary<int, LinkedListNode<Instruction>> nodesMap = new Dictionary<int, LinkedListNode<Instruction>>();
		readonly List<LinkedListNode<Instruction>> toRemove = new List<LinkedListNode<Instruction>>();
		readonly List<Tuple<LinkedListNode<Instruction>, Instruction>> toAdd = new List<Tuple<LinkedListNode<Instruction>, Instruction>>();
		
		public Optimizer(LinkedList<Instruction> nodes, Dictionary<int, LinkedListNode<Instruction>> nodesMap)
		{
			this.nodes = nodes;
			this.nodesMap = nodesMap;
		}
				
		public void Run()
		{
			CheckGotos();
			Optimize();			
			AddItems();			
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
		
		void Optimize()
		{					
			for(var node = nodes.First; node != null; node = node.Next)
			{
				var ins = node.Value;
				switch(ins.Type)
				{
					case LifeEnum.IF_EGAL:
					case LifeEnum.IF_DIFFERENT:
					case LifeEnum.IF_SUP_EGAL:
					case LifeEnum.IF_SUP:
					case LifeEnum.IF_INF_EGAL:
					case LifeEnum.IF_INF:
					case LifeEnum.IF_IN:
					case LifeEnum.IF_OUT:
						OptimizeIf(node);
						break;

					case LifeEnum.SWITCH:
						OptimizeSwitch(node);
						break;
				}
			}
		}
		
		void OptimizeIf(LinkedListNode<Instruction> node)
		{
			var ins = node.Value;
			var target = nodesMap[ins.Goto];
			var previous = target.Previous;
			if (previous.Value.Type == LifeEnum.GOTO)
			{							
				toRemove.Add(previous);
				AddBefore(target, new Instruction { Type = LifeEnum.ELSE });						
				AddBefore(nodesMap[previous.Value.Goto], new Instruction { Type = LifeEnum.END });
			}
			else
			{
				AddBefore(target, new Instruction { Type = LifeEnum.END });
			}

			//check for consecutive IFs
			var next = node.Next;
			while(next != null &&
				(
				next.Value.Type == LifeEnum.IF_EGAL ||
				next.Value.Type == LifeEnum.IF_DIFFERENT ||
				next.Value.Type == LifeEnum.IF_INF ||
				next.Value.Type == LifeEnum.IF_INF_EGAL ||
				next.Value.Type == LifeEnum.IF_SUP ||
				next.Value.Type == LifeEnum.IF_IN ||
				next.Value.Type == LifeEnum.IF_OUT ||
				next.Value.Type == LifeEnum.IF_SUP_EGAL) &&
				target == nodesMap[next.Value.Goto]) //the IFs ends up at same place
			{
				var after = next.Next;
				ins.Add(next.Value.Arguments[0]);
				nodes.Remove(next);

				next = after;
			}
		}
		
		void OptimizeSwitch(LinkedListNode<Instruction> node)
		{	
			//instruction after switch should be CASE or MULTICASE
			//but if could be instructions (eg: DEFAULT after switch)
			var target = node.Next;
			bool defaultCase = false;
			while(target != null && target.Next != null &&
				  target.Value.Type != LifeEnum.CASE &&
				  target.Value.Type != LifeEnum.MULTI_CASE)
			{
				defaultCase = true;
				target = target.Next;
			}
			
			if (defaultCase && target.Previous.Value.Type == LifeEnum.GOTO) //default statement right after switch with a goto
			{
				toRemove.Add(target.Previous); //remove goto
				AddBefore(node.Next, new Instruction { Type = LifeEnum.DEFAULT });
				AddBefore(target, new Instruction { Type = LifeEnum.END });	
			}
	
			//detect end of switch
			LinkedListNode<Instruction> endOfSwitch = null;
			bool lastInstruction = false;
	
			do
			{
				var ins = target.Value;
				switch(ins.Type)
				{
					case LifeEnum.CASE:
					case LifeEnum.MULTI_CASE: //detect end of case
					{
						ins.Parent = node.Value;
						target = nodesMap[ins.Goto];
						if (target.Previous.Value.Type == LifeEnum.GOTO)
						{
							toRemove.Add(target.Previous); //remote goto
							if(endOfSwitch == null) //first case target is end of switch
							{
								endOfSwitch = nodesMap[target.Previous.Value.Goto];
							}
						}
	
						AddBefore(target, new Instruction { Type = LifeEnum.END });
						break;
					}
	
					default:
						lastInstruction = true;
						break;
				}
			}
			while (!lastInstruction);
	
			//should be equal, otherwise there is a default case
			if(endOfSwitch != null && target != endOfSwitch)
			{
				AddBefore(endOfSwitch, new Instruction { Type = LifeEnum.END }); //end of default
				AddBefore(endOfSwitch, new Instruction { Type = LifeEnum.END }); //end of switch
				AddBefore(target, new Instruction { Type = LifeEnum.DEFAULT });
			}
			else
			{
				AddBefore(target, new Instruction { Type = LifeEnum.END });
			}
		}
		
		void AddBefore(LinkedListNode<Instruction> node, Instruction value)
		{
			toAdd.Add(new Tuple<LinkedListNode<Instruction>, Instruction>(node, value));
		}
		
		void AddItems()
		{
			foreach (var item in toAdd)
			{
				nodes.AddBefore(item.Item1, item.Item2);
			}
		}
		
		void Cleanup()
		{
			//remove gotos
			//gotos can't be removed immediately because they might be referenced by IF/CASE statements
			foreach (var node in toRemove)
			{
				nodes.Remove(node);
			}
			
			//remove endlife
			if (nodes.Last != null && nodes.Last.Value.Type == LifeEnum.ENDLIFE)
			{
				nodes.Remove(nodes.Last);
			}
		}			
	}
}
