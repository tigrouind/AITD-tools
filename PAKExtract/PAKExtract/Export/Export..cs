using Shared;
using System.IO;
using System.Linq;

namespace PAKExtract
{
	public static class Export
	{
		public static void ExportBackground()
		{
			foreach (var directory in Directory.GetDirectories("."))
			{
				if (Path.GetFileName(directory).StartsWith("CAMERA") || Path.GetFileName(directory) == "ITD_RESS")
				{
					foreach (var filePath in Directory.GetFiles(directory, @"*.*", SearchOption.TopDirectoryOnly))
					{
						if (Background.IsBackground(new FileInfo(filePath).Length))
						{
							var data = File.ReadAllBytes(filePath);
							Background.GetBackground(data);
							var destPath = Path.Combine("BACKGROUND", Path.GetFileName(Path.GetDirectoryName(filePath)), $"{Path.GetFileNameWithoutExtension(filePath)}.png");
							Program.WriteFile(destPath, Background.SaveBitmap());
						}
					}
				}
			}
		}

		public static void ExportSvg(string[] args)
		{
			int rotate = 0;
			var rooms = new int[0];

			string svgArgs = Tools.GetArgument<string>(args, "-svg");
			if (svgArgs != null && !svgArgs.StartsWith("-"))
			{
				var svgParams = svgArgs.Split(' ');
				rooms = (Tools.GetArgument<string>(svgParams, "rooms") ?? string.Empty)
					.Split(',')
					.Where(x => x != string.Empty && int.TryParse(x, out _))
					.Select(x => int.Parse(x))
					.ToArray();
				rotate = Tools.GetArgument<int>(svgParams, "rotate");
			}

			foreach (var directory in Directory.GetDirectories("."))
			{
				if (Path.GetFileName(directory).StartsWith("ETAGE"))
				{
					var data = Svg.Export(directory, rooms, rotate);
					Program.WriteFile(Path.Combine("SVG", Path.GetFileNameWithoutExtension(directory) + ".svg"), data);
				}
			}
		}
	}
}
