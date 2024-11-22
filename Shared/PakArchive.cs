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
			stream.Seek(offsets[entry.Index] + entry.Offset.Length + entry.Extra.Length + 16, SeekOrigin.Begin);

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
			stream.Seek(offsets[entry.Index] + entry.Offset.Length + entry.Extra.Length + 16, SeekOrigin.Begin);
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

		public static void Save(string filePath, ICollection<PakArchiveEntry> entries)
		{
			using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
			{
				int offset = 4 + entries.Count * 4;
				writer.Seek(4, SeekOrigin.Begin);
				foreach (var entry in entries)
				{
					writer.Write(offset);
					offset += 16;
					offset += entry.Extra.Length;
					offset += entry.Offset.Length;
					offset += entry.CompressedSize;
				}

				foreach (var entry in entries)
				{
					WriteEntry(writer, entry);
					writer.Write(entry.Data);
				}
			}

			void WriteEntry(BinaryWriter writer, PakArchiveEntry entry)
			{
				writer.Write(entry.Extra.Length == 0 ? 0 : entry.Extra.Length + 4);
				writer.Write(entry.Extra);
				writer.Write(entry.CompressedSize);
				writer.Write(entry.UncompressedSize);
				writer.Write(entry.CompressionType);
				writer.Write(entry.CompressionFlags);
				writer.Write((ushort)entry.Offset.Length);
				writer.Write(entry.Offset);
			}
		}

		PakArchiveEntry GetEntry(int index)
		{
			var entry = new PakArchiveEntry() { Archive = this, Index = index };

			stream.Seek(offsets[entry.Index], SeekOrigin.Begin);

			int skip = reader.ReadInt32();
			entry.Extra = skip != 0 ? reader.ReadBytes(skip - 4) : Array.Empty<byte>(); //used in AITD3/masks (contains several 4 bytes data offsets)
			entry.CompressedSize = reader.ReadInt32();
			entry.UncompressedSize = reader.ReadInt32();
			entry.CompressionType = reader.ReadByte();
			entry.CompressionFlags = reader.ReadByte();
			int offset = reader.ReadUInt16();
			entry.Offset = reader.ReadBytes(offset); //usually 16 bytes, unused PKZip 1.10 header

			return entry;
		}

		public void Dispose()
		{
			reader.Close();
			stream.Close();
		}
	}
}
