using System;

namespace CacheViewer
{
	public class StringBuffer
	{
		char[] buffer = new char[16];
		int length;

		public int Length
		{
			get
			{
				return length;
			}
		}
		
		public char this[int index]
		{
			get
			{
				return buffer[index];
			}
		}
		
		public void Clear()
		{
			length = 0;
		}
		
		public void Append(StringBuffer value)
		{
			EnsureCapacity(length + value.length);
			Array.Copy(value.buffer, 0, buffer, length, value.length);
			length += value.length;
		}
		
		public void Append(char value, int repeat = 1)
		{
			EnsureCapacity(length + repeat);
			for(int i = 0; i < repeat; i++)
			{
				buffer[length++] = value;
			}
		}
		
		public void Append(string value)
		{
			EnsureCapacity(length + value.Length);
			for(int i = 0; i < value.Length; i++)
			{
				buffer[length++] = value[i];
			}
		}
		
		public void Append(uint value, int digits)
		{
			int start = length;
			do
			{	
				var reminder = value % 10;
				Append((char)(reminder + '0'));
				value /= 10;
			}
			while(--digits > 0 || value > 0);
			Array.Reverse(buffer, start, length - start);
		}
		
		public void Append(int value, int digits)
		{
			if (value < 0)
			{
				Append('-');
				Append((uint)(-value), digits);
				
			}
			else
			{
				Append((uint)value, digits);
			}
		}	
		
		public override string ToString()
		{
			return new string(buffer, 0, length);
		}
		
		void EnsureCapacity(int capacity)
		{
			if (buffer.Length < capacity)
			{
				var newBuffer = new char[buffer.Length * 2];
				Array.Copy(buffer, newBuffer, buffer.Length);
				buffer = newBuffer;
			}
		}
	}	
}
