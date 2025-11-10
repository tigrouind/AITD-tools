using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;

namespace MoviePlayer
{
	public class MovieReader(string filePath) : IDisposable
	{
		public const int DELTA_SIZE = 512;

		readonly BinaryReader reader = new(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
		bool endOfMovie;
		BinaryReader? decoder;
		int frames, currentFrame;
		readonly Dictionary<int, (long Position, byte[] Memory)> keyFrames = [];

		public (int TotalFrame, TimeSpan TotalTime) ReadHeader()
		{
			return (reader.ReadInt32(), new TimeSpan(reader.ReadInt64()));
		}

		public bool ReadFrame(byte[] memory, out TimeSpan currentTime)
		{
			if (endOfMovie)
			{
				currentTime = TimeSpan.Zero;
				return false;
			}

			if (frames == 0)
			{
				keyFrames[currentFrame] = (reader.BaseStream.Position, memory.ToArray()); //fast seek

				frames = reader.ReadByte();
				if (frames == 0)
				{
					endOfMovie = true;
					currentTime = TimeSpan.Zero;
					return false;
				}

				int compressedSize = reader.ReadInt32();
				var buffer = reader.ReadBytes(compressedSize);
				decoder?.Dispose();
				decoder = new BinaryReader(new BrotliStream(new MemoryStream(buffer), CompressionMode.Decompress));
			}

			currentTime = new TimeSpan(decoder!.ReadInt64());

			//apply deltas
			int delta = decoder.ReadUInt16();
			for (int i = 0; i < delta; i++)
			{
				int position = decoder!.ReadUInt16();
				decoder!.BaseStream.ReadExactly(memory, position * DELTA_SIZE, DELTA_SIZE);
			}

			currentFrame++;
			frames--;

			return true;
		}


		public bool Seek(int frame, byte[] memory, out TimeSpan currentTime)
		{
			var keyFrame = keyFrames
				.Where(x => x.Key < frame) //because of 'smaller than' it will always force to read one frame
				.DefaultIfEmpty()
				.Select(x => (Frame: x.Key, x.Value.Position, x.Value.Memory))
				.MaxBy(x => x.Frame);

			reader.BaseStream.Position = keyFrame.Position;
			currentFrame = keyFrame.Frame;
			endOfMovie = false;
			frames = 0;

			if (keyFrame == default)
			{
				ReadHeader();
				Array.Clear(memory, 0, memory.Length);
			}
			else
			{
				Array.Copy(keyFrame.Memory, memory, keyFrame.Memory.Length);
			}

			currentTime = default;
			for (int i = 0; i < frame - keyFrame.Frame; i++) //one frame read at minimum
			{
				if (!ReadFrame(memory, out currentTime))
				{
					return false;
				}
			}

			return true;
		}

		public void Dispose()
		{
			reader.Close();
		}

		public static bool FastEqual(byte[] a, byte[] b, int offset, int length)
		{
			Debug.Assert(length % Vector<byte>.Count == 0);
			for (int i = offset; i < offset + length; i += Vector<byte>.Count)
			{
				var va = new Vector<byte>(a, i);
				var vb = new Vector<byte>(b, i);
				if (!Vector.EqualsAll(va, vb))
				{
					return false;
				}
			}

			return true;
		}
	}
}
