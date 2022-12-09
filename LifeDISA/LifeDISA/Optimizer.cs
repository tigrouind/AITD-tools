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
				if (ins.IsIfCondition)
				{
					DetectIfElse(node);
					CombineConsecutiveIfs(node);
				}
				
				switch(ins.Type)
				{
					case LifeEnum.SWITCH:
						OptimizeSwitch(node);
						break;
				}
			}
		}
		
		#region If
		
		void DetectIfElse(LinkedListNode<Instruction> node)
		{
			// if COND goto A       if COND      
			//   ...                  ...        
			//   goto C                     
			// A:                   elseif    
			// if COND goto B  =>       
			//   ...                  ...
			//   goto C  
			// B:                   else
			//   ...                  ...
			// C:                   end
			
			var ins = node.Value;
			var target = nodesMap[ins.Goto];
			var previous = target.Previous;
			if (previous.Value.Type == LifeEnum.GOTO) //else or elseif
			{										
				toRemove.Add(previous); //remove goto				
				if (target.Value.IsIfCondition && GetEndOfIf(target.Value) == previous.Value.Goto) //else directly followed by if block (and nothing else after if)
				{					
					target.Value.IsIfElse = true;
				}
				else 
				{					
					AddBefore(target, new Instruction { Type = LifeEnum.ELSE });						
				}
				
				if (!ins.IsIfElse)
				{					
					AddBefore(nodesMap[previous.Value.Goto], new Instruction { Type = LifeEnum.END }); //end of else or elseif
				}
			}
			else if (!ins.IsIfElse)
			{
				AddBefore(target, new Instruction { Type = LifeEnum.END }); //regular if 
			}
		}
				
		int GetEndOfIf(Instruction ins)
		{
			var target = nodesMap[ins.Goto];
			var previous = target.Previous;
			if (previous.Value.Type == LifeEnum.GOTO)
			{
				return previous.Value.Goto;
			}
			
			return ins.Goto;
		}
				
		void CombineConsecutiveIfs(LinkedListNode<Instruction> node)
		{
			// if COND1 goto A       if COND1 
			// if COND2 goto A   =>     and COND2 
			// if COND3 goto A          and COND3
			//	 ...                    ...
			// A:                    end
			
			var target = nodesMap[node.Value.Goto];
			var next = node.Next;
			while(next != null &&
				next.Value.IsIfCondition &&
				target == nodesMap[next.Value.Goto]) //the IFs ends up at same place
			{
				next.Value.Type = LifeEnum.AND;				
				next = next.Next;
			}
			
			if (next != node.Next)
			{
				AddBefore(next, new Instruction { Type = LifeEnum.BEGIN });
			}
		}
		
		#endregion
		
		#region Switch
		
		void OptimizeSwitch(LinkedListNode<Instruction> node)
		{	
			//instruction after switch should be CASE or MULTICASE
			//but if could be instructions (eg: DEFAULT after switch)
			LinkedListNode<Instruction> target = node.Next;
			SkipCodeAfterSwitchBeforeCase(ref target);
			
			if (target != node.Next && target.Previous.Value.Type == LifeEnum.GOTO) //default statement right after switch with a goto
			{
				//switch             switch 
				//                      default                     
				//    ...          =>     ...
				//    goto X            end						
				//  case 1 goto X       
				//    ...                      
				//  X:               end
				
				toRemove.Add(target.Previous); //remove default goto
								
				//remove everything after default/end block
				var start = target;
				var end = nodesMap[target.Value.Goto];
				RemoveNodes(start, end);
							
				AddBefore(node.Next, new Instruction { Type = LifeEnum.DEFAULT });
				AddBefore(end, new Instruction { Type = LifeEnum.END }); //end of default	
				AddBefore(end, new Instruction { Type = LifeEnum.END }); //end of switch			
			}
			else 
			{
				//switch             switch 
				//  case 1 goto A       case 1
				//    ...                 ...
				//    goto X      =>    end   
				//  A:                  default                     
				//    ...                 ...
				//                      end 
				//  X:               end
							
				var endOfSwitch = ProcessCaseStatements(node, ref target);
				if(endOfSwitch != null && target != endOfSwitch) //should be equal, otherwise there is a default case
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
		}
				
		void SkipCodeAfterSwitchBeforeCase(ref LinkedListNode<Instruction> target)
		{								
			while (target != null && target.Next != null &&
				  target.Value.Type != LifeEnum.CASE &&
				  target.Value.Type != LifeEnum.MULTI_CASE)
			{
				target = target.Next;
			}						
		}
		
		LinkedListNode<Instruction> ProcessCaseStatements(LinkedListNode<Instruction> node, ref LinkedListNode<Instruction> target)
		{
			LinkedListNode<Instruction> endOfSwitch = null;
	
			while (target.Value.Type == LifeEnum.CASE
			    || target.Value.Type == LifeEnum.MULTI_CASE)
			{
				target.Value.Parent = node.Value; //used later
				target = nodesMap[target.Value.Goto];
				
				if (target.Previous.Value.Type == LifeEnum.GOTO)
				{
					toRemove.Add(target.Previous); //remote goto
					if (endOfSwitch == null) //first case target is end of switch
					{
						endOfSwitch = nodesMap[target.Previous.Value.Goto];
					}
					else if(endOfSwitch != nodesMap[target.Previous.Value.Goto])
					{
						throw new Exception("Inconsistent case statements found");
					}
				}

				AddBefore(target, new Instruction { Type = LifeEnum.END });				
			}	
			
			return endOfSwitch;
		}		
		
		#endregion
		
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
			//nodes have to be added later to not conflict with optimization
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
