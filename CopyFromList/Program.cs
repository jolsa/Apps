using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CopyFromList
{
	class Program
	{
		static void Main(string[] args)
		{
			string inputFile = null;
			string targetFolder = null;
			bool showHelp = false;
			if (args.Length != 2)
				showHelp = true;
			else
			{
				inputFile = args.FirstOrDefault(a => File.Exists(a));
				targetFolder = args.FirstOrDefault(a => Directory.Exists(a));
				showHelp = inputFile == null || targetFolder == null;
			}
			if (showHelp)
			{
				Console.WriteLine(Resources.Syntax);
				return;
			}
			inputFile = Path.GetFullPath(inputFile);
			targetFolder = Path.GetFullPath(targetFolder);
			while (targetFolder.EndsWith("\\")) targetFolder = targetFolder.Substring(0, targetFolder.Length - 1);
			Console.WriteLine($"input file: {inputFile}\r\ntarget folder: {targetFolder}");
			CopyFiles(inputFile, targetFolder);
		}

		private static void CopyFiles(string inputFile, string targetFolder)
		{
			//	Get lines from input file
			var lines = File.ReadAllLines(inputFile).Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();
			//	Get the files
			var files =
				//	If it's a path, get the files
				lines.Where(l => Directory.Exists(l)).SelectMany(l => Directory.GetFiles(l, "*.*", SearchOption.AllDirectories).Select(f => Path.GetFullPath(f)))
				//	Add any files
				.Concat(lines.Where(l => File.Exists(l)).Select(f => Path.GetFullPath(f))).ToList();

			//	Get distinct paths
			var allPaths = files.Select(f => Path.GetDirectoryName(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

			//	Get all paths (e.g. C:\dir1\dir2\dir3\File.txt = C:\dir1, C:\dir1\dir2, C:\dir1\dir2\dir3)
			Func<string, List<string>> getRoots = null; // Recursion must be declared as null before used
			getRoots = p =>
			{
				var list = new List<string>();
				var root = Path.GetDirectoryName(p);
				if (root != null)
				{
					list.Add(root);
					list.AddRange(getRoots(root));
				}
				return list;
			};

			//	Get all roots by entry
			var pathsAndRoots = allPaths.Select(p => new { path = p, subs = getRoots(p) });
			//	Get distinct roots
			var allRoots = pathsAndRoots.SelectMany(r => r.subs).Distinct(StringComparer.OrdinalIgnoreCase);
			//	Get the longest common path
			var commonRoot = allRoots.Where(r => allPaths.All(p => p.StartsWith(r, StringComparison.OrdinalIgnoreCase))).OrderBy(r => r.Length).Last();

			//	Get files to copy
			var toCopy = files.Select(f => new
			{
				source = f,
				target = Regex.Replace(f, $@"^{ Regex.Escape(commonRoot)}", targetFolder, RegexOptions.IgnoreCase)
			}).Select(f =>
			{
				var targetPath = Path.GetDirectoryName(f.target);
				var srcInfo = new FileInfo(f.source);
				var trgInfo = new FileInfo(f.target);
				return new
				{
					targetPath,
					f.source,
					srcModify = srcInfo.LastWriteTime,
					srcSize = srcInfo.Length,
					f.target,
					trgInfo.Exists,
					trgModify = trgInfo.Exists ? (DateTime?)trgInfo.LastWriteTime : null,
					trgSize = trgInfo.Exists ? (long?)trgInfo.Length : null
				};
			})
			//	If target doesn't exist, dates are different or size is different, copy it
			.Where(f => !f.Exists || f.srcModify != (f.trgModify ?? DateTime.MinValue) || f.srcSize != (f.trgSize ?? -1))
			.ToList();

			//	Create all the directories
			toCopy.Select(c => c.targetPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList().ForEach(p =>
			{
				if (!Directory.Exists(p)) Directory.CreateDirectory(p);
			});

			//	Copy the files
			toCopy.ForEach(c =>
			{
				File.Copy(c.source, c.target, true);
				Console.WriteLine($"{c.source}");
			});
			Console.WriteLine($"{toCopy.Count:#,0} file(s) copied.");
		}
	}
}
