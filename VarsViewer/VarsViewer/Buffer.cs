using System;

namespace VarsViewer
{
	public class Buffer<T>
	{
		T[] array = [];
		public T[] AsArray() => array;

		public int Width { private set; get; }
		public int Height { private set; get; }

		public void Clear()
		{
			Array.Clear(array, 0, array.Length);
		}

		public T this[int y, int x]
		{
			get => array[y * Width + x];

			set
			{
				EnsureCapacity(x + 1, y + 1);
				array[y * Width + x] = value;
			}
		}

		public T this[int x]
		{
			get => this[0, x];
			set => this[0, x] = value;
		}

		public void EnsureCapacity(int width, int height)
		{
			if (Width < width || Height < height)
			{
				width = Math.Max(PowerOf2(width), Width);
				height = Math.Max(PowerOf2(height), Height);

				Resize();
				Width = width;
				Height = height;
			}

			void Resize()
			{
				var oldArray = array;
				array = new T[width * height];

				for (int row = Height - 1; row >= 0; row--)
				{
					Array.Copy(oldArray, row * Width, array, row * width, Width);
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
