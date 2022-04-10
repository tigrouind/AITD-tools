using System;

namespace CacheViewer
{
	//GC friendly (unlike string.Format())	
	public static class StringFormat
	{			
		public static char[] Buffer = new char[256];
		public static int BufferLength;
		static int charStart, charLength;
				
		public static void Format(string format, FormatArgument arg0, FormatArgument arg1, FormatArgument arg2, FormatArgument arg3, FormatArgument arg4)
		{			
			Array.Clear(Buffer, 0, Buffer.Length);
			BufferLength = 0;
			int pos = 0;
			while (pos < format.Length)
			{
				char ch = format[pos++];
				if (ch == '{')
				{					
					ch = format[pos++];
					if (ch < '0' || ch > '9') throw new FormatException();
					
					FormatArgument value;
					int arg = ch - '0';
					switch (arg)
					{
						case 0:
							value = arg0;
							break;							
						case 1:
							value = arg1;
							break;						
						case 2:
							value = arg2;
							break;							
						case 3:
							value = arg3;
							break;							
						case 4:
							value = arg4;
							break;									
						default:
							throw new FormatException();
					}
					
					int width = 0;
					int zeroPad = 0;
					ch = format[pos++];
					
					//padding (optional)
					if (ch == ',') 
					{		
						ch = format[pos++];
						
						if (ch < '0' || ch > '9') throw new FormatException();
						do
						{
		                    width = width * 10 + ch - '0';		                    
		                    if (pos == format.Length) throw new FormatException();
		                    ch = format[pos++];
		                } 
						while (ch >= '0' && ch <= '9');
						
						//zero padding (optional)
						if (ch == ':')
						{
							ch = format[pos++];
							if (ch != 'D') throw new FormatException();
							
							ch = format[pos++];
							if (ch < '0' || ch > '9') throw new FormatException();
							do
							{	 
								zeroPad = zeroPad * 10 + ch - '0';								
			                    if (pos == format.Length) throw new FormatException();
			                    ch = format[pos++];
			                } 
							while (ch >= '0' && ch <= '9');
							
							if(zeroPad > width) width = zeroPad;
						}
					}
					
					ToString(value);
										
					//padding left
					if (width > 0)
					{
						for(int i = 0 ; i < width - charLength ; i++)
						{
							Buffer[BufferLength++] = zeroPad > 0 ? '0' : ' ';
						}
					}
					
					for(int i = 0 ; i < charLength ; i++)
					{
						Buffer[BufferLength++] = Buffer[i + charStart];
					}
	
					//padding right					
					if (width < 0)
					{
						width = -width;
						for(int i = 0 ; i < width - charLength ; i++)
						{
							Buffer[BufferLength++] = ' ';
						}
					}
																																		
					if (ch != '}') throw new FormatException();
				}
				else
				{
					Buffer[BufferLength++] = ch;
				}
			}
		}		
		
		static void ToString(FormatArgument value)
		{
			if (value.Type == typeof(int))
			{
				ToString(value.Int);
			}
			else if (value.Type == typeof(uint))
	        {
	        	ToString(value.UInt);	
	        }
			else if (value.Type == typeof(char))
			{
				ToString(value.Char);	
			}
			else if (value.Type == typeof(string))
			{
				ToString(value.String);	
			}
			else if (value.Type == null)
			{
				throw new IndexOutOfRangeException();
			}
			else
			{
				throw new NotSupportedException();
			}
		}
				
		static void ToString(uint value)
		{
			charLength = 0;
			charStart = Buffer.Length;			
			do
			{	
				var reminder = value % 10;
				Buffer[--charStart] = (char)(reminder + '0');
				value /= 10;
				charLength++;
			}
			while(value > 0);
		}		
		
		static void ToString(int value)
		{
			bool negative = false;
			if (value < 0)
			{
				value = -value;
				negative = true;
			}
			
			ToString((uint)value);
			
			if(negative) 
			{
				Buffer[--charStart] = '-';
				charLength++;
			}
		}	

		static void ToString(char value)
		{
			charLength = 1;
			charStart = Buffer.Length;
			Buffer[--charStart] = value;
		}	

		static void ToString(string value)
		{
			charLength = value.Length;
			charStart = Buffer.Length;
			for(int i = value.Length - 1 ; i >= 0 ; i--)
			{
				Buffer[--charStart] = value[i];
			}			
		}		
	}
}
