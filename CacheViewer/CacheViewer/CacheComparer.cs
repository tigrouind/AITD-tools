using System;
using System.IO;
using Shared;

namespace CacheViewer
{
	public class CacheComparer
	{			
		readonly MemoryStream memoryStream = new MemoryStream();		
		readonly BinaryWriter binaryWriter;	
		byte[] previousData = new byte[0];

		public CacheComparer()
		{	
			binaryWriter = new BinaryWriter(memoryStream);
		}
		
		public bool NeedRefresh(Cache[] cache)
		{
			long length;
			byte[] data = DumpData(cache, out length);
						
			bool isEqual = data.IsEqual(previousData, 0, length);			
			if(!isEqual)
			{		
				if (previousData.Length != data.Length)
				{
					Array.Resize(ref previousData, data.Length);
				}				
				Array.Copy(data, previousData, length);
			}			
			
			return !isEqual;
		}
		
		byte[] DumpData(Cache[] cache, out long length)
		{
			memoryStream.Position = 0;
			foreach (var ch in cache)
			{
				if(ch.Name != null)
				{
					binaryWriter.Write(ch.Name);
					binaryWriter.Write(ch.MaxFreeData);
					binaryWriter.Write(ch.SizeFreeData);
					binaryWriter.Write(ch.NumUsedEntry);
					binaryWriter.Write(ch.NumMaxEntry);				
					binaryWriter.Write(ch.Entries.Count);		

					foreach(var entry in ch.Entries)
					{
						binaryWriter.Write(entry.Id);			
						binaryWriter.Write(entry.Size);			
						binaryWriter.Write(entry.Time / 60);			
						binaryWriter.Write(entry.Added);
						binaryWriter.Write(entry.Removed);
						binaryWriter.Write(entry.Touched);
					}
				}
			}	
			
			length = memoryStream.Position;
			return memoryStream.GetBuffer();
		}			
	}
}
