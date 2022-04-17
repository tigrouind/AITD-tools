using System;

namespace CacheViewer
{
	//GC friendly (unlike string.Format())	
	public static class StringFormat
	{			
		public readonly static StringBuffer Buffer = new StringBuffer();
		static readonly StringBuffer temp = new StringBuffer();
		static readonly StringBuffer args = new StringBuffer();
				
		public static void Format(string format, FormatArgument arg0, FormatArgument arg1, FormatArgument arg2, FormatArgument arg3, FormatArgument arg4, FormatArgument arg5, FormatArgument arg6)
		{			
			Buffer.Clear();
			int pos = 0;
			while (pos < format.Length)
			{
				char ch = format[pos++];
				if (ch == '{')
				{					
					int arg = 0;
					ch = format[pos++];
					
					if (ch < '0' || ch > '9') throw new FormatException();
					do
					{
	                    arg = arg * 10 + ch - '0';		                  
	                    ch = format[pos++];
	                } 
					while (ch >= '0' && ch <= '9');
					
					FormatArgument value;
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
						case 5:
							value = arg5;
							break;	
						case 6:
							value = arg6;
							break;									
						default:
							throw new FormatException();
					}
					
					bool neg = false;
					int width = 0;
					
					//padding (optional)
					if (ch == ',') 
					{		
						ch = format[pos++];
						if (ch == '-')
						{
							neg = true;
							ch = format[pos++];
						}
						
						if (ch < '0' || ch > '9') throw new FormatException();
						do
						{
		                    width = width * 10 + ch - '0';		                  
		                    ch = format[pos++];
		                } 
						while (ch >= '0' && ch <= '9');
						
						if (neg)
						{
							width = -width;
						}
					}
					
					//format arguments (optional)
					args.Clear();
					if (ch == ':')
					{						
						ch = format[pos++];
						if (ch == '}') throw new FormatException();
																		
						do
						{
							args.Append(ch);
							ch = format[pos++];
						}
						while(ch != '}');
					}
					
					if (ch != '}') throw new FormatException();
										
					ToString(value);
										
					//padding left
					if (width > 0)
					{
						Buffer.Append(' ', width - temp.Length);
					}
					
					Buffer.Append(temp);
	
					//padding right					
					if (width < 0)
					{
						Buffer.Append(' ', -width - temp.Length);
					}
				}
				else if(ch == '}')
				{
					throw new FormatException();
				}
				else
				{
					Buffer.Append(ch);
				}
			}
		}		
		
		static void ToString(FormatArgument value)
		{			
			temp.Clear();
			if (value.Type == typeof(int))
			{				
				int digits = ParseIntFormat();
				temp.Append(value.Int, digits);
			}
			else if (value.Type == typeof(uint))
	        {		
				int digits = ParseIntFormat();				
	        	temp.Append(value.UInt, digits);
	        }
			else if (value.Type == typeof(char))
			{
				temp.Append(value.Char);
			}
			else if (value.Type == typeof(string))
			{
				temp.Append(value.String);
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
				
		static int ParseIntFormat()
		{
			int length = 0;
			if (args.Length > 0)
			{		
				int pos = 0;
				char ch = args[pos++];
				if (ch != 'D') throw new FormatException();
				
				do
				{
					ch = args[pos++];
					if (ch < '0' || ch > '9') throw new FormatException();
					length = length * 10 + ch - '0';	
				}
				while(pos < args.Length);
			}
			
			return length;
		}		
	}
}
