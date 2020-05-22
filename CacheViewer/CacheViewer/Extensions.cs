using System;
using System.Collections.Generic;

namespace CacheViewer
{
	public static class Extensions
	{
		//stable and fast enough for small lists already substantially sorted
		public static void InsertionSort<T>(this IList<T> source, IComparer<T> comparer)
		{
			for (int i = 1; i < source.Count; i++)
			{
				var item = source[i];
				int j = i - 1;
				while (j >= 0 && comparer.Compare(source[j], item) > 0)
				{
					source[j + 1] = source[j];
					j--;
				}

				source[j + 1] = item;
			}
		}
	}
}
