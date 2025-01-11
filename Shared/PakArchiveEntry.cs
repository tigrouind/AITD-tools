namespace Shared
{
	public class PakArchiveEntry
	{
		public int Index;
		public int CompressedSize;
		public int UncompressedSize;
		public byte CompressionType;
		public byte[] Extra;

		internal byte[] Data;
		internal PakArchive Archive;
		internal byte CompressionFlags;
		internal ushort Offset;

		public byte[] Read()
		{
			if (Data != null)
			{
				return Data;
			}
			else
			{
				return Archive.GetData(this);
			}
		}

		public void Write(byte[] data)
		{
			Data = data;
			CompressedSize = data.Length;
			UncompressedSize = data.Length;
			CompressionType = 0;
			CompressionFlags = 0;
		}
	}
}
