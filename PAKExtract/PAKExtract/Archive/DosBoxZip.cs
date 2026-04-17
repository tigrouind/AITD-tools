using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PAKExtract;

public static class DosBoxZip
{
	public static string GetDosBoxPath()
	{
		if (!File.Exists("pkzip.exe")) //cannot use DOSBox compression without PKZIP.exe (should be v1.01, v1.02 or v1.1)
		{
			Console.WriteLine("pkzip.exe not found, DOSBox compression will not be available.");
			return null;
		}

		var searchDirectories = new[]
			{
				".",
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
			};

		var dosBoxPath = searchDirectories
			.SelectMany(x => Directory.GetDirectories(x))
			.Where(x => x.Contains("dosbox", StringComparison.InvariantCultureIgnoreCase))
			.Select(x => Path.Combine(x, "dosbox.exe"))
			.FirstOrDefault(x => File.Exists(x));

		if (dosBoxPath == null)
		{
			Console.WriteLine("dosbox installation not found, DOSBox compression will not be available.");
			return null;
		}

		return dosBoxPath;
	}

	public static void CompressWithDosBox(string dosBoxPath, List<(string FilePath, PakArchiveEntry Entry)> filesToCompressWithDOSBox)
	{
		var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		Directory.CreateDirectory(tempDirectory);
		Directory.CreateDirectory(Path.Combine(tempDirectory, "FILES"));

		try
		{
			//copy pkzip.exe and all files to be compressed to a temporary folder
			File.Copy("pkzip.exe", Path.Combine(tempDirectory, "pkzip.exe"));
			foreach (var (filePath, _) in filesToCompressWithDOSBox)
			{
				File.Copy(filePath, Path.Combine(tempDirectory, "FILES", Path.GetFileName(filePath)));
			}

			var destFile = Path.Combine(tempDirectory, "temp.zip"); //compress all files into a single zip file
			RunDOSBox(dosBoxPath, destFile);
			if (File.Exists(destFile))
			{
				var data = File.ReadAllBytes(destFile);
				var result = GetCompressedDataFromZip(data);

				foreach (var (filePath, entry) in filesToCompressWithDOSBox)
				{
					//some entries might be missing (eg: file wasn't compressed because it makes no sense (eg: stored compression type)
					if (result.TryGetValue(Path.GetFileName(filePath), out var compressedInfo))
					{
						var (compressedData, uncompressedSize) = compressedInfo;
						if (uncompressedSize == entry.UncompressedSize) //safety check
						{
							entry.Write(compressedData);
							entry.CompressionType = 1;
						}
					}
				}
			}
			else
			{
				Console.WriteLine("can't read zip archive created by PKZIP, DOSBox compression will not be available.");
			}
		}
		finally
		{
			Directory.Delete(tempDirectory, true);
		}
	}

	static Dictionary<string, (byte[] ComparessedData, int UncompressedSize)> GetCompressedDataFromZip(byte[] data)
	{
		int offset = 0;
		var result = new Dictionary<string, (byte[] ComparessedData, int UncompressedSize)>(StringComparer.InvariantCultureIgnoreCase);
		while (offset + 0x20 <= data.Length &&
			   Tools.ReadUnsignedInt(data, offset + 0) == 0x04034b50) //PKZIP file header 
		{
			var compressionType = data.ReadUnsignedShort(offset + 0x08);
			var compressedSize = data.ReadUnsignedShort(offset + 0x12);
			var uncompressedSize = data.ReadUnsignedShort(offset + 0x16);
			var fileNameLen = data.ReadUnsignedShort(offset + 0x1a);
			var extraLen = data.ReadUnsignedShort(offset + 0x1c);
			var fileName = data.ReadString(offset + 0x1e, fileNameLen);

			if (data[offset + 0x04] != 0x0a //PKZIP 1.x
				|| (compressionType != 0 && compressionType != 6)) //store or implode
			{
				Console.WriteLine("Unsupported PKZIP version, DOSBox compression will not be available.");
				return [];
			}

			offset += 0x1e + fileNameLen + extraLen;
			if ((offset + compressedSize) <= data.Length
				&& compressionType == 6) //implode (might be stored as well)
			{
				result[fileName] = ([.. data.AsSpan(offset, compressedSize)], uncompressedSize);
			}

			offset += compressedSize;
		}

		return result;
	}

	static void RunDOSBox(string dosboxExePath, string directory)
	{
		var psi = new ProcessStartInfo
		{
			FileName = dosboxExePath,
			UseShellExecute = false,
			CreateNoWindow = false,
		};

		string[] commands = ["cycles max",
			@$"mount c ""{Path.GetDirectoryName(directory)}""",
			"c:",
			@$"pkzip -ei ""C:\{Path.GetFileName(directory)}"" C:\FILES\*.*",
			"exit"];

		foreach (var command in commands)
		{
			psi.ArgumentList.Add("-c");
			psi.ArgumentList.Add(command);
		}

		using var p = Process.Start(psi);
		p.WaitForExit();
	}
}