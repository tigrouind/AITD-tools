using System;
using System.Drawing;
using System.Windows.Forms;
using Shared;

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
			var width = Tools.GetArgument(args, "-screen-width") ?? 1024;
			var height = Tools.GetArgument(args, "-screen-height") ?? 576;
			form.Size = new Size(width, height);

			Application.Run(form);
		}
	}
}
