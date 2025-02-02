using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared;

namespace TrackDISA
{
	class Program
	{
		static readonly VarParserForScript vars = new VarParserForScript();

		static readonly (GameVersion Version, TrackEnum[] TrackMacro)[] gameConfigs =
		{
			(GameVersion.AITD1        , MacroTable.TrackA),
			(GameVersion.AITD1_FLOPPY , MacroTable.TrackA),
			(GameVersion.AITD1_DEMO   , MacroTable.TrackA),
			(GameVersion.AITD2        , MacroTable.TrackB),
			(GameVersion.AITD2_DEMO   , MacroTable.TrackB),
			(GameVersion.AITD3        , MacroTable.TrackB),
			(GameVersion.JACK         , MacroTable.TrackB),
			(GameVersion.TIMEGATE     , MacroTable.TrackB),
			(GameVersion.TIMEGATE_DEMO, MacroTable.TrackB)
		};

		static int Main(string[] args)
		{
			return CommandLine.ParseAndInvoke(args, new Func<GameVersion?, bool, string, int>(Run));
		}

		static int Run(GameVersion? version, bool verbose, string output = "tracks.vb")
		{
			Directory.CreateDirectory("GAMEDATA");

			if (File.Exists(@"GAMEDATA\vars.txt"))
			{
				vars.Load(@"GAMEDATA\vars.txt", VarEnum.TRACKS);
			}

			if (!File.Exists(@"GAMEDATA\LISTTRAK.PAK"))
			{
				return -1;
			}

			var config = gameConfigs.FirstOrDefault(x => x.Version == version);
			if (version == null || config == default)
			{
				var versions = string.Join("|", gameConfigs.Select(x => x.Version.ToString().ToLowerInvariant()));
				Console.WriteLine($"Usage: TrackDISA -version {{{versions}}} [-output] [-verbose]");
				return -1;
			}

			using (var writer = new StreamWriter(output))
			using (var pak = new PakArchive(@"GAMEDATA\LISTTRAK.PAK"))
			{
				foreach (var entry in pak)
				{
					writer.WriteLine("'--------------------------------------------------");
					writer.WriteLine("'#{0} {1}", entry.Index, vars.GetText(VarEnum.TRACKS, entry.Index, string.Empty));
					writer.WriteLine("'--------------------------------------------------");
					Dump(entry.Read(), writer, config.TrackMacro, verbose);
				}
			}

			return 0;
		}

		static void Dump(byte[] allbytes, TextWriter writer, TrackEnum[] trackMacro, bool verbose)
		{
			int i = 0;

			var result = new List<(string Result, int Position, int Size)>();
			bool quit = false;
			while (i < allbytes.Length && !quit)
			{
				int pos = i;
				int macro = allbytes.ReadShort(i + 0);
				i += 2;

				string output;
				TrackEnum trackEnum = trackMacro[macro];
				switch (trackEnum)
				{
					case TrackEnum.WARP:
						output = string.Format("warp(ROOM{0} {1}, {2}, {3})",
							allbytes.ReadShort(i + 0),
							allbytes.ReadShort(i + 2),
							allbytes.ReadShort(i + 4),
							allbytes.ReadShort(i + 6));
						i += 8;
						break;

					case TrackEnum.GOTO_POS:
						output = string.Format("goto_position(ROOM{0}, {1}, {2})",
							allbytes.ReadShort(i + 0),
							allbytes.ReadShort(i + 2),
							allbytes.ReadShort(i + 4));
						i += 6;
						break;

					case TrackEnum.END:
						output = "stop()";
						quit = true;
						break;

					case TrackEnum.REWIND:
						output = "rewind()";
						quit = true;
						break;

					case TrackEnum.MARK:
						output = string.Format("mark({0})", allbytes.ReadShort(i + 0));
						i += 2;
						break;

					case TrackEnum.SPEED_4:
						output = "speed(4)";
						break;

					case TrackEnum.SPEED_5:
						output = "speed(5)";
						break;

					case TrackEnum.SPEED_0:
						output = "speed(0)";
						break;

					case TrackEnum.ROTATE_X:
						output = string.Format("rotate({0})", allbytes.ReadShort(i + 0) * 360 / 1024);
						i += 2;
						break;

					case TrackEnum.COLLISION_ENABLE:
						output = "collision(1)";
						break;

					case TrackEnum.COLLISION_DISABLE:
						output = "collision(0)";
						break;

					case TrackEnum.TRIGGERS_ENABLE:
						output = "triggers(1)";
						break;

					case TrackEnum.TRIGGERS_DISABLE:
						output = "triggers(0)";
						break;

					case TrackEnum.WARP_ROT:
						output = string.Format("warp(ROOM{0}, {1}, {2}, {3}, {4})",
							allbytes.ReadShort(i + 0),
							allbytes.ReadShort(i + 2),
							allbytes.ReadShort(i + 4),
							allbytes.ReadShort(i + 6),
							allbytes.ReadShort(i + 8));
						i += 10;
						break;

					case TrackEnum.STORE_POS:
						output = "store_position()";
						break;

					case TrackEnum.STAIRS_X:
					case TrackEnum.STAIRS_Z:
						output = string.Format("walk_stairs_on_{0}({1}, {2}, {3})",
							trackEnum == TrackEnum.STAIRS_X ? "x" : "z",
							allbytes.ReadShort(i + 0),
							allbytes.ReadShort(i + 2),
							allbytes.ReadShort(i + 4));
						i += 6;
						break;

					case TrackEnum.ROTATE_XYZ:
						output = string.Format("rotate({0}, {1}, {2})",
							allbytes.ReadShort(i + 0) * 360 / 1024,
							allbytes.ReadShort(i + 2) * 360 / 1024,
							allbytes.ReadShort(i + 4) * 360 / 1024);
						i += 6;
						break;

					case TrackEnum.DUMMY:
						output = "dummy()";
						break;

					default:
						throw new NotSupportedException(trackEnum.ToString());
				}

				result.Add((output, pos, i - pos));
			}

			int maxLength = result.Max(x => x.Position / 2).ToString().Length;
			int padding = result.Max(x => x.Size) / 2;

			foreach (var item in result)
			{
				if (verbose)
				{
					var bytesData = Enumerable.Range(0, item.Size / 2)
						.Select(x => allbytes.ReadUnsignedShortSwap(item.Position + x * 2)
						.ToString("x4"));

					var byteInfo = string.Join("_", bytesData).PadLeft(padding * 4 + (padding - 1), ' ');

					writer.Write($"{byteInfo} ");
				}

				writer.WriteLine($"{(item.Position / 2).ToString().PadLeft(maxLength)}: {item.Result}");
			}
		}
	}
}