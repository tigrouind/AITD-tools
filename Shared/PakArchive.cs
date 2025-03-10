using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;

namespace Shared
{
	public class PakArchive : IDisposable, IEnumerable<PakArchiveEntry>
	{
		[DllImport("UnPAK", CallingConvention = CallingConvention.Cdecl)]
		static extern void PAK_explode(byte[] srcBuffer, byte[] dstBuffer, uint compressedSize, uint uncompressedSize, ushort flags);

		readonly FileStream stream;
		readonly BinaryReader reader;
		int[] offsets;

		public PakArchive(string filename)
		{
			stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
			reader = new BinaryReader(stream);
			ReadEntries();
		}

		void ReadEntries()
		{
			stream.Seek(4, SeekOrigin.Begin);
			int offset = reader.ReadInt32();
			int count = offset / 4 - 1;

			offsets = new int[count];
			offsets[0] = offset;

			for (int i = 1; i < count; i++)
			{
				offset = reader.ReadInt32();
				if (offset == 0) //TIMEGATE
				{
					Array.Resize(ref offsets, i);
					break;
				}

				offsets[i] = offset;
			}
		}

		internal byte[] GetData(PakArchiveEntry entry)
		{
			stream.Seek(offsets[entry.Index] + entry.Offset + entry.Extra.Length + 16, SeekOrigin.Begin);

			var result = new byte[entry.UncompressedSize];
			switch (entry.CompressionType)
			{
				case 0: //uncompressed
					{
						stream.Read(result, 0, entry.CompressedSize);
						break;
					}

				case 1: //pak explode
					{
						var source = new byte[entry.CompressedSize];
						stream.Read(source, 0, entry.CompressedSize);
						PAK_explode(source, result, (uint)entry.CompressedSize, (uint)entry.UncompressedSize, entry.CompressionFlags);
						break;
					}

				case 4: //deflate
					{
						using (var deflateStream = new DeflateStream(stream, CompressionMode.Decompress, true))
						{
							deflateStream.Read(result, 0, entry.UncompressedSize);
						}
						break;
					}

				default:
					throw new NotSupportedException();
			}

			return result;
		}

		internal byte[] GetRawData(PakArchiveEntry entry)
		{
			stream.Seek(offsets[entry.Index] + entry.Offset + entry.Extra.Length + 16, SeekOrigin.Begin);
			var result = new byte[entry.CompressedSize];
			stream.Read(result, 0, entry.CompressedSize);
			return result;
		}

		public int Count
		{
			get
			{
				return offsets.Length;
			}
		}

		public PakArchiveEntry this[int index]
		{
			get
			{
				return GetEntry(index);
			}
		}

		public IEnumerator<PakArchiveEntry> GetEnumerator()
		{
			for (int i = 0; i < Count; i++)
			{
				yield return GetEntry(i);
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public static PakArchiveEntry[] Load(string filePath)
		{
			using (var pak = new PakArchive(filePath))
			{
				var entries = pak.ToArray();
				foreach (var entry in entries)
				{
					entry.Data = pak.GetRawData(entry);
				}
				return entries;
			}
		}

		public static byte[] Save(ICollection<PakArchiveEntry> entries)
		{
			using (var ms = new MemoryStream())
			using (var writer = new BinaryWriter(ms))
			{
				//header
				int offset = 4 + entries.Count * 4;
				writer.Seek(4, SeekOrigin.Begin);
				foreach (var entry in entries)
				{
					writer.Write(offset);
					offset += 16; //size of all fields (4+4+4+1+1+2)
					offset += entry.Offset;
					offset += entry.Extra.Length;
					offset += entry.CompressedSize;
				}

				//entries
				foreach (var entry in entries)
				{
					WriteEntry(writer, entry);
					writer.Seek(entry.Offset, SeekOrigin.Current); //expected to be zero
					writer.Write(entry.Data);
				}

				return ms.ToArray();
			}

			void WriteEntry(BinaryWriter writer, PakArchiveEntry entry)
			{
				writer.Write(entry.Extra.Length == 0 ? 0 : entry.Extra.Length + 4);
				writer.Write(entry.Extra);
				writer.Write(entry.CompressedSize);
				writer.Write(entry.UncompressedSize);
				writer.Write(entry.CompressionType);
				writer.Write(entry.CompressionFlags);
				writer.Write(entry.Offset);
			}
		}

		PakArchiveEntry GetEntry(int index)
		{
			//before this entry : might contains first 16 bytes of PKZip 1.0 header (skipped by the game)
			//after this entry: might contains end of PKZip header (skipped by offset field)

			var entry = new PakArchiveEntry(this, index);
			stream.Seek(offsets[entry.Index], SeekOrigin.Begin);

			int skip = reader.ReadInt32();
			//used in AITD2/3 masks (contains several 32-bit integers, all multiple of 4)
			entry.Extra = skip != 0 ? reader.ReadBytes(skip - 4) : Array.Empty<byte>();
			entry.CompressedSize = reader.ReadInt32();
			entry.UncompressedSize = reader.ReadInt32();
			entry.CompressionType = reader.ReadByte();
			entry.CompressionFlags = reader.ReadByte();
			entry.Offset = reader.ReadUInt16();

			return entry;
		}

		public void Dispose()
		{
			reader.Close();
			stream.Close();
		}
	}
}
