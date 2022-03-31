using System;
using System.Collections.Generic;

namespace LifeDISA
{
	#if !NO_OPTIMIZE
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
			Optimize();			
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
				previous.Value.ToRemove = true;
				nodes.AddBefore(target, new Instruction { Type = LifeEnum.ELSE });						
				nodes.AddBefore(nodesMap[previous.Value.Goto], new Instruction { Type = LifeEnum.END });
			}
			else
			{
				nodes.AddBefore(target, new Instruction { Type = LifeEnum.END });
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
				ins.Arguments.Add(next.Value.Arguments[0]);
				nodes.Remove(next);

				next = after;
			}
		}
		
		void OptimizeSwitch(LinkedListNode<Instruction> node)
		{
			var ins = node.Value;			
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
			
			if (defaultCase) //default statement right after switch
			{
				if (target.Previous.Value.Type == LifeEnum.GOTO)
				{
					target.Previous.Value.ToRemove = true;
				}
				
				nodes.AddBefore(node.Next, new Instruction { Type = LifeEnum.DEFAULT });
				nodes.AddBefore(target, new Instruction { Type = LifeEnum.END });	
			}
	
			//detect end of switch
			LinkedListNode<Instruction> endOfSwitch = null;
			bool lastInstruction = false;
	
			do
			{
				ins = target.Value;
				switch(ins.Type)
				{
					case LifeEnum.CASE:
					case LifeEnum.MULTI_CASE:
					{
						ins.Parent = node.Value;
						target = nodesMap[ins.Goto];
						if (target.Previous.Value.Type == LifeEnum.GOTO)
						{
							target.Previous.Value.ToRemove = true;
							if(endOfSwitch == null)
							{
								endOfSwitch = nodesMap[target.Previous.Value.Goto];
							}
						}
	
						nodes.AddBefore(target, new Instruction { Type = LifeEnum.END });
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
				nodes.AddBefore(endOfSwitch, new Instruction { Type = LifeEnum.END });
				nodes.AddBefore(endOfSwitch, new Instruction { Type = LifeEnum.END });
				nodes.AddBefore(target, new Instruction { Type = LifeEnum.DEFAULT });
			}
			else
			{
				nodes.AddBefore(target, new Instruction { Type = LifeEnum.END });
			}
		}
		
		void Cleanup()
		{
			//remove GOTO and ENDLIFE
			var currentNode = nodes.First;
			while(currentNode != null)
			{
				var nextNode = currentNode.Next;
				if(currentNode.Value.ToRemove)
				{
					nodes.Remove(currentNode);
				}

				currentNode = nextNode;
			}
		}			
	}
	#endif		
}
