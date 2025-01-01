using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
	public class CommandLine
	{
		public static object ParseAndInvoke(string[] args, Delegate del)
		{
			var parameters = del.Method.GetParameters();

			var arguments = GetArguments(args, parameters
					.ToDictionary(x => "-" + x.Name, x => x.ParameterType, StringComparer.InvariantCultureIgnoreCase))
				.ToDictionary(x => x.Name, x => x.Value, StringComparer.InvariantCultureIgnoreCase);

			var values = parameters
				.Select(x => arguments.TryGetValue("-" + x.Name, out object value) ? value :
					(x.DefaultValue == DBNull.Value ? GetDefaultValue(x.ParameterType) : x.DefaultValue))
				.ToArray();

			return del.Method.Invoke(del.Target, values);

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
	}
}
