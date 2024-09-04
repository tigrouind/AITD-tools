using System.Collections.Generic;

namespace VarsViewer
{
	public static class Tools
	{
		public static int RoundToNearest(int dividend, int divisor)
		{
			return (dividend + (divisor / 2)) / divisor;
		}

		public static string SubString(string text, int length, bool ellipsis = false)
		{
			if (text.Length > length)
			{
				if (ellipsis && length > 1)
				{
					return text.Substring(0, length - 1) + "…";
				}
				else
				{
					return text.Substring(0, length);
				}
			}

			return text;
		}

		public static string PadBoth(string text, int length)
		{
			int spaces = length - text.Length;
			int padLeft = spaces / 2 + text.Length;
			return text.PadLeft(padLeft).PadRight(length);
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

				if (node != min)
				{
					list.Remove(node);
					list.AddBefore(min, node);
				}

				node = next;
			}
		}
	}
}
