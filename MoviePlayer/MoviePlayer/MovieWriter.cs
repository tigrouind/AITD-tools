using System.IO.Compression;

namespace MoviePlayer
{
	public class MovieWriter(string filePath)
	{
		readonly BinaryWriter writer = new(File.Create(filePath));

		readonly byte[] previousMemory = new byte[640 * 1024];
		readonly byte[] buffer = new byte[640 * 1024];

		public void WriteFrame(byte[] memory, TimeSpan currentTime)
		{
			writer.Write(currentTime.Ticks);

			MovieReader.FastXor(memory, previousMemory, buffer);
			Array.Copy(memory, previousMemory, memory.Length);

			if (!BrotliEncoder.TryCompress(buffer, memory, out var compressedSize))
			{
				throw new Exception("Compression failed");
			}

			writer.Write(compressedSize);
			writer.Write(memory, 0, compressedSize);
		}

		public void Close()
		{
			writer.Write(TimeSpan.MaxValue.Ticks); //end marker
			writer.Close();
		}
	}
}
