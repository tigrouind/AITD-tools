using System;
using System.Runtime.InteropServices;

namespace CacheViewer
{	
    [StructLayout(LayoutKind.Explicit)]
	public struct FormatArgument
	{
		[FieldOffset(0)] 
		public char Char;
		
		[FieldOffset(0)] 
		public int Int;
		
		[FieldOffset(0)] 
		public uint UInt;
				
		[FieldOffset(4)] 
		public string String;
		
		[FieldOffset(8)]
		public Type Type;
		
		public static implicit operator FormatArgument(int value)
		{
			return new FormatArgument { Int = value, Type = typeof(int) };
		}
		
		public static implicit operator FormatArgument(uint value)
		{
			return new FormatArgument { UInt = value, Type = typeof(uint) };
		}
		
		public static implicit operator FormatArgument(char value)
		{
			return new FormatArgument { Char = value, Type = typeof(char) };
		}
		
		public static implicit operator FormatArgument(string value)
		{
			return new FormatArgument { String = value, Type = typeof(string) };
		}
		
		public override string ToString()
		{
			if (Type == typeof(int))
			{
				return Int.ToString();
			}			
			
			if (Type == typeof(uint))
			{
				return UInt.ToString();
			}
			
			if (Type == typeof(char))
			{
				return Char.ToString();
			}
			
			if (Type == typeof(string))
			{
				return String;
			}
			
			if (Type == null)
			{
				return string.Empty;
			}
			
			throw new NotSupportedException();
		}
	}
}
