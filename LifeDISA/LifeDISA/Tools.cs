using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LifeDISA
{
	public static class Tools
	{
		public static IEnumerable<string> ReadLines(byte[] buffer, Encoding encoding)
		{
			using(var stream = new MemoryStream(buffer))
			using(var reader = new StreamReader(stream, encoding))
	      	{
				string line;					
				while((line = reader.ReadLine()) != null)
				{
					yield return line;
				}					      		
			}
		}
	}
}
