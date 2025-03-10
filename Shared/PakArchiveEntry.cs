using System.Linq;

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

		public PakArchiveEntry(byte[] data, byte[] extra, byte[] compressedData)
		{
			Data = compressedData;
			CompressedSize = compressedData.Length;
			CompressionType = (byte)(Enumerable.SequenceEqual(data, compressedData) ? 0 : 4);
			UncompressedSize = data.Length;
			CompressionFlags = 0;
			Extra = extra;
		}

		public byte[] Read()
		{
			return Data ?? Archive.GetData(this);
		}
	}
}
