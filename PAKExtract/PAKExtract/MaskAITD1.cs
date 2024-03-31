using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PAKExtract
{
	public static class MaskAITD1
	{
		static readonly bool[] mask = new bool[64000];
		static readonly int[] minX = new int[200];
		static readonly int[] maxX = new int[200];

		public static IEnumerable<int> RenderMasks(PakArchive pak, uint[] dest)
		{
			var buffer = pak[1].Read();
			uint cameraCount = buffer.ReadUnsignedInt(0) / 4 - 1; //does not always work
			for (int cameraID = 0; cameraID < cameraCount; cameraID++)
			{
				var items = GetPolygons(buffer, cameraID);
				if (items.Any())
				{
					int colorIndex = 0;
					MaskUtils.ClearBitmap(dest);
					foreach (var item in items.Skip(0))
					{
						Array.Clear(mask, 0, mask.Length);
						foreach (var poly in item.Polygons)
						{
							RenderPolygon(poly, mask);
						}

						foreach (var plot in item.Plots)
						{
							mask[plot.X + plot.Y * 320] = true;
						}

						MaskUtils.FillBitmap(mask, dest, colorIndex++);
					}

					yield return cameraID;
				}
			}
		}

		static List<(List<List<(int X, int Y)>> Polygons, List<(int X, int Y)> Plots)> GetPolygons(byte[] buffer, int cameraID)
		{
			var result = new List<(List<List<(int X, int Y)>> Polygons, List<(int X, int Y)> Plots)>();

			int cameraHeader = buffer.ReadInt(cameraID * 4);
			if (cameraHeader < 0 || cameraHeader >= buffer.Length)
			{
				return result;
			}

			int numEntries = buffer.ReadUnsignedShort(cameraHeader + 0x12);
			for (int n = 0; n < numEntries; n++)
			{
				int cameraEntryHeader = cameraHeader + buffer.ReadUnsignedShort(cameraHeader + 0x16 + n * 12);

				//overlays
				int numOverlays = buffer.ReadUnsignedShort(cameraEntryHeader);
				int overlayOffset = cameraEntryHeader + 2;

				for (int i = 0; i < numOverlays; i++)
				{
					int overlaySize = buffer.ReadUnsignedShort(overlayOffset);
					int polygonOffset = cameraEntryHeader + buffer.ReadUnsignedShort(overlayOffset + 2);
					overlayOffset += overlaySize * 8 + 4; //skip bounding boxes

					//polygons
					var numPolygons = buffer.ReadUnsignedShort(polygonOffset);
					polygonOffset += 2;

					var polygons = new List<List<(int X, int Y)>>();

					for (int j = 0; j < numPolygons; j++)
					{
						var numPoints = buffer.ReadUnsignedShort(polygonOffset);
						polygonOffset += 2;

						var polygon = new List<(int X, int Y)>();
						for (int k = 0; k < numPoints; k++)
						{
							var x = buffer.ReadUnsignedShort(polygonOffset);
							polygonOffset += 2;
							var y = buffer.ReadUnsignedShort(polygonOffset);
							polygonOffset += 2;
							polygon.Add((x, y));
						}

						polygons.Add(polygon);
					}

					var plots = new List<(int X, int Y)>();

					var count = buffer.ReadUnsignedShort(polygonOffset);
					polygonOffset += 2;
					for (int p = 0; p < count; p++)
					{
						var x = buffer.ReadUnsignedShort(polygonOffset);
						polygonOffset += 2;
						var y = buffer.ReadUnsignedShort(polygonOffset);
						polygonOffset += 2;

						plots.Add((x, y));
					}

					result.Add((polygons, plots));
				}
			}

			return result;
		}

		static void RenderPolygon(List<(int X, int Y)> poly, bool[] dest)
		{
			if (poly.Any())
			{
				int minY = poly.Min(x => x.Y);
				int maxY = poly.Max(x => x.Y);

				for (int y = minY; y <= maxY; y++)
				{
					minX[y] = int.MaxValue;
					maxX[y] = int.MinValue;
				}

				for (int i = 0; i < poly.Count; i++)
				{
					var (x1, y1) = poly[i];
					var (x2, y2) = poly[(i + 1) % poly.Count];
					Line(x1, y1, x2, y2);
				}

				for (int y = minY; y <= maxY; y++)
				{
					for (int x = minX[y]; x <= maxX[y]; x++)
					{
						dest[x + y * 320] = true;
					}
				}
			}
		}

		static void Line(int x, int y, int x2, int y2) //Bresenham
		{
			int w = x2 - x;
			int h = y2 - y;

			int dx1 = 0, dy1 = 0, dx2 = 0, dy2 = 0;
			if (w < 0) dx1 = -1; else if (w > 0) dx1 = 1;
			if (h < 0) dy1 = -1; else if (h > 0) dy1 = 1;
			if (w < 0) dx2 = -1; else if (w > 0) dx2 = 1;

			int longest = Math.Abs(w);
			int shortest = Math.Abs(h);
			if (longest <= shortest)
			{
				longest = Math.Abs(h);
				shortest = Math.Abs(w);
				if (h < 0) dy2 = -1;
				else if (h > 0) dy2 = 1;
				dx2 = 0;
			}

			int numerator = longest / 2;
			for (int i = 0; i <= longest; i++)
			{
				if (x < minX[y]) minX[y] = x;
				if (x > maxX[y]) maxX[y] = x;

				numerator += shortest;
				if (numerator >= longest)
				{
					numerator -= longest;
					x += dx1;
					y += dy1;
				}
				else
				{
					x += dx2;
					y += dy2;
				}
			}
		}
	}
}
