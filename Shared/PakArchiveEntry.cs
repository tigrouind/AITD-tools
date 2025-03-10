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
		internal byte CompressionFlags;
		internal ushort Offset;

		public PakArchiveEntry(PakArchive archive, int index)
		{
			Archive = archive;
			Index = index;
		}

		public PakArchiveEntry(byte[] data)
		{
			Data = data;
			CompressedSize = data.Length;
			UncompressedSize = data.Length;
			CompressionType = 0;
			CompressionFlags = 0;
			Extra = new byte[0];
		}

		public byte[] Read()
		{
			return Data ?? Archive.GetData(this);
		}
	}
}
