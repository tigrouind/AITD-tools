using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Shared
{
	public class CommandLine
	{
		public static int ParseAndInvoke(string[] args, Delegate del)
		{
			var parameters = del.Method.GetParameters();

			var invalidArguments = new List<string>();
			var otherArguments = new List<string>();
			var arguments = GetArguments(args, parameters
					.ToDictionary(x => "-" + x.Name, x => Nullable.GetUnderlyingType(x.ParameterType) ?? x.ParameterType, StringComparer.InvariantCultureIgnoreCase), invalidArguments, otherArguments)
				.ToDictionary(x => x.Name, x => x.Value, StringComparer.InvariantCultureIgnoreCase);

			if (parameters.Any(x => x.Name == "args"))
			{
				arguments.Add("-args", otherArguments.ToArray());
			}

			var values = parameters
				.Select(x => arguments.TryGetValue("-" + x.Name, out object value) ? value :
					(x.DefaultValue == DBNull.Value ? GetDefaultValue(x.ParameterType) : x.DefaultValue))
				.ToArray();

			try
			{
				return (int)del.Method.Invoke(del.Target, values);
			}
			catch (TargetInvocationException ex)
			{
				ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
				return -1;
			}

			static object GetDefaultValue(Type type)
			{
				return type.IsValueType ? Activator.CreateInstance(type) : null;
			}
		}

		static IEnumerable<(string Name, object Value)> GetArguments(string[] args, Dictionary<string, Type> nameToType, List<string> invalidArguments, List<string> otherArguments)
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
						var otherArgs = args.Skip(i + 1).TakeWhile(x => !x.Contains('-', StringComparison.CurrentCulture)).ToArray();
						var fields = paramType.GetFields();

						var instance = Activator.CreateInstance(paramType);
						foreach (var item in GetArguments(otherArgs, fields.ToDictionary(x => x.Name, x => x.FieldType, StringComparer.InvariantCultureIgnoreCase), invalidArguments, otherArguments)
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

							if (value == null)
							{
								invalidArguments.Add(args[i + 1]);
							}

							yield return (arg, value);
							i++;
						}
					}
					else
					{
						throw new NotSupportedException();
					}
				}
				//else if (!checkArgumentsWithDashes || arg.Contains("-"))
				//{
				//	invalidArguments.Add(arg);
				//}
				else
				{
					otherArguments.Add(arg);
				}
			}
		}
	}
}
