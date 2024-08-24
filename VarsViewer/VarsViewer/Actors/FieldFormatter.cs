using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VarsViewer.Actors
{
	public static class FieldFormatter
	{
		static readonly Dictionary<int, string> namesByIndex;

		public static uint Timer1;

		public static ushort Timer2;

		public static bool FullMode;

		static FieldFormatter()
		{
			namesByIndex = Language.Load();
		}

		public static string Format(byte[] memory, Column column, int i)
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
						if (FullMode && namesByIndex.TryGetValue(value, out string name))
						{
							name = Regex.Replace(name, "^(an?|the) ", string.Empty, RegexOptions.IgnoreCase).Trim();
							name = Tools.SubString(name, 6).Trim().ToLowerInvariant().Replace(" ", "_");
							return $"{value}:{name}";
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
						if (column.Values != null && FullMode)
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

				default:
					if (column.Values != null)
					{
						if (column.Values.TryGetValue(value, out string name) && (FullMode || (name != null && name.Length == 1)))
						{
							return name;
						}
					}

					if (value != -1)
					{
						return value.ToString();
					}
					break;
			}

			return null;

			string FormatVar(VarEnum varType)
			{
				if (value != -1 && value != -2)
				{
					if (FullMode)
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
