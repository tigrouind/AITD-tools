using System.IO.Compression;

namespace MoviePlayer
{
	public class MovieWriter : IDisposable
	{
		readonly BinaryWriter writer;

		readonly byte[] previousMemory = new byte[640 * 1024];

		BinaryWriter? encoder;
		MemoryStream memoryStream = new();
		readonly List<int> deltas = [];
		int frames;
		int totalFrames;
		TimeSpan totalTime;

		public MovieWriter(string filePath)
		{
			writer = new(File.Create(filePath));
			WriteHeader();
		}

		void WriteHeader()
		{
			writer.Write(totalFrames);
			writer.Write(totalTime.Ticks);
		}

		public void WriteFrame(byte[] memory, TimeSpan currentTime)
		{
			encoder ??= new BinaryWriter(new BrotliStream(memoryStream, CompressionLevel.Optimal));
			encoder.Write(currentTime.Ticks);

			//check deltas
			deltas.Clear();
			for (int i = 0; i < memory.Length; i += MovieReader.DELTA_SIZE)
			{
				if (!MovieReader.FastEqual(memory, previousMemory, i, MovieReader.DELTA_SIZE))
				{
					deltas.Add(i);
				}
			}

			//write deltas
			encoder.Write((ushort)deltas.Count);
			foreach (var delta in deltas)
			{
				Array.Copy(memory, delta, previousMemory, delta, MovieReader.DELTA_SIZE);

				encoder.Write((ushort)(delta / MovieReader.DELTA_SIZE));
				encoder.Write(memory.AsSpan(delta, MovieReader.DELTA_SIZE));
			}

			//statistics
			totalFrames++;
			totalTime = currentTime;

			frames++;
			if (frames == 255)
			{
				Flush();
			}
		}

		void Flush()
		{
			writer.Write((byte)frames);

			encoder?.Flush();
			writer.Write((int)memoryStream.Length);
			writer.Write(memoryStream.ToArray(), 0, (int)memoryStream.Length);

			memoryStream = new MemoryStream();
			encoder?.Dispose();
			encoder = null;
			frames = 0;
		}

		public void Dispose()
		{
			Flush();
			writer.Write((byte)0); //end marker

			writer.BaseStream.Position = 0;
			WriteHeader();
			writer.Close();
		}
	}
}
