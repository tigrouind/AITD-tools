using System.Collections.Generic;

namespace VarsViewer
{
	public static class Actors
	{
		public static Column[] Instance =
		[
			new() { Name = "Id" },
			new() {
				Name = "Flags",
				Columns = [
					new() { Type = ColumnType.FLAGS, Offset = 4, Values = new Dictionary<int, string> { { 1, "anim" }, { 4, "draw" }, { 8, "back" }, { 16, "push" }, { 32, "special" }, { 64, "trig" }, { 128, "pick" }, { 256, "fall" } } },
					new() { Name = "Col", Offset = 6, Values = new Dictionary<int, string> { { 0, "N" }, { 1, "Y" } } }
				]
			},
			new() { Name = "Room", Type = ColumnType.ROOM, Offset = 46, IncludeZero = true },
			new() {
				Name = "Room",
				Columns = [
					new() { Name = "X", Offset = 28, IncludeZero = true },
					new() { Name = "Y", Offset = 30 },
					new() { Name = "Z", Offset = 32, IncludeZero = true }
				]
			},
			new() {
				Name = "World",
				Columns = [
					new() { Name = "X", Offset = 34, IncludeZero = true },
					new() { Name = "Y", Offset = 36 },
					new() { Name = "Z", Offset = 38, IncludeZero = true }
				]
			},
			new() {
				Name = "ZV",
				Columns = [
					new() { Name = "X", Type = ColumnType.ZVPOS, Offset = 8, IncludeZero = true },
					new() { Name = "Y", Type = ColumnType.ZVPOS, Offset = 12, IncludeZero = true },
					new() { Name = "Z", Type = ColumnType.ZVPOS, Offset = 16, IncludeZero = true },
					new() { Name = "SX", Type = ColumnType.ZVSIZE, Offset = 8, IncludeZero = true },
					new() { Name = "SY", Type = ColumnType.ZVSIZE, Offset = 12, IncludeZero = true },
					new() { Name = "SZ", Type = ColumnType.ZVSIZE,  Offset = 16, IncludeZero = true }
				]
			},
			new() {
				Name = "Mod",
				Columns = [
					new() { Name = "X", Offset = 90 },
					new() { Name = "Y", Offset = 92 },
					new() { Name = "Z", Offset = 94 }
				]
			},
			new() {
				Name = "Angle",
				Columns = [
					new() { Name = "X", Type = ColumnType.ANGLE, Offset = 40, Condition = 2 },
					new() { Name = "Dir", Offset = 114, Values = new Dictionary <int, string> { { -1, "▼" }, { 32767, "▼" }, { 1, "▲" } }, Condition = 2 },
					new() { Name = "Y",  Type = ColumnType.ANGLE, Offset = 42, Condition = 2 },
					new() { Name = "Z", Type = ColumnType.ANGLE, Offset = 44, Condition = 2 }
				]
			},
			new() {
				Name = "Life",
				Columns = [
					new() { Type = ColumnType.LIFE, Offset = 52, IncludeZero = true },
					new() { Name = "Mode", Offset = 50, Values = new Dictionary <int, string> { { 0, "floor" }, { 1, "room" }, { 2, "camera" } }, IncludeZero = true, Condition = 52 }
				]
			},
			new() { Name = "Body",  Type = ColumnType.BODY, Offset = 2, IncludeZero = true },
			new() {
				Name = "Anim",
				Columns = [
					new() { Type = ColumnType.ANIM, Offset = 62, IncludeZero = true },
					new() { Name = "Type", Offset = 64, Values = new Dictionary<int, string> { { 0, "once" }, { 1, "repeat" }, { 2, "uninter" } }, IncludeZero = true, Condition = 62 },
					new() { Name = "Next", Type = ColumnType.ANIM, Offset = 66  }
				]
			},
			new() {
				Name = "NewAnim",
				Columns = [
					new() { Name = "Anim", Type = ColumnType.ANIM, Offset = 68  },
					new() { Name = "Type", Offset = 70, Values = new Dictionary<int, string> { { 0, "once" }, { 1, "repeat" }, { 2, "uninter" } }, Condition = 68 },
					new() { Name = "Next",  Type = ColumnType.ANIM, Offset = 72 }
				]
			},
			new() {
				Name = "Key",
				Columns = [
					new() { Name = "Frame", Offset = 74, IncludeZero = true, Condition = 62 },
					new() { Name = "Total", Offset = 76, Condition = 62 },
					new() { Name = "End",   Offset = 78, Condition = 62 },
					new() { Name = "End", Offset = 80, Condition = 62 }
				]
			},
			new() { Name = "Speed", Offset = 116, Values = new Dictionary <int, string> { { -1, "back" }, { 0, "idle" }, { 1, "walk" }, { 2, "walk" }, { 3, "walk" }, { 4, "walk" }, { 5, "run" } } },
			new() { Name = "Fall", Offset = 104, Values = new Dictionary <int, string> { { 0, "N" }, { 1, "Y" } } },
			new() {
				Name = "Chrono",
				Columns = [
					new() { Name = "Room", Type = ColumnType.TIME, Offset = 54 },
					new() { Type = ColumnType.TIME, Offset = 58 }
				]
			},
			new() {
				Name = "Track",
				Columns = [
					new() { Name = "Number", Type = ColumnType.TRACK, Offset = 84, IncludeZero = true },
					new() { Name = "Mode", Offset = 82, Values = new Dictionary <int, string> { { 0, "none" }, { 1, "manual" }, { 2, "follow" }, { 3, "track" } } },
					new() { Name = "Mark", Offset = 86, Condition = 84 }
				]
			},
			new() {
				Name = "Angle",
				Columns = [
					new() { Name = "Offset", Type = ColumnType.ZVSIZE, Offset = 106, IncludeZero = true, Condition = 110 },
					new() { Name = "Time", Type = ColumnType.TIME2, Offset = 112, Condition = 110 },
					new() { Name = "Param", Offset = 110 }
				]
			},
			new() {
				Name = "Fall",
				Columns = [
					new() { Name = "Offset", Type = ColumnType.ZVSIZE, Offset = 96, IncludeZero = true, Condition = 100 },
					new() { Name = "Time",  Type = ColumnType.TIME2, Offset = 102, Condition = 100 },
					new() { Name = "Param", Offset = 100 }
				]
			},
			new() {
				Name = "Speed",
				Columns = [
					new() { Name = "Offset", Type = ColumnType.ZVSIZE, Offset = 118, IncludeZero = true, Condition = 142 },
					new() { Name = "Time", Type = ColumnType.TIME2, Offset = 124, Condition = 142 },
					new() { Name = "Param", Offset = 122, Condition = 142 }
				]
			},
			new() {
				Name = "2DBox",
				Columns = [
					new() { Name = "X", Offset = 20, IncludeZero = true },
					new() { Name = "Y", Offset = 24, IncludeZero = true },
					new() { Name = "SX", Offset = 22, IncludeZero = true },
					new() { Name = "SY", Offset = 26, IncludeZero = true }
				]
			},
			new() {
				Name = "Action",
				Columns = [
					new() { Name = "Anim", Type = ColumnType.ANIM, Offset = 144, Condition = 142 },
					new() { Name = "Type", Offset = 142, Values = new Dictionary <int, string> { { 0, "none" }, { 1, "pre_hit" }, { 2, "hit" }, { 3, "unknown" }, { 4, "pre_fire" }, { 5, "fire" }, { 6, "pre_throw" }, { 7, "throw" }, { 8, "hit_obj" }, { 9, "during_throw" }, { 10, "pre_hit" } } },
					new() { Name = "Frame", Offset = 146, Condition = 142 },
					new() { Name = "Force", Offset = 150, Condition = 142 }
				]
			},
			new() {
				Name = "HitBox",
				Columns = [
					new() { Name = "Id", Offset = 152, Condition = 142 },
					new() { Name = "Size", Offset = 148, Condition = 142 },
					new() { Name = "X", Offset = 154, Condition = 142 },
					new() { Name = "Y", Offset = 156, Condition = 142 },
					new() { Name = "Z", Offset = 158, Condition = 142 }
				]
			},
			new() {
				Name = "Hit",
				Columns = [
					new() { Offset = 138 },
					new() { Name = "By", Offset = 140 }
				]
			},
			new() {
				Name = "Col",
				Columns = [
					new() { Name = "0", Offset = 126 },
					new() { Name = "1", Offset = 128 },
					new() { Name = "2", Offset = 130 },
					new() { Name = "By", Offset = 132 }
				]
			},
			new() {
				Name = "Hard",
				Columns = [
					new() { Name = "Trig", Offset = 134 },
					new() { Name = "Col", Offset = 136  }
				]
			},
			new() {
				Name = "Weight",
				Columns = [
					new() { Name = "TrackPos", Offset = 88  }
				]
			},
			new() { Name = "Slot", Type = ColumnType.SLOT }
		];
	}
}