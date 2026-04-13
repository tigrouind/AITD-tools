using Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PAKExtract
{
	public static class MaskAITD1
	{
		static readonly int[] minX = new int[200];
		static readonly int[] maxX = new int[200];

		public static IEnumerable<int> GetMasks(string filePath, bool[] mask)
		{
			var buffer = System.IO.File.ReadAllBytes(filePath);
			uint cameraCount = buffer.ReadUnsignedInt(0) / 4 - 1; //does not always work

			for (int cameraID = 0; cameraID < cameraCount; cameraID++)
			{
				Array.Clear(mask, 0, mask.Length);

				foreach (var item in GetPolygons(buffer, cameraID))
				{
					foreach (var poly in item.Polygons)
					{
						RenderPolygon(poly, mask);
					}

					foreach (var plot in item.Plots)
					{
						mask[plot.X + plot.Y * 320] = true;
					}
				}

				yield return cameraID;
			}
		}

		public static bool RenderMask(bool[] mask, Image<Rgba32> dest)
		{
			bool any = false;
			for (int y = 0; y < 200; y++)
			{
				for (int x = 0; x < 320; x++)
				{
					if (!mask[x + y * 320])
					{
						dest[x, y] = new Rgba32();
					}
					else
					{
						any = true;
					}
				}
			}

			return any;
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

		static void RenderPolygon(List<(int X, int Y)> poly, bool[] mask)
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
					foreach (var (x, y) in Line(x1, y1, x2, y2))
					{
						if (x < minX[y]) minX[y] = x;
						if (x > maxX[y]) maxX[y] = x;
					}
				}

				for (int y = minY; y <= maxY; y++)
				{
					for (int x = minX[y]; x <= maxX[y]; x++)
					{
						mask[x + y * 320] = true;
					}
				}
			}
		}

		static IEnumerable<(int X, int Y)> Line(int x0, int y0, int x1, int y1) //Bresenham
		{
			int dx = Math.Abs(x1 - x0);
			int dy = Math.Abs(y1 - y0);

			int sx = x0 < x1 ? 1 : -1;
			int sy = y0 < y1 ? 1 : -1;

			if (dx == 0 && dy == 0)
			{
				yield break;
			}

			if (dy > dx)
			{
				for (int i = 0; i <= dy; i++)
				{
					int x = x0 + sx * (dx * i) / dy;
					int y = y0 + sy * i;

					yield return (x, y);
				}
			}
			else
			{
				for (int i = 0; i <= dx; i++)
				{
					int x = x0 + sx * i;
					int y = y0 + sy * (dy * i) / dx;

					yield return (x, y);
				}
			}
		}
	}
}
