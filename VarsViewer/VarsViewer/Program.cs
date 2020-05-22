using System;
using System.Drawing;
using System.Windows.Forms;

namespace VarsViewer
{
	sealed class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			var form = new MainForm();
			var width = GetArgument(args, "-screen-width") ?? 1024;
			var height = GetArgument(args, "-screen-height") ?? 576;
			form.Size = new Size(width, height);

			Application.Run(form);
		}

		static int? GetArgument(string[] args, string name)
		{
			int index = Array.IndexOf(args, name);
			if (index >= 0 && index < (args.Length - 1))
			{
				int value;
				if(int.TryParse(args[index + 1], out value))
				{
					return value;
				}
			}

			return null;
		}
	}
}
