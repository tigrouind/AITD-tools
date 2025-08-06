using System.Collections.Generic;

namespace VarsViewer
{
	public static class Objects
	{
		public static Column[] Instance =
		[
			new() { Name = "Id", Type = ColumnType.SLOT },
			new() { Name = "Flags", Type = ColumnType.FLAGS, Offset = 4, Values = new Dictionary<int, string> { { 1, "anim" }, { 4, "draw" }, { 8, "back" }, { 16, "push" }, { 64, "trig" }, { 128, "pick" }, { 256, "fall" } } },
			new() { Name = "Room", Type = ColumnType.ROOM, Offset = 28, IncludeZero = true },
			new()
			{
				Name = "Room",
				Columns = [
					new() { Name = "X", Offset = 16, IncludeZero = true },
					new() { Name = "Y", Offset = 18 },
					new() { Name = "Z", Offset = 20, IncludeZero = true }
				]
			},
			new() { Name = "ZVType", Offset = 6, Values = new Dictionary<int, string> { { 0, "none" }, { 1, "normal" }, { 2, "cube" }, { 3, "rot" }, { 4, "collider" } } },
			new()
			{
				Name = "Angle",
				Columns = [
					new() { Name = "X", Offset = 22 },
					new() { Name = "Y", Offset = 24 },
					new() { Name = "Z", Offset = 26 }
				]
			},
			new()
			{
				Name = "Life",
				Columns = [
					new() { Type = ColumnType.LIFE, Offset = 34, IncludeZero = true },
					new() { Name = "Mode", Offset = 32, Values = new Dictionary<int, string> { { 0, "floor" }, { 1, "room" }, { 2, "camera" } }, IncludeZero = true, Condition = 34 }
				]
			},
			new() { Name = "Body", Type = ColumnType.BODY, Offset = 2, IncludeZero = true },
			new()
			{
				Name = "Anim",
				Columns = [
					new() { Type = ColumnType.ANIM, Offset = 38, IncludeZero = true },
					new() { Name = "Key", Offset = 40, Condition = 38 },
					new() { Name = "Type", Offset = 42, Values = new Dictionary<int, string> { { 0, "once" }, { 1, "repeat" }, { 2, "uninterrupt" } }, IncludeZero = true, Condition = 38 },
					new() { Name = "Next", Offset = 44 }
				]
			},
			new()
			{
				Name = "Track",
				Columns = [
					new() { Name = "Num/Time", Type = ColumnType.TIME3, Offset = 48, IncludeZero = true },
					new() { Name = "Mode", Offset = 46, Values = new Dictionary<int, string> { { 0, "none" }, { 1, "manual" }, { 2, "follow" }, { 3, "track" } }, Condition = 48 }
				]
			},
			new()
			{
				Name = "Found",
				Columns = [
					new() { Name = "Body", Type = ColumnType.BODY, Offset = 8 },
					new() { Name = "Name", Type = ColumnType.NAME, Offset = 10 },
					new() { Name = "Flag", Type = ColumnType.FLAGS, Offset = 12, Values = new Dictionary<int, string> { { 1, "use" }, { 2, "eat" }, { 4, "read" }, { 8, "reload" }, { 16, "fight" }, { 32, "jump" }, { 64, "open" }, { 128, "close" }, { 256, "push" }, { 512, "throw" }, { 1024, "drop" } } },
					new() { Name = "Life", Type = ColumnType.LIFE, Offset = 14 }
				]
			},
			new()
			{
				Name = "FloorLife",
				Columns = [
					new() { Name = "InventoryVar", Offset = 36 }
				]
			},
			new() { Name = "Weight", Offset = 50 },
			new() { Name = "Slot" }
		];
	}
}
