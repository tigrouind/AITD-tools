using System;
using System.IO;
using System.Security.Cryptography;
using Shared;

namespace CacheViewer
{
	public class CacheComparer
	{			
		readonly MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
		readonly MemoryStream memoryStream = new MemoryStream();		
		readonly BinaryWriter binaryWriter;	
		byte[] previousHash;

		public CacheComparer()
		{	
			binaryWriter = new BinaryWriter(memoryStream);
		}
		
		public bool NeedRefresh(Cache[] cache)
		{
			byte[] hash = ComputeHash(cache);
			bool result = !hash.IsEqual(previousHash);
			previousHash = hash;
			return result;
		}
		
		byte[] ComputeHash(Cache[] cache)
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
			
			return md5.ComputeHash(memoryStream.GetBuffer());
		}			
	}
}
