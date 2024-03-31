using Shared;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace PAKExtract
{
	class Program
	{
		public static string RootFolder = "GAMEDATA";
		static readonly DirectBitmap bitmap = new DirectBitmap(320, 200);
		static bool aitd1, raw;

		static void Main(string[] args)
		{
			aitd1 = Tools.HasArgument(args, "-aitd1");
			raw = Tools.HasArgument(args, "-raw");

			bool foundFile = false;
			foreach (var arg in args)
			{
				if (!arg.StartsWith("-") && File.Exists(arg))
				{
					ExtractFile(arg);
					foundFile = true;
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
					Console.WriteLine($"{Path.GetFileName(ex.FileName)} not found");
				}
			}
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
				if (fileName.StartsWith("ETAGE") && aitd1 && !raw)
				{
					RenderMasks(pak, fileName, null, MaskAITD1.RenderMasks);
				}
				else if ((fileName.StartsWith("MASK") || fileName.StartsWith("NASK")) && !raw)
				{
					RenderMasks(pak, fileName, null, MaskAITD2.RenderMasks);
				}
				else if ((fileName.StartsWith("MK") || fileName.StartsWith("NK")) && !raw)
				{
					RenderMasks(pak, fileName.Substring(0, 4), int.Parse(fileName.Substring(4, 2)), MaskAITD2.RenderMasksTimeGate);
				}
				else
				{
					foreach (var entry in pak)
					{
						if ((fileName.StartsWith("CAMERA") || fileName == "ITD_RESS") && IsBackground(entry) && !raw)
						{
							var data = entry.Read();
							GetBackground(data, bitmap.Bits);
							var destPath = Path.Combine(fileName, $"{entry.Index:D8}.png");
							SaveBitmap(destPath);
						}
						else
						{
							var destPath = Path.Combine(fileName, $"{entry.Index:D8}.dat");
							WriteFile(destPath, entry.Read());
						}
					}
				}
			}
		}

		static bool IsBackground(PakArchiveEntry entry)
		{
			switch (entry.UncompressedSize)
			{
				case 64000:
				case 64768:
				case 64770:
					return true;

				default:
					return false;
			}
		}

		static void GetBackground(byte[] data, uint[] dest)
		{
			switch (data.Length)
			{
				case 64000: //AITD1
					{
						var pal = Palette.LoadITDPalette(RootFolder);
						for (int i = 0; i < 64000; i++)
						{
							dest[i] = pal[data[i]];
						}
						break;
					}


				case 64768: //AITD2, AITD3, TIME GATE
					{
						var pal = Palette.LoadPalette(data, 64000);
						for (int i = 0; i < 64000; i++)
						{
							dest[i] = pal[data[i]];
						}
						break;
					}


				case 64770: //ITD_RESS
					{
						var pal = Palette.LoadPalette(data, 2);
						for (int i = 0; i < 64000; i++)
						{
							dest[i] = pal[data[i + 770]];
						}
						break;
					}
			}
		}

		static void SaveBitmap(string filePath)
		{
			using (var stream = new MemoryStream())
			{
				bitmap.Bitmap.Save(stream, ImageFormat.Png);
				WriteFile(filePath, stream.ToArray());
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

		static void RenderMasks(PakArchive pak, string folder, int? camID, Func<PakArchive, uint[], IEnumerable<int>> renderMasks)
		{
			foreach (var cameraID in renderMasks(pak, bitmap.Bits))
			{
				var destPath = Path.Combine(folder, $"{camID ?? cameraID:D8}.png");
				SaveBitmap(destPath);
			}
		}
	}
}
