using Shared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace PAKExtract
{
	public static class Svg
	{
		static readonly int[,] rotateMatrix = new int[,] { { 1, 0, 0, -1 }, { 0, 1, 1, 0 }, { -1, 0, 0, 1 }, { 0, -1, -1, 0 } };
		static readonly int[] rotateArgs = new int[] { 0, 90, 180, 270 };

		public static byte[] Export(PakArchive pak, int[] rooms, int rotate, bool color, GameVersion version)
		{
			var result = new List<(int Index, int X, int Y, List<(int X, int Y, int Width, int Height, int Flags)> Rects)>();
			int rotateIndex = Array.IndexOf(rotateArgs, rotate);
			if (rotateIndex == -1) rotateIndex = 0;

			LoadRooms();
			return Render();

			void LoadRooms()
			{
				if (version == GameVersion.TIMEGATE)
				{
					int currentroom = 0;
					foreach (var entry in pak)
					{
						if (!rooms.Any() || rooms.Contains(currentroom))
						{
							LoadRoom(entry.Read(), 0, entry.Index);
						}

						currentroom++;
					}
				}
				else
				{
					var buffer = pak[0].Read();
					int maxrooms = buffer.ReadInt(0) / 4;
					for (int currentroom = 0; currentroom < maxrooms; currentroom++)
					{
						int roomheader = buffer.ReadInt(currentroom * 4);
						if (roomheader <= 0 || roomheader >= buffer.Length)
						{
							//all rooms parsed
							break;
						}

						if (!rooms.Any() || rooms.Contains(currentroom))
						{
							LoadRoom(buffer, roomheader, currentroom);
						}
					}
				}
			}

			void LoadRoom(byte[] buffer, int roomheader, int currentroom)
			{
				(int X, int Y, int z) rotateFunc((int x, int y, int z) vertex)
				{
					return (vertex.x * rotateMatrix[rotateIndex, 0] + vertex.z * rotateMatrix[rotateIndex, 1], 0, vertex.x * rotateMatrix[rotateIndex, 2] + vertex.z * rotateMatrix[rotateIndex, 3]);
				}

				var roomPosition = buffer.ReadVector(roomheader + 4);
				roomPosition = (roomPosition.X * 10, -roomPosition.Y * 10, -roomPosition.Z * 10);

				//colliders
				int i = roomheader + buffer.ReadShort(roomheader + 0);
				int totalpoint = buffer.ReadShort(i + 0);
				i += 2;

				if (totalpoint > 0)
				{
					var rects = new List<(int X, int Y, int Width, int Height, int Flags)>();

					for (int count = 0; count < totalpoint; count++)
					{
						var (lower, upper) = buffer.ReadBoundingBox(i + 0);

						int flags = buffer.ReadShort(i + 14);

						lower = rotateFunc(lower);
						upper = rotateFunc(upper);
						rects.Add((Math.Min(lower.X, upper.X), Math.Min(lower.Z, upper.Z), Math.Abs(upper.X - lower.X), Math.Abs(upper.Z - lower.Z), flags));
						i += 16;
					}

					roomPosition = rotateFunc(roomPosition);
					result.Add((currentroom, roomPosition.X, roomPosition.Z, rects));
				}
			}

			byte[] Render()
			{
				var (xMin, xMax, yMin, yMax) = result
					.SelectMany(x => x.Rects.Select(r => (X: x.X + r.X, Y: x.Y + r.Y, r.Width, r.Height)))
					.Aggregate((xMin: int.MaxValue, xMax: int.MinValue, yMin: int.MaxValue, yMax: int.MinValue),
						(s, r) => (Math.Min(s.xMin, r.X), Math.Max(s.xMax, r.X + r.Width), Math.Min(s.yMin, r.Y), Math.Max(s.yMax, r.Y + r.Height)));

				const int padding = 5;
				float scale = 0.05f;
				return WriteSvg();

				byte[] WriteSvg()
				{
					Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
					using (MemoryStream ms = new MemoryStream())
					{
						XNamespace ns = "http://www.w3.org/2000/svg";
						var svg = new XElement(ns + "svg",
							new XAttribute("width", Math.Ceiling((xMax - xMin) * scale + padding * 2)),
							new XAttribute("height", Math.Ceiling((yMax - yMin) * scale + padding * 2)),
							new XElement(ns + "style",
								"rect { stroke: black; fill: white; stroke-width: 20; }"),
							color ? new XElement(ns + "style",
								@"rect { fill:lightgray; }
								.floor { fill:darkgray; }
								.link { fill:teal; }
								.interact { fill:blue; }"
							) : null,
							new XElement(ns + "g",
								new XAttribute("transform", $"translate({padding} {padding}) scale({scale} {scale}) translate({-xMin} {-yMin})"),
								result.Select(room =>
									new XElement(ns + "g",
										new XAttribute("id", $"room{room.Index}"),
										new XAttribute("transform", $"translate({room.X} {room.Y})"),
										room.Rects.Select(rect =>
										{
											var className = GetClassName(rect.Flags);
											return new XElement(ns + "rect",
												new XAttribute("x", rect.X),
												new XAttribute("y", rect.Y),
												new XAttribute("width", rect.Width),
												new XAttribute("height", rect.Height),
												color && className != null ? new XAttribute("class", className) : null
											);
										})
									)
								)
							)
						);

						svg.Save(ms);
						return ms.ToArray();
					}
				}

				string GetClassName(int flags)
				{
					if ((flags & 2) != 0)
					{
						return "floor";
					}
					else if ((flags & 4) != 0)
					{
						return "link";
					}
					else if ((flags & 8) != 0)
					{
						return "interact";
					}

					return null;
				}
			}
		}
	}
}
