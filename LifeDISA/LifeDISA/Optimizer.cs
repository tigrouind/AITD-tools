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
			// if COND goto A       if COND
			//   ...                  ...        
			//   goto B        =>            
			// A:                   else     
			//   ...                  ...
			// B:                   end
			
			var ins = node.Value;
			var target = nodesMap[ins.Goto];
			var previous = target.Previous;
			if (previous.Value.Type == LifeEnum.GOTO)
			{							
				toRemove.Add(previous); //remove goto
				AddBefore(target, new Instruction { Type = LifeEnum.ELSE });						
				AddBefore(nodesMap[previous.Value.Goto], new Instruction { Type = LifeEnum.END }); //end of if else 
			}
			else
			{
				AddBefore(target, new Instruction { Type = LifeEnum.END }); //regular if 
			}
			
			// if COND1 goto A       if COND1 and COND2 and COND3
			// if COND2 goto A   =>     
			// if COND3 goto A          
			//	 ...                    ...
			// A:                    end  

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
				ins.LineEnd = next.Value.LineEnd;

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
			
			//switch             switch 
			//                      default                     
			//    ...                 ...
			//    goto X       =>   end						
			//  case 1 goto X       
			//    ...                      
			//  X:               end
			
			if (defaultCase && target.Previous.Value.Type == LifeEnum.GOTO) //default statement right after switch with a goto
			{
				toRemove.Add(target.Previous); //remove default goto
								
				//remove everything after default/end block
				var start = target;
				var end = nodesMap[target.Value.Goto];
				RemoveNodes(start, end);
							
				AddBefore(node.Next, new Instruction { Type = LifeEnum.DEFAULT });
				AddBefore(end, new Instruction { Type = LifeEnum.END }); //end of default	
				AddBefore(end, new Instruction { Type = LifeEnum.END }); //end of switch			
				return;
			}
			
			//switch             switch 
			//  case 1 goto A       case 1
			//    ...                 ...
			//    goto X      =>    end   
			//  A:                  default                     
			//    ...                 ...
			//                      end 
			//  X:               end
	
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
		
		void RemoveNodes(LinkedListNode<Instruction> start, LinkedListNode<Instruction> end)
		{
			while (start != null && start != end)
			{
				var next = start.Next;
				nodes.Remove(start);
				start = next;
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
			
			//remove endlife (if last instruction)
			if (nodes.Last != null && nodes.Last.Value.Type == LifeEnum.ENDLIFE)
			{
				nodes.Remove(nodes.Last);
			}
		}			
	}
}
