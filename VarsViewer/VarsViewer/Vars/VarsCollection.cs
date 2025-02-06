using Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VarsViewer
{
	public class VarsCollection : IReadOnlyCollection<Var>
	{
		readonly VarEnum type;
		Var[] vars = new Var[0];
		int count;

		public VarsCollection(VarEnum type)
		{
			this.type = type;
		}

		#region IReadOnlyCollection

		public Var this[int index]
		{
			get
			{
				return vars[index];
			}
		}

		public int Count
		{
			set
			{
				EnsureCapacity();
				Reset();

				void EnsureCapacity()
				{
					if (vars.Length < value)
					{
						int previousSize = vars.Length;
						Array.Resize(ref vars, value);
						for (int i = previousSize; i < vars.Length; i++)
						{
							vars[i] = new Var
							{
								Index = i,
								Type = type,
								Text = string.Empty
							};
						}
					}
				}

				void Reset()
				{
					if (value != count)
					{
						count = value;
						foreach (var var in vars)
						{
							var.Value = 0;
							var.Text = string.Empty;
							var.Time = 0;
							var.Difference = false;
						}
					}
				}
			}

			get
			{
				return count;
			}
		}

		public IEnumerator<Var> GetEnumerator()
		{
			return vars.Take(Count).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion
	}
}
