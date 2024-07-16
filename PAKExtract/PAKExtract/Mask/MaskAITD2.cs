using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PAKExtract
{
	public static class MaskAITD2
	{
		static readonly bool[] mask = new bool[64000];

		public static IEnumerable<int> RenderMasks(PakArchive pak, uint[] dest)
		{
			foreach (var entry in pak)
			{
				var buffer = entry.Read();
				var offsets = GetMaskOffsets(buffer);
				if (offsets.Any())
				{
					int colorIndex = 0;
					MaskUtils.ClearBitmap(dest);
					foreach (var offset in offsets)
					{
						Array.Clear(mask, 0, mask.Length);
						RenderMask(buffer, mask, offset);
						MaskUtils.FillBitmap(mask, dest, colorIndex++);
					}

					yield return entry.Index;
				}
			}
		}

		public static IEnumerable<int> RenderMasksTimeGate(PakArchive pak, uint[] dest)
		{
			bool any = false;
			int colorIndex = 0;

			MaskUtils.ClearBitmap(dest);

			foreach (var entry in pak.Where(x => x.UncompressedSize < 384000))
			{
				Array.Clear(mask, 0, mask.Length);
				var buffer = entry.Read();
				if (RenderMask(buffer, mask, 0))
				{
					MaskUtils.FillBitmap(mask, dest, colorIndex++);
					any = true;
				}
			}

			if (any)
			{
				yield return 0;
			}
			else
			{
				yield break;
			}
		}

		static List<int> GetMaskOffsets(byte[] buffer)
		{
			var offsets = new List<int>();

			var numZones = buffer.ReadInt(0) / 4;
			for (int i = 0; i < numZones; i++)
			{
				int start = buffer.ReadInt(i * 4);
				int numAreas = buffer.ReadInt(start) / 4;
				for (int j = 0; j < numAreas; j++)
				{
					int offset = start + buffer.ReadInt(start + j * 4);
					uint endMarker = buffer.ReadUnsignedInt(offset);
					if (endMarker != 0x6E656972)
					{
						offsets.Add(offset);
					}
				}
			}

			return offsets;
		}

		static bool RenderMask(byte[] buffer, bool[] pixels, int offset)
		{
			int width = buffer.ReadShort(offset + 0x08);
			int height = buffer.ReadShort(offset + 0x0A);
			if (width == 320 && height == 199)
			{
				return false;
			}

			int dest = buffer.ReadShort(offset) + buffer.ReadShort(offset + 2) * 320;
			offset += 12;
			for (int line = 0; line < height; line++)
			{
				int items = buffer.ReadShort(offset);
				offset += 2;

				for (int n = 0; n < items; n++)
				{
					int skip = buffer[offset];
					offset++;
					int copy = buffer[offset];
					offset++;

					dest += skip;
					for (int i = 0; i < copy;  i++)
					{
						pixels[dest++] = true;
					}
				}

				dest += 320 - width;
			}

			return true;
		}
	}
}
