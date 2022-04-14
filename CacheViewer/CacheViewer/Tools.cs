using System;
using System.Collections.Generic;
using System.Linq;

namespace CacheViewer
{
	public static class Tools
	{
		public static int RoundToNearest(int dividend, int divisor)
        {
        	return (dividend + (divisor / 2)) / divisor;
        }
		
		public static bool StringEquals(byte[] data, int index, int count, string value)
		{			
			if(value == null)
			{
				return false;
			}
			
			if(GetByteCount(data, index, count) != value.Length)
			{
				return false;
			}
			
			for (int i = 0 ; i < Math.Min(value.Length, count); i++)
			{
				if (data[index + i] != value[i])
				{
					return false;
				}
			}
			
			return true;
		}
										
		static int GetByteCount(byte[] data, int index, int count)
		{
			for(int i = 0 ; i < count ; i++)
			{
				if(data[index + i] == 0)
				{
					return i;
				}
			}
			
			return count;
		}	

		public static void InsertionSort<T>(LinkedList<T> list, IComparer<T> comparer)
		{
			var node = list.First;
			while (node != null)
			{
				var next = node.Next;
				
				var min = node;
				for (var comp = node.Previous; comp != null && comparer.Compare(node.Value, comp.Value) < 0; comp = comp.Previous)
				{
					min = comp;
				}
				
				if(node != min) 
				{				
					list.Remove(node);
					list.AddBefore(min, node);
				}
				
				node = next;
			}
		}		
	}
}
