using Shared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace PAKExtract
{
	public static class Svg
	{
		static readonly int[,] rotateMatrix = new int[,] { { 1, 0, 0, -1 }, { 0, 1, 1, 0 }, { -1, 0, 0, 1 }, { 0, -1, -1, 0 } };
		static readonly int[] rotateArgs = new int[] { 0, 90, 180, 270 };
		static readonly int[] cameraColors = { 0xFF8080, 0x789CF0, 0xB0DE6F, 0xCC66C0, 0x5DBAAB, 0xF2BA79, 0x8E71E3, 0x6ED169, 0xBF6080, 0x7CCAF7 };

		public static byte[] Export(string directory, HashSet<int> room, int rotate, int zoom, bool trigger, bool camera)
		{
			var rooms = new List<(int Index, int X, int Y,
				List<(int X, int Y, int Width, int Height, int Flags)> Colliders,
				List<(int X, int Y, int Width, int Height, int Flags)> Triggers,
				List<(int Id, List<(int X, int Y)> Polygon)> Cameras)>();

			int rotateIndex = Array.IndexOf(rotateArgs, rotate);
			if (rotateIndex == -1) rotateIndex = 0;

			LoadRooms();
			return Render();

			void LoadRooms()
			{
				var firstFile = Directory.EnumerateFiles(directory, "*.*").First();
				var buffer = File.ReadAllBytes(firstFile);
				int maxrooms = buffer.ReadInt(0) / 4;

				if (maxrooms >= 255) //timegate
				{
					int currentroom = 0;
					foreach (var filePath in Directory.EnumerateFiles(directory, "*.*"))
					{
						if (!room.Any() || room.Contains(currentroom))
						{
							LoadRoom(File.ReadAllBytes(firstFile), 0, currentroom);
						}

						currentroom++;
					}
				}
				else
				{
					var cameras = new HashSet<int>();
					for (int currentroom = 0; currentroom < maxrooms; currentroom++)
					{
						int roomheader = buffer.ReadInt(currentroom * 4);
						if (roomheader <= 0 || roomheader >= buffer.Length)
						{
							//all rooms parsed
							break;
						}

						if (!room.Any() || room.Contains(currentroom))
						{
							LoadRoom(buffer, roomheader, currentroom);

							if (camera)
							{
								int cameraCount = buffer.ReadShort(roomheader + 10);
								for (int cameraIndex = 0; cameraIndex < cameraCount; cameraIndex++)
								{
									int cameraID = buffer.ReadShort(roomheader + cameraIndex * 2 + 12);  //camera
									cameras.Add(cameraID);
								}
							}
						}
					}

					if (camera)
					{
						var secondFile = Directory.EnumerateFiles(directory, "*.*").Skip(1).First();
						buffer = File.ReadAllBytes(secondFile);
						foreach (var cameraId in cameras)
						{
							LoadCamera(buffer, cameraId);
						}
					}
				}
			}

			void LoadRoom(byte[] buffer, int roomheader, int currentroom)
			{
				(int X, int Y, int Z) rotateFunc((int X, int Y, int Z) vertex)
				{
					return (vertex.X * rotateMatrix[rotateIndex, 0] + vertex.Z * rotateMatrix[rotateIndex, 1], 0, vertex.X * rotateMatrix[rotateIndex, 2] + vertex.Z * rotateMatrix[rotateIndex, 3]);
				}

				var roomPosition = buffer.ReadVector(roomheader + 4);
				roomPosition = (roomPosition.X * 10, -roomPosition.Y * 10, -roomPosition.Z * 10);

				int i, totalpoint;
				var triggers = new List<(int X, int Y, int Width, int Height, int Flags)>();
				var colliders = new List<(int X, int Y, int Width, int Height, int Flags)>();
				var cameras = new List<(int Id, List<(int X, int Y)>)>();

				GetColliders();
				if (trigger)
				{
					GetTriggers();
				}

				if (colliders.Any() || triggers.Any())
				{
					roomPosition = rotateFunc(roomPosition);
					rooms.Add((currentroom, roomPosition.X, roomPosition.Z, colliders, triggers, cameras));
				}

				void GetColliders()
				{
					i = roomheader + buffer.ReadShort(roomheader + 0);
					totalpoint = buffer.ReadShort(i + 0);
					i += 2;

					for (int count = 0; count < totalpoint; count++)
					{
						var (lower, upper) = buffer.ReadBoundingBox(i + 0);

						int flags = buffer.ReadShort(i + 14);

						lower = rotateFunc(lower);
						upper = rotateFunc(upper);
						colliders.Add((Math.Min(lower.X, upper.X), Math.Min(lower.Z, upper.Z), Math.Abs(upper.X - lower.X), Math.Abs(upper.Z - lower.Z), flags));
						i += 16;
					}
				}

				void GetTriggers()
				{
					i = roomheader + buffer.ReadShort(roomheader + 2);
					totalpoint = buffer.ReadShort(i + 0);
					i += 2;

					for (int count = 0; count < totalpoint; count++)
					{
						var (lower, upper) = buffer.ReadBoundingBox(i + 0);
						int id = buffer.ReadShort(i + 12);
						int flags = buffer.ReadShort(i + 14);

						lower = rotateFunc(lower);
						upper = rotateFunc(upper);
						triggers.Add((Math.Min(lower.X, upper.X), Math.Min(lower.Z, upper.Z), Math.Abs(upper.X - lower.X), Math.Abs(upper.Z - lower.Z), flags));
						i += 16;
					}
				}
			}

			void LoadCamera(byte[] buffer, int cameraId)
			{
				(int X, int Y) rotateFunc((int X, int Y) vertex)
				{
					return (vertex.X * rotateMatrix[rotateIndex, 0] + vertex.Y * rotateMatrix[rotateIndex, 1], vertex.X * rotateMatrix[rotateIndex, 2] + vertex.Y * rotateMatrix[rotateIndex, 3]);
				}

				int cameraHeader = buffer.ReadInt(cameraId * 4);
				int numentries = buffer.ReadShort(cameraHeader + 0x12);
				for (int k = 0; k < numentries; k++)
				{
					int structSize = 12;

					int i = cameraHeader + 0x14 + k * structSize;
					int cameraRoomId = buffer.ReadShort(i + 0);

					i = cameraHeader + buffer.ReadShort(i + 4);
					int totalAreas = buffer.ReadShort(i + 0);
					i += 2;

					for (int g = 0; g < totalAreas; g++)
					{
						int totalPoints = buffer.ReadShort(i + 0);
						i += 2;

						var pts = new List<(int x, int y)>();
						for (int u = 0; u < totalPoints; u++)
						{
							int px = buffer.ReadShort(i + 0);
							int py = buffer.ReadShort(i + 2);
							pts.Add(rotateFunc((px * 10, py * 10)));
							i += 4;
						}
						var cameraRoom = rooms.FirstOrDefault(x => x.Index == cameraRoomId);
						if (cameraRoom != default) cameraRoom.Cameras.Add((cameraId, pts));
					}
				}
			}

			string GetColliderClass(int flags)
			{
				if ((flags & 4) != 0)
				{
					return "link";
				}

				return null;
			}

			string GetTriggerClass(int flags)
			{
				switch (flags)
				{
					case 9:
						return "custom";
					case 10:
						return "exit";
					default:
						return "room";
				}
			}

			byte[] Render()
			{
				var (xMin, xMax, yMin, yMax) = rooms
					.SelectMany(x => x.Colliders.Concat(x.Triggers).Concat(x.Cameras.SelectMany(y => y.Polygon).Select(y => (y.X, y.Y, Width: 0, Height: 0, Flags: 0)))
						.Select(r => (X: x.X + r.X, Y: x.Y + r.Y, r.Width, r.Height)))
					.Aggregate((xMin: int.MaxValue, xMax: int.MinValue, yMin: int.MaxValue, yMax: int.MinValue),
						(s, r) => (Math.Min(s.xMin, r.X), Math.Max(s.xMax, r.X + r.Width), Math.Min(s.yMin, r.Y), Math.Max(s.yMax, r.Y + r.Height)));

				const int padding = 5;
				float scale = 0.05f * zoom / 100.0f;
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
								".colliders rect { stroke: black; fill: white; fill-opacity: 0.8; stroke-width: 25; }\n" +
								".colliders rect.link { stroke: chocolate; }\n" +
								".triggers rect { fill-opacity:0.1; }\n" +
								".triggers rect.custom { fill: orange; }\n" +
								".triggers rect.exit { fill: yellow; }\n" +
								".triggers rect.room { fill: red; }\n" +
								".cameras polygon { fill-opacity: 0.5; }"),
							new XElement(ns + "g",
								new XAttribute("transform", $"translate({padding} {padding}) scale({scale} {scale}) translate({-xMin} {-yMin})"),
								new XElement(ns + "g",
									new XAttribute("class", "cameras"),
									rooms.Where(x => x.Cameras.Any())
										.Select(x =>
											new XElement(ns + "g",
												new XAttribute("transform", $"translate({x.X} {x.Y})"),
												x.Cameras.Select(c =>
													new XElement(ns + "polygon",
														new XAttribute("fill", $"#{cameraColors[c.Id % cameraColors.Length]:X6}"),
														new XAttribute("points", string.Join(" ", c.Polygon.Select(p => $"{p.X} {p.Y}")))
													)
												)
											)
										)
								),
								new XElement(ns + "g",
									new XAttribute("class", "colliders"),
									rooms.Where(x => x.Colliders.Any())
										.Select(x => (x.Index, x.X, x.Y, Rects: x.Colliders
											.OrderBy(c => (c.Flags & 4) == 0)
											.Select(c => (c.X, c.Y, c.Width, c.Height, Class: GetColliderClass(c.Flags)))))
										.Select(x => GetRoom(ns, x))
								),
								new XElement(ns + "g",
									new XAttribute("class", "triggers"),
									rooms.Where(x => x.Triggers.Any())
										.Select(x => (x.Index, x.X, x.Y, Rects: x.Triggers
											.Select(t => (t.X, t.Y, t.Width, t.Height, Class: GetTriggerClass(t.Flags)))))
										.Select(x => GetRoom(ns, x))
								)
							)
						);

						svg.Save(ms);
						return ms.ToArray();
					}
				}

				XElement GetRoom(XNamespace ns, (int Index, int X, int Y, IEnumerable<(int X, int Y, int Width, int Height, string Class)> Rects) r)
				{
					return new XElement(ns + "g",
						new XAttribute("id", $"room{r.Index}"),
						new XAttribute("transform", $"translate({r.X} {r.Y})"),
						r.Rects.Select(rect =>
							new XElement(ns + "rect",
								rect.Class != null ? new XAttribute("class", rect.Class) : null,
								new XAttribute("x", rect.X),
								new XAttribute("y", rect.Y),
								new XAttribute("width", rect.Width),
								new XAttribute("height", rect.Height)
							)
						)
					);
				}
			}
		}
	}
}
