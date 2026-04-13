using Shared;
using System;
using System.Collections.Generic;
using System.IO;

namespace PAKExtract
{
	public static class MaskAITD2
	{
		public static void RenderMask(string filePath, bool[] mask)
		{
			Array.Clear(mask, 0, mask.Length);
			var buffer = File.ReadAllBytes(filePath);
			foreach (var offset in GetMaskOffsets(buffer))
			{
				RenderMask(buffer, mask, offset);
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

		public static void RenderMasksTimeGate(string folder, bool[] mask)
		{
			Array.Clear(mask, 0, mask.Length);
			foreach (var filePath in Directory.EnumerateFiles(folder, @"*.*"))
			{
				var buffer = File.ReadAllBytes(filePath);
				if (buffer.Length < 384000)
				{
					RenderMask(buffer, mask, 0);
				}
			}
		}

		static void RenderMask(byte[] buffer, bool[] pixels, int offset)
		{
			int width = buffer.ReadShort(offset + 0x08);
			int height = buffer.ReadShort(offset + 0x0A);
			if (width == 320 && height == 199)
			{
				return;
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
					for (int i = 0; i < copy; i++)
					{
						pixels[dest++] = true;
					}
				}

				dest += 320 - width;
			}

			return;
		}
	}
}
