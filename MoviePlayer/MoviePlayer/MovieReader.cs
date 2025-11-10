using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;

namespace MoviePlayer
{
	public class MovieReader
	{
		readonly byte[] previousMemory = new byte[640 * 1024];
		readonly byte[] buffer = new byte[640 * 1024];

		readonly BinaryReader reader;
		public readonly TimeSpan TotalTime;
		bool endOfMovie;

		public MovieReader(string filePath)
		{
			reader = new(File.Open(filePath, FileMode.Open));
			TotalTime = GetTotalTime();

			TimeSpan GetTotalTime()
			{
				var totalTime = TimeSpan.Zero;
				while (true)
				{
					var nextTime = TimeSpan.FromTicks(reader.ReadInt64());
					if (nextTime == TimeSpan.MaxValue)
					{
						break;
					}

					totalTime = nextTime;
					int compressedSize = reader.ReadInt32();
					reader.BaseStream.Position += compressedSize;
				}

				reader.BaseStream.Position = 0;
				return totalTime;
			}
		}

		public bool ReadFrame(byte[] memory, out TimeSpan currentTime)
		{
			if (endOfMovie)
			{
				currentTime = default;
				return false;
			}

			var nextTime = TimeSpan.FromTicks(reader.ReadInt64());
			if (nextTime == TimeSpan.MaxValue)
			{
				currentTime = default;
				endOfMovie = true;
				return false;
			}

			int compressedSize = reader.ReadInt32();
			reader.BaseStream.ReadExactly(buffer, 0, compressedSize);
			if (!BrotliDecoder.TryDecompress(buffer.AsSpan(0, compressedSize), previousMemory, out _))
			{
				throw new Exception("Decompress failed");
			}

			FastXor(memory, previousMemory, memory);
			currentTime = nextTime;

			return true;
		}

		public void Close()
		{
			reader.Close();
		}

		public long Position
		{
			get
			{
				return reader.BaseStream.Position;
			}

			set
			{
				endOfMovie = false;
				reader.BaseStream.Position = value;
			}
		}

		public byte[] PreviousFrame(byte[] memory)
		{
			FastXor(memory, previousMemory, buffer);
			return buffer;
		}

		public static void FastXor(byte[] a, byte[] b, byte[] result)
		{
			Debug.Assert(a.Length % Vector<byte>.Count == 0);
			for (int i = 0; i < a.Length; i += Vector<byte>.Count)
			{
				var va = new Vector<byte>(a, i);
				var vb = new Vector<byte>(b, i);
				var vr = Vector.Xor(va, vb);
				vr.CopyTo(result, i);
			}
		}
	}
}
