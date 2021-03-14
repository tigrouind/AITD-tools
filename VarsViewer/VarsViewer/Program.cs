using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Shared;

namespace VarsViewer
{
	sealed class Program
	{
		[DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();
        
		[STAThread]
		static void Main(string[] args)
		{
			SetProcessDPIAware();
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			var form = new MainForm();
			var width = Tools.GetArgument(args, "-screen-width") ?? 1024;
			var height = Tools.GetArgument(args, "-screen-height") ?? 504;
			form.ClientSize = new Size(width, height);

			Application.Run(form);
		}
	}
}
