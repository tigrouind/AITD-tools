using System;

namespace VarsViewer
{
	public class Buffer<T>
	{
		T[] array = new T[0];
		public static implicit operator T[](Buffer<T> x) => x.array;

		public int Width;
		public int Height;

		public void Clear()
		{
			Array.Clear(array, 0, array.Length);
		}

		public T this[int y, int x]
		{
			get
			{
				return array[y * Width + x];
			}

			set
			{
				EnsureCapacity(x + 1, y + 1);
				array[y * Width + x] = value;
			}
		}

		public T this[int i]
		{
			get
			{
				return array[i];
			}
		}

		public void EnsureCapacity(int newWidth, int newHeight)
		{
			if (Width < newWidth || Height < newHeight)
			{
				newWidth = Math.Max(PowerOf2(newWidth), Width);
				newHeight = Math.Max(PowerOf2(newHeight), Height);

				Resize();
				Width = newWidth;
				Height = newHeight;
			}

			void Resize()
			{
				var oldArray = array;
				array = new T[newWidth * newHeight];

				for (int row = Height - 1; row >= 0; row--)
				{
					Array.Copy(oldArray, row * Width, array, row * newWidth, Width);
				}
			}

			int PowerOf2(int value)
			{
				value--;
				value |= value >> 1;
				value |= value >> 2;
				value |= value >> 4;
				value |= value >> 8;
				value |= value >> 16;
				value++;

				return value;
			}
		}
	}
}
