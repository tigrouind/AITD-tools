using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Shared
{
	public static class Tools
	{
		#region Read

		public static ushort ReadUnsignedShort(this byte[] data, int offset)
		{
			unchecked
			{
				return (ushort)(data[offset] | data[offset + 1] << 8);
			}
		}

		public static ushort ReadUnsignedShortSwap(this byte[] data, int offset)
		{
			unchecked
			{
				return (ushort)(data[offset] << 8 | data[offset + 1]);
			}
		}

		public static short ReadShort(this byte[] data, int offset)
		{
			unchecked
			{
				return (short)(data[offset] | data[offset + 1] << 8);
			}
		}

		public static uint ReadUnsignedInt(this byte[] data, int offset)
		{
			unchecked
			{
				return (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
			}
		}

		public static int ReadInt(this byte[] data, int offset)
		{
			unchecked
			{
				return data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24;
			}
		}

		public static int ReadFarPointer(this byte[] data, int offset)
		{
			unchecked
			{
				return ReadUnsignedShort(data, offset) + ReadUnsignedShort(data, offset + 2) * 16;
			}
		}

		public static (int X, int Y, int Z) ReadVector(this byte[] data, int offset)
		{
			unchecked
			{
				return (
					ReadShort(data, offset + 0),
					ReadShort(data, offset + 2),
					ReadShort(data, offset + 4)
				);
			}
		}

		public static ((int X, int Y, int Z) lower, (int X, int Y, int Z) upper) ReadBoundingBox(this byte[] data, int offset)
		{
			return ((
				data.ReadShort(offset + 0),
				data.ReadShort(offset + 4),
				data.ReadShort(offset + 8)
			),(
				data.ReadShort(offset + 2),
				data.ReadShort(offset + 6),
				data.ReadShort(offset + 10)
			));
		}

		#endregion

		#region Write

		public static void Write(this byte[] data, short value, int offset)
		{
			unchecked
			{
				data[offset + 0] = (byte)(value & 0xFF);
				data[offset + 1] = (byte)(value >> 8);
			}
		}

		public static void Write(this byte[] data, ushort value, int offset)
		{
			unchecked
			{
				data[offset + 0] = (byte)(value & 0xFF);
				data[offset + 1] = (byte)(value >> 8);
			}
		}

		#endregion

		#region Pattern

		public static int IndexOf(byte[] buffer, byte[] pattern, int offset = 0, int stride = 1)
		{
			for (int index = offset; index < buffer.Length - pattern.Length + 1; index += stride)
			{
				if (buffer[index] == pattern[0] && buffer.IsMatch(pattern, index))
				{
					return index;
				}
			}

			return -1;
		}

		static bool IsMatch(this byte[] buffer, byte[] pattern, int index)
		{
			for (int i = 0; i < pattern.Length; i++)
			{
				if (buffer[i + index] != pattern[i])
				{
					return false;
				}
			}

			return true;
		}

		#endregion

		public static TimeSpan GetTimeSpan(long start, long end)
		{
			return TimeSpan.FromTicks(start - end);
		}

		public static IEnumerable<string> ReadLines(byte[] buffer, Encoding encoding)
		{
			using (var stream = new MemoryStream(buffer))
			using (var reader = new StreamReader(stream, encoding))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					yield return line;
				}
			}
		}

		#region Arguments

		public static int ParseArguments<T>(string[] args)
		{
			var method = typeof(T)
				.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
				.Where(x => x.Name == "Main")
				.Select(x => (Method: x, Parameters: x.GetParameters()))
				.FirstOrDefault(x => !(x.Parameters.Length == 1 && x.Parameters[0].ParameterType == typeof(string[])));

			if (method == default)
			{
				throw new Exception("No suitable Main() method found");
			}

			var arguments = GetArguments(args, method
					.Parameters
					.ToDictionary(x => "-" + x.Name, x => x.ParameterType, StringComparer.InvariantCultureIgnoreCase))
				.Append((Name: "-args", Value:args))
				.ToDictionary(x => x.Name, x => x.Value, StringComparer.InvariantCultureIgnoreCase);

			var values = method.Parameters
				.Select(x => arguments.TryGetValue("-" + x.Name, out object value) ? value :
					(x.DefaultValue == DBNull.Value ? GetDefaultValue(x.ParameterType) : x.DefaultValue))
				.ToArray();

			return (int)method.Method.Invoke(null, values);

			object GetDefaultValue(Type type)
			{
				return type.IsValueType ? Activator.CreateInstance(type) : null;
			}
		}

		static IEnumerable<(string Name, object Value)> GetArguments(string[] args, Dictionary<string, Type> nameToType)
		{
			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i];
				if (nameToType.TryGetValue(arg, out Type paramType))
				{
					if (paramType == typeof(bool))
					{
						yield return (arg, true);
					}
					else if (paramType == typeof(string))
					{
						if (i + 1 < args.Length)
						{
							yield return (arg, args[i + 1]);
							i++;
						}
					}
					else if (paramType == typeof(int))
					{
						if (i + 1 < args.Length)
						{
							if (int.TryParse(args[i + 1], out int value))
							{
								yield return (arg, value);
								i++;
							}
						}
					}
					else if (paramType == typeof(int[]))
					{
						if (i + 1 < args.Length)
						{
							var value = args[i + 1].Split(',')
								.Select(x => x != string.Empty && int.TryParse(x, out int intValue) ? (int?)intValue : null)
								.Where(x => x.HasValue)
								.Select(x => x.Value)
								.ToArray();

							yield return (arg, value);
							i++;
						}
					}
					else if (paramType.IsClass)
					{
						var otherArgs = args.Skip(i + 1).TakeWhile(x => x.IndexOf("-") == -1).ToArray();
						var fields = paramType.GetFields();

						var instance = Activator.CreateInstance(paramType);
						foreach (var item in GetArguments(otherArgs, fields.ToDictionary(x => x.Name, x => x.FieldType, StringComparer.InvariantCultureIgnoreCase))
							.Join(fields, x => x.Name, x => x.Name, (x, y) => (Field: y, x.Value), StringComparer.InvariantCultureIgnoreCase))
						{
							item.Field.SetValue(instance, item.Value);
						}

						yield return (arg, instance);
						i += otherArgs.Length;
					}
					else if (paramType.IsEnum)
					{
						if (i + 1 < args.Length)
						{
							var names = Enum.GetNames(paramType);
							var values = Enum.GetValues(paramType);

							var value = names.Zip(values.Cast<Enum>(), (x, y) => (Name: x, Value: y))
								.Where(x => string.Equals(x.Name, args[i + 1], StringComparison.InvariantCultureIgnoreCase))
								.Select(x => x.Value)
								.FirstOrDefault();

							yield return (arg, value);
							i++;
						}
					}
				}
			}
		}

		#endregion
	}
}
