using Shared;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;

namespace PAKExtract
{
	class Program
	{
		public static string RootFolder = "GAMEDATA";

		static int Main(string[] args)
		{
			var files = new Argument<string[]>("files") { Arity = ArgumentArity.ZeroOrMore };

			//background
			var background = new Command("background");
			background.SetAction(result => Export.ExportBackground());

			//svg
			var svgRoom = new Option<int[]>("-room") { AllowMultipleArgumentsPerToken = true };
			var svgRotate = new Option<int>("-rotate");
			svgRotate.Validators.Add(result =>
			{
				if (Array.IndexOf([0, 90, 180, 270], result.GetValue<int>("-rotate")) == -1)
				{
					result.AddError("Should be: 0, 90, 180 or 270");
				}
			});

			var svgZoom = new Option<int>("-zoom") { DefaultValueFactory = x => 100 };
			var svgTrigger = new Option<bool>("-trigger");
			var svgCamera = new Option<bool>("-camera");
			var svgCaption = new Option<bool>("-caption");
			var svg = new Command("svg") { svgRoom, svgRotate, svgZoom,	svgTrigger, svgCamera, svgCaption };
			svg.SetAction(result =>
			{
				Export.ExportSvg(
					result.GetValue(svgRoom),
					result.GetValue(svgRotate),
					result.GetValue(svgZoom),
					result.GetValue(svgTrigger),
					result.GetValue(svgCamera),
					result.GetValue(svgCaption));
			});

			//archive
			var archiveTimegate = new Option<bool>("-timegate");
			var archive = new Command("archive") { archiveTimegate,	files };
			archive.SetAction(result => Archive.CreateArchive(result.GetValue(files), result.GetValue(archiveTimegate)));

			//info
			var info = new Command("info") { files };
			info.SetAction(result => Unpack(result.GetValue(files), true));

			//root
			var rootCommand = new RootCommand() { info, svg, archive, background, files };
			rootCommand.SetAction(result => Unpack(result.GetValue(files), false));

			var parseResult = rootCommand.Parse(args);
			return parseResult.Invoke();
		}

		static void Unpack(string[] files, bool info)
		{
			if (!files.Any())
			{
				Directory.CreateDirectory(RootFolder);
				files = [RootFolder];
			}

			var errors = new List<string>();
			string[] pakFiles = [.. GetFiles(files, errors)];

			if (errors.Any())
			{
				Console.Error.WriteLine($"Cannot find file(s) or folder(s): {string.Join(", ", errors.Select(x => $"'{x}'"))}");
			}

			var compressType = new[] { "-", "INFLATE", "", "", "DEFLATE" };
			if (info && pakFiles.Any())
			{
				Console.WriteLine("     PAK Entry   CType   CSize   USize Extra");
			}

			foreach (var file in pakFiles)
			{
				using var pak = new PakArchive(file);
				foreach (var entry in pak)
				{
					if (info)
					{
						Console.WriteLine($"{Path.GetFileNameWithoutExtension(file),8} " +
							$"{entry.Index,5} " +
							$"{(entry.CompressionType >= 0 && entry.CompressionType <= 4 ? compressType[entry.CompressionType] : entry.CompressionType.ToString()),7} " +
							$"{(entry.CompressionType != 0 ? entry.CompressedSize.ToString() : "-"),7} " +
							$"{entry.UncompressedSize,7} " +
							$"{string.Join(" ", Enumerable.Range(0, entry.Extra.Length / 4).Select(x => entry.Extra.ReadInt(x * 4)))}");
					}
					else
					{
						var destPath = Path.Combine(Path.GetFileNameWithoutExtension(file), $"{entry.Index:D8}.dat");
						WriteFile(destPath, entry.Read());
						if (entry.Extra.Length > 0)
						{
							WriteFile(Path.Combine(Path.GetDirectoryName(destPath), "EXTRA", Path.GetFileName(destPath)), entry.Extra);
						}
					}
				}
			}
		}

		static IEnumerable<string> GetFiles(string[] args, List<string> errors)
		{
			foreach (var arg in args)
			{
				if (File.Exists(arg)) //files
				{
					yield return arg;
				}
				else if (Directory.Exists(arg) && Directory.EnumerateFiles(arg, "*.PAK", SearchOption.TopDirectoryOnly).Any()) //directories
				{
					foreach (var filePath in Directory.EnumerateFiles(arg, "*.PAK", SearchOption.TopDirectoryOnly))
					{
						yield return filePath;
					}
				}
				else if (File.Exists(Path.Combine(RootFolder, arg))) //file inside GAMEDATA
				{
					yield return Path.Combine(RootFolder, arg);
				}
				else
				{
					errors.Add(arg);
				}
			}
		}

		public static void WriteFile(string filePath, byte[] data)
		{
			if (!File.Exists(filePath) || data.Length != new FileInfo(filePath).Length || !Enumerable.SequenceEqual(File.ReadAllBytes(filePath), data))
			{
				Console.WriteLine($"{filePath} {data.Length}");
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				File.WriteAllBytes(filePath, data);
			}
		}
	}
}
