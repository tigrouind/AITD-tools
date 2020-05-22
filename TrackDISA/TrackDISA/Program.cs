﻿using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using Shared;

namespace TrackDISA
{
	class Program
	{
		static readonly VarParserExt vars = new VarParserExt();

		public static int Main()
		{
			Directory.CreateDirectory("GAMEDATA");

			if(File.Exists(@"GAMEDATA\vars.txt"))
			{
				vars.Parse(@"GAMEDATA\vars.txt");
			}

			if(!Directory.Exists(@"GAMEDATA\LISTTRAK"))
			{
				Console.WriteLine("Folder LISTTRAK is required");
				return -1;
			}

			using (TextWriter writer = new StreamWriter("output.txt"))
			{
				Regex r = new Regex(@"[0-9a-fA-F]{8}\.[0-9a-zA-Z]{3}", RegexOptions.IgnoreCase);
				foreach(var file in Directory.GetFiles(@"GAMEDATA\LISTTRAK")
					.Where(x => r.IsMatch(Path.GetFileName(x)))
					.Select(x => new
					{
						FilePath = x,
						FileNumber = Convert.ToInt32(Path.GetFileNameWithoutExtension(x), 16)
					})
					.OrderBy(x => x.FileNumber))
				{
					writer.WriteLine("--------------------------------------------------");
					writer.WriteLine("#{0} {1}", file.FileNumber, vars.GetText("TRACKS", file.FileNumber, string.Empty));
					writer.WriteLine("--------------------------------------------------");
					Dump(file.FilePath, writer);
				}
			}

			return 0;
		}

		static void Dump(string filename, TextWriter writer)
		{
			int i = 0;
			byte[] allbytes = File.ReadAllBytes(filename);

			while(i < allbytes.Length)
			{
				writer.Write("{0,2}: ", i / 2);
				int macro = allbytes.ReadShort(i+0);
				i += 2;
				TrackEnum trackEnum = (TrackEnum)macro;
				switch (trackEnum)
				{

					case TrackEnum.WARP:
						writer.WriteLine("warp ROOM{0} {1} {2} {3}",
							allbytes.ReadShort(i+0),
							allbytes.ReadShort(i+2),
							allbytes.ReadShort(i+4),
							allbytes.ReadShort(i+6));
						i += 8;
						break;

					case TrackEnum.GOTO_POS:
						writer.WriteLine("goto position ROOM{0} {1} {2}",
							allbytes.ReadShort(i+0),
							allbytes.ReadShort(i+2),
							allbytes.ReadShort(i+4));
						i += 6;
						break;

					case TrackEnum.END:
						writer.WriteLine("end of track");
						return;

					case TrackEnum.REWIND:
						writer.WriteLine("rewind");
						return;

					case TrackEnum.MARK:
						writer.WriteLine("mark {0}", allbytes.ReadShort(i+0));
						i += 2;
						break;

					case TrackEnum.SPEED_4:
						writer.WriteLine("speed 4");
						break;

					case TrackEnum.SPEED_5:
						writer.WriteLine("speed 5");
						break;

					case TrackEnum.SPEED_0:
						writer.WriteLine("speed 0");
						break;

					case TrackEnum.ROTATE_X:
						writer.WriteLine("rotate {0}", allbytes.ReadShort(i+0) * 360 / 1024);
						i += 2;
						break;

					case TrackEnum.COLLISION_ENABLE:
						writer.WriteLine("enable collision");
						break;

					case TrackEnum.COLLISION_DISABLE:
						writer.WriteLine("disable collision");
						break;

					case TrackEnum.TRIGGERS_DISABLE:
						writer.WriteLine("disable triggers");
						break;

					case TrackEnum.TRIGGERS_ENABLE:
						writer.WriteLine("enable triggers");
						break;

					case TrackEnum.WARP_ROT:
						writer.WriteLine("warp ROOM{0} {1} {2} {3} {4}",
							allbytes.ReadShort(i+0),
							allbytes.ReadShort(i+2),
							allbytes.ReadShort(i+4),
							allbytes.ReadShort(i+6),
							allbytes.ReadShort(i+8));
						i += 10;
						break;

					case TrackEnum.STORE_POS:
						writer.WriteLine("store position");
						break;

					case TrackEnum.STAIRS_X:
					case TrackEnum.STAIRS_Z:
						writer.WriteLine("walk stairs on {0} {1} {2} {3}",
							trackEnum == TrackEnum.STAIRS_X ? "X" : "Z",
							allbytes.ReadShort(i+0),
							allbytes.ReadShort(i+2),
							allbytes.ReadShort(i+4));
						i += 6;
						break;

					case TrackEnum.ROTATE_XYZ:
						writer.WriteLine("rotate {0} {1} {2}",
							allbytes.ReadShort(i+0) * 360 / 1024,
							allbytes.ReadShort(i+2) * 360 / 1024,
							allbytes.ReadShort(i+4) * 360 / 1024);
						i += 6;
						break;

					default:
						throw new NotImplementedException(macro.ToString());
				}
			}

		}
	}
}