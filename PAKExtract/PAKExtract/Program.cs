using Shared;
using System;
using System.IO;
using System.Linq;

namespace PAKExtract
{
	class Program
	{
		public static string RootFolder = "GAMEDATA";

		static bool background, svg;
		static int[] svgRooms = new int[0];
		static int svgRotate;

		static int Main(string[] args)
		{
			background = Tools.HasArgument(args, "-background");

			if (Tools.HasArgument(args, "-svg"))
			{
				svg = true;
				string svgArgs = Tools.GetArgument<string>(args, "-svg");
				if (svgArgs != null && !svgArgs.StartsWith("-"))
				{
					var svgParams = svgArgs.Split(' ');
					svgRooms = (Tools.GetArgument<string>(svgParams, "rooms") ?? string.Empty)
						.Split(',')
						.Where(x => x != string.Empty && int.TryParse(x, out _))
						.Select(x => int.Parse(x))
						.ToArray();
					svgRotate = Tools.GetArgument<int>(svgParams, "rotate");
				}
			}

			bool foundFile = false;
			for (int i = 0; i < args.Length; i++)
			{
				var arg = args[i];
				if (!arg.StartsWith("-") && (i == 0 || !args[i - 1].StartsWith("-")))
				{
					if (File.Exists(arg))
					{
						ExtractFile(arg);
						foundFile = true;
					}
					else
					{
						Console.Error.Write($"Cannot find file '{arg}'");
						return -1;
					}
				}
			}

			if (!foundFile)
			{
				try
				{
					ExtractFiles();
				}
				catch (FileNotFoundException ex)
				{
					Console.Error.Write($"{Path.GetFileName(ex.FileName)} not found");
					return -1;
				}
			}

			return 0;
		}

		static void ExtractFiles()
		{
			Directory.CreateDirectory(RootFolder);
			foreach (var filePath in Directory.GetFiles(RootFolder, "*.PAK", SearchOption.TopDirectoryOnly))
			{
				ExtractFile(filePath);
			}
		}

		static void ExtractFile(string filePath)
		{
			string fileName = Path.GetFileNameWithoutExtension(filePath);

			using (var pak = new PakArchive(filePath))
			{
				if (svg && fileName.StartsWith("ETAGE"))
				{
					ExportSVG(pak, fileName, svgRooms, svgRotate);
				}

				if (background && (fileName.StartsWith("CAMERA") || fileName == "ITD_RESS"))
				{
					foreach (var entry in pak.Where(Background.IsBackground))
					{
						var data = entry.Read();
						Background.GetBackground(data);
						var destPath = Path.Combine(fileName, $"{entry.Index:D8}.png");
						WriteFile(destPath, Background.SaveBitmap());
					}
				}

				if (!background && !svg)
				{
					foreach (var entry in pak)
					{
						var destPath = Path.Combine(fileName, $"{entry.Index:D8}.dat");
						WriteFile(destPath, entry.Read());
					}
				}
			}
		}

		static void WriteFile(string filePath, byte[] data)
		{
			if (!File.Exists(filePath) || data.Length != new FileInfo(filePath).Length || !Enumerable.SequenceEqual(File.ReadAllBytes(filePath), data))
			{
				Console.WriteLine($"{filePath} {data.Length}");
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				File.WriteAllBytes(filePath, data);
			}
		}

		static void ExportSVG(PakArchive pak, string fileName, int[] rooms, int rotate)
		{
			var data = Svg.Export(pak, rooms, rotate);
			WriteFile(Path.Combine("SVG", Path.GetFileNameWithoutExtension(fileName) + ".svg"), data);
		}
	}
}
