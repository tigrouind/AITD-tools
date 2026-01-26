namespace Shared
{
	public class PakArchiveEntry
	{
		internal readonly PakArchive Archive;
		public readonly int Index;

		public int CompressedSize;
		public int UncompressedSize;
		public byte CompressionType;
		public byte[] Extra;

		internal byte[] Data;
		internal byte[] UncompressedData;
		internal byte CompressionFlags;
		internal ushort Offset;

		public PakArchiveEntry(PakArchive archive, int index)
		{
			Archive = archive;
			Index = index;
		}

		public PakArchiveEntry(int uncompressedSize, byte[] extra, byte[] compressedData, byte compressionType)
		{
			Data = compressedData;
			CompressedSize = compressedData.Length;
			CompressionType = compressionType;
			UncompressedSize = uncompressedSize;
			CompressionFlags = 0;
			Extra = extra;
		}

		public byte[] Read()
		{
			return UncompressedData ?? Archive.UncompressData(this, Archive.ReadData(this));
		}

		public void Write(byte[] data)
		{
			Data = data;
			CompressedSize = data.Length;
		}
	}
}
