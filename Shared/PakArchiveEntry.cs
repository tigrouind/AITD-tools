namespace Shared
{
	public class PakArchiveEntry
	{
		public int Index;
		public int CompressedSize;
		public int UncompressedSize;
		public byte CompressionType;

		internal PakArchive Archive;
		internal byte CompressionFlags;
		internal int Offset;

		public byte[] Read()
		{
			return Archive.GetData(this);
		}
	}
}
