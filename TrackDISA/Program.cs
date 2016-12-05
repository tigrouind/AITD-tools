using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

namespace TRACKDISA
{
	class Program
	{
		public static void Main(string[] args)
		{		
			using (TextWriter writer = new StreamWriter("output.txt"))
			{	
				Regex r = new Regex(@"[0-9a-fA-F]{8}\.[0-9a-zA-Z]{3}", RegexOptions.IgnoreCase);				
				foreach(var file in System.IO.Directory.GetFiles(@"E:\DARK\data\LISTTRAK")
			        .Where(x => r.IsMatch(Path.GetFileName(x)))
	        		.Select(x => new
	                {
	               		FilePath = x,
	               		FileNumber = Convert.ToInt32(Path.GetFileNameWithoutExtension(x), 16)
	            	})
		        	.OrderBy(x => x.FileNumber))				    
				{		        	
					writer.WriteLine("#{0} --------------------------------------------------", file.FileNumber);
					Dump(file.FilePath, writer);
				}
			}
		}
		
		public static short ReadShort(byte a, byte b)
		{
			unchecked
			{
				return (short)(a | b << 8);
			}
		}
			
		public static void Dump(string filename, TextWriter writer)
		{
			int i = 0;
			byte[] allbytes = System.IO.File.ReadAllBytes(filename);
			
			while(i < allbytes.Length)
			{
				writer.Write("{0,2}: ", i / 2);
				int macro = ReadShort(allbytes[i+0], allbytes[i+1]);
				i += 2;
				switch (macro)
				{
						
					case 0x00:
						writer.WriteLine("warp ROOM{0} {1} {2} {3}", 
						                 ReadShort(allbytes[i+0], allbytes[i+1]),
						                 ReadShort(allbytes[i+2], allbytes[i+3]),
						                 ReadShort(allbytes[i+4], allbytes[i+5]),
						                 ReadShort(allbytes[i+6], allbytes[i+7]));
					    i += 8;
						break;							
						
					case 0x01:
						writer.WriteLine("goto position ROOM{0} {1} {2}", 
						                 ReadShort(allbytes[i+0], allbytes[i+1]),
						                 ReadShort(allbytes[i+2], allbytes[i+3]),
						                 ReadShort(allbytes[i+4], allbytes[i+5]));		
					    i += 6;
						break;	
						
					case 0x02: //end
						writer.WriteLine("end of track");
						return;							
						
					case 0x03:
						writer.WriteLine("rewind");					
						return;
						
					case 0x04:
						writer.WriteLine("mark {0}", ReadShort(allbytes[i+0], allbytes[i+1]));					
						i += 2;
						break;	
						
					case 0x05:
						writer.WriteLine("speed 4");					
						break;	
						
					case 0x06:
						writer.WriteLine("speed 5");					
						break;	

					case 0x07:
						writer.WriteLine("speed 0");					
						break;	

					case 0x09:
						writer.WriteLine("rotate {0}", ReadShort(allbytes[i+0], allbytes[i+1]) * 360 / 1024);					
						i += 2;
						break;							
						
					case 0x0A:
						writer.WriteLine("disable collision");					
						break;		

					case 0x0B:
						writer.WriteLine("enable collision");					
						break;								
						
					case 0x0D:
						writer.WriteLine("disable flag 0x0040");					
						break;
						
					case 0x0E:
						writer.WriteLine("enable flag 0x0040");					
						break;	

					case 0x0F:
						writer.WriteLine("warp ROOM{0} {1} {2} {3} {4}", 
						                 ReadShort(allbytes[i+0], allbytes[i+1]),
						                 ReadShort(allbytes[i+2], allbytes[i+3]),
						                 ReadShort(allbytes[i+4], allbytes[i+5]),
						                 ReadShort(allbytes[i+6], allbytes[i+7]),
						                 ReadShort(allbytes[i+8], allbytes[i+9]));
					    i += 10;
						break;									
						
					case 0x10:
						writer.WriteLine("store position");					
						break;
						
					case 0x11:
					case 0x12:
						writer.WriteLine("walk stairs {0} {1} {2}",
						                 ReadShort(allbytes[i+0], allbytes[i+1]),
						                 ReadShort(allbytes[i+2], allbytes[i+3]),
						                 ReadShort(allbytes[i+4], allbytes[i+5]));					
						i += 6;
						break;
												
					case 0x13:						
						writer.WriteLine("rotate {0} {1} {2}", 
						                 ReadShort(allbytes[i+0], allbytes[i+1]) * 360 / 1024,
						                 ReadShort(allbytes[i+2], allbytes[i+3]) * 360 / 1024,
						                 ReadShort(allbytes[i+4], allbytes[i+5]) * 360 / 1024);
						i += 6;
						break;

					default: 
						throw new NotImplementedException(macro.ToString());
				}
			}
			
		}
	}
}