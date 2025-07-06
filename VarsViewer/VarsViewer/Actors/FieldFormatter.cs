using Shared;
using System;
using System.Linq;

namespace VarsViewer.Actors
{
	public static class FieldFormatter
	{
		public static uint Timer1;

		public static ushort Timer2;

		public static int GetSize(Column column)
		{
			return column.Type switch
			{
				ColumnType.ZVPOS or ColumnType.ZVSIZE or ColumnType.ROOM or ColumnType.TIME => 4,
				ColumnType.BODY or ColumnType.LIFE or ColumnType.TRACK or ColumnType.ANIM or ColumnType.NAME or ColumnType.ANGLE or
					ColumnType.FLAGS or ColumnType.TIME2 or ColumnType.TIME3 or ColumnType.DEFAULT => 2,
				ColumnType.SLOT => 0,
				_ => throw new NotImplementedException(),
			};
		}

		public static string Format(byte[] memory, Column column, int i, bool fullMode)
		{
			if (column.Type == ColumnType.SLOT)
			{
				return i.ToString();
			}

			int pos = column.Offset;
			var value = memory.ReadShort(pos);

			if (value == 0 && !column.IncludeZero)
			{
				return null;
			}

			if (column.Condition != 0)
			{
				int otherValue = memory.ReadShort(column.Condition);
				if (otherValue == -1 || otherValue == 0)
				{
					return null;
				}
			}

			switch (column.Type)
			{
				case ColumnType.ZVPOS:
					{
						var next = memory.ReadShort(pos + 2);
						return $"{(value + next) / 2}";
					}

				case ColumnType.ZVSIZE:
					{
						var next = memory.ReadShort(pos + 2);
						return $"{next - value}";
					}

				case ColumnType.BODY:
					return FormatVar(VarEnum.BODYS);

				case ColumnType.LIFE:
					return FormatVar(VarEnum.LIFES);

				case ColumnType.TRACK:
					return FormatVar(VarEnum.TRACKS);

				case ColumnType.ANIM:
					return FormatVar(VarEnum.ANIMS);

				case ColumnType.NAME:
					if (value != -1)
					{
						if (fullMode && Program.Language.TryGetValue(value, out string name))
						{
							return $"{value}:{Tools.SubString(name, 6).TrimEnd('_')}";
						}

						return value.ToString();
					}
					break;

				case ColumnType.ANGLE:
					return $"{Math.Floor((value + 1024) % 1024 * 360.0f / 1024.0f)}";

				case ColumnType.ROOM:
					if (value != -1)
					{
						var next = memory.ReadShort(pos + 2);
						return $"E{value}R{next}";
					}
					break;

				case ColumnType.TIME:
					if (Program.GameVersion == GameVersion.AITD1)
					{
						var value32 = memory.ReadUnsignedInt(pos);
						var elapsed = (Timer1 - (long)value32) / 60;
						if (elapsed > 0)
						{
							return $"{elapsed / 60}:{elapsed % 60:D2}";
						}
					}
					break;

				case ColumnType.TIME2:
					if (Program.GameVersion == GameVersion.AITD1)
					{
						var uValue = unchecked((ushort)value);
						var elapsed = Timer2 - uValue;
						if (elapsed > 0 && elapsed < 60)
						{
							return elapsed.ToString();
						}
					}
					break;

				case ColumnType.TIME3:
					{
						var uValue = unchecked((ushort)value);
						if (uValue > 60)
						{
							if (Program.GameVersion == GameVersion.AITD1)
							{
								var elapsed = unchecked((ushort)Timer1) - uValue;
								if (elapsed > 0 && elapsed < 300)
								{
									return elapsed.ToString();
								}
							}

							return null;
						}

						return FormatVar(VarEnum.TRACKS);
					}

				case ColumnType.FLAGS:
					if (value != -1)
					{
						if (column.Values != null && fullMode)
						{
							return string.Join("|", column.Values
								.Where(x => (value & x.Key) != 0 && !string.IsNullOrEmpty(x.Value))
								.Select(x => x.Value));
						}

						unchecked
						{
							return $"{(ushort)value:X}";
						}
					}
					break;

				case ColumnType.DEFAULT:
					if (column.Values != null)
					{
						if (column.Values.TryGetValue(value, out string name) && (fullMode || (name != null && name.Length == 1)))
						{
							return name;
						}
					}

					if (value != -1)
					{
						return value.ToString();
					}
					break;

				default:
					throw new NotImplementedException();
			}

			return null;

			string FormatVar(VarEnum varType)
			{
				if (value != -1 && value != -2)
				{
					if (fullMode)
					{
						string name = Tools.SubString(Program.VarParser.GetText(varType, value), 6).Trim().Replace(" ", "_");
						if (!string.IsNullOrEmpty(name))
						{
							return $"{value}:{name}";
						}
					}

					return value.ToString();
				}

				return null;
			}
		}
	}
}
