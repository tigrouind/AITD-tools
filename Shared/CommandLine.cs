using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Shared
{
	public class CommandLine
	{
		public static void ParseAndInvoke(string[] args, Expression<Action> action)
		{
			ParseArgumentsAndInvoke(args, (MethodCallExpression)action.Body);
		}

		public static int ParseAndInvoke(string[] args, Expression<Func<int>> func)
		{
			return (int)ParseArgumentsAndInvoke(args, (MethodCallExpression)func.Body);
		}

		static object ParseArgumentsAndInvoke(string[] args, MethodCallExpression methodCall)
		{
			var parameters = methodCall.Method.GetParameters();

			var arguments = GetArguments(args, parameters
					.ToDictionary(x => "-" + x.Name, x => x.ParameterType, StringComparer.InvariantCultureIgnoreCase))
					.Append(("-args", args))
				.ToDictionary(x => x.Name, x => x.Value, StringComparer.InvariantCultureIgnoreCase);

			var values = parameters
				.Zip(methodCall.Arguments, (x, y) => (Parameter: x, Argument: y))
				.Select(x => arguments.TryGetValue("-" + x.Parameter.Name, out object value) ? value :
					(x.Parameter.DefaultValue == DBNull.Value ? ((ConstantExpression)x.Argument).Value : x.Parameter.DefaultValue))
				.ToArray();

			return methodCall.Method.Invoke(null, values);
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
