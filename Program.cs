using System.Diagnostics;
using System.Text.RegularExpressions;
using CommandLine;

namespace MP3AppenderNinja;

/// <summary>
///     Console app that merges all mp3s found in each subfolder of a root folder into a single mp3 (named after the
///     folder) by shelling out to ffmpeg. See README.md for usage.
/// </summary>
public class Program
{
    private const string DefaultFfmpegPath = @"C:\Program Files\FFMpeg\bin\ffmpeg.exe";

    // ffmpeg's concat protocol takes a single "a|b|c" string, so a '|' in a filename can't be represented.
    private const char FfmpegSeparatorChar = '|';

    // The merge is written to this (non-mp3) extension first and only renamed on success, so a failed/aborted run
    // never leaves a half-written .mp3 behind that the next run would try to merge.
    private const string TempExtension = ".merging.tmp";

    // Matches runs of digits, used to zero-pad numbers in filenames so that they sort numerically.
    private static readonly Regex DigitRun = new(pattern: @"\d+", options: RegexOptions.Compiled);

    private static int Main(string[] args)
    {
        // cls (this throws if output is redirected to a file/pipe, which isn't worth dying over)
        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
            // ignore
        }

        ParserResult<Options> parsed = Parser.Default.ParseArguments<Options>(args: args);

        // On bad args (or --help/--version) the parser has already printed the relevant text.
        if (parsed is not Parsed<Options> success) return IsHelpOrVersion(result: parsed) ? 0 : 1;

        // Quoted paths ending in a backslash (e.g. -p "d:\foo\") can leave stray quotes/slashes/spaces on the end.
        char[] trimChars = { '\\', ' ', '\"' };
        string ffmpegPath = success.Value.FfmpegPath.Trim().TrimEnd(trimChars: trimChars);
        string parseFolderPath = success.Value.ParseFolderPath.Trim().TrimEnd(trimChars: trimChars);
        bool keepOriginalFiles = success.Value.KeepOriginalFiles;

        // ascertain that the args seem valid, else -> exit.
        string exitMsg = string.Empty;
        if (!File.Exists(path: ffmpegPath)) exitMsg += Environment.NewLine + $"FFMPEG path ({ffmpegPath}) invalid.";

        if (!Directory.Exists(path: parseFolderPath))
            exitMsg += Environment.NewLine + $"ParseFolderPath ({parseFolderPath}) path invalid.";

        if (exitMsg != string.Empty)
        {
            Console.WriteLine(value: exitMsg + Environment.NewLine + "Exiting.");
            return 1;
        }

        Console.WriteLine(value: $"Starting app with {ffmpegPath} for FFMPEG and {parseFolderPath} to be parsed.");

        // get a list of subfolders within root; skip anything we're not allowed to look into rather than crashing.
        EnumerationOptions enumerationOptions = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };
        string[] folders = Directory.GetDirectories(path: parseFolderPath, searchPattern: "*",
            enumerationOptions: enumerationOptions);

        // if root has no subfolders try the root itself
        if (folders.Length == 0) folders = new[] { parseFolderPath };

        // for each of the above folders run ffmpeg
        int failures = 0;
        foreach (string folder in folders)
            if (!MergeFolder(folderPath: folder, ffmpegPath: ffmpegPath, keepOriginalFiles: keepOriginalFiles))
                failures++;

        if (failures > 0)
            Console.WriteLine(value: Environment.NewLine +
                                     $"{failures} folder(s) failed to merge (see messages above). Their original files were left untouched.");

        Console.WriteLine(value: "Process finished. Exiting." +
                                 Environment.NewLine +
                                 "If the app has been useful consider a donation at https://www.buymeacoffee.com/nemethv or https://www.paypal.com/donate/?hosted_button_id=R5GSBXW8A5NNN" +
                                 Environment.NewLine +
                                 "Alternatively, raise a ticket pls.");
        return failures > 0 ? 1 : 0;
    }

    // --help / --version are "NotParsed" too but aren't errors.
    private static bool IsHelpOrVersion(ParserResult<Options> result)
    {
        bool help = false;
        result.WithNotParsed(action: errors =>
            help = errors.All(predicate: e => e.Tag is ErrorType.HelpRequestedError
                                                  or ErrorType.HelpVerbRequestedError
                                                  or ErrorType.VersionRequestedError));
        return help;
    }

    /// <summary>
    ///     Merges the mp3s directly inside <paramref name="folderPath" /> into <c>folderName.mp3</c>.
    ///     Returns false only if a merge was attempted and failed.
    /// </summary>
    private static bool MergeFolder(string folderPath, string ffmpegPath, bool keepOriginalFiles)
    {
        string folderName = new DirectoryInfo(path: folderPath).Name;
        string outputFile = Path.Combine(path1: folderPath, path2: folderName + ".mp3");
        string tempFile = Path.Combine(path1: folderPath, path2: folderName + TempExtension);

        // get a list of mp3s (actually this does a lot more things, check inside.)
        string[] fileList;
        try
        {
            fileList = GetOrderedFileArray(folderPath: folderPath, outputFile: outputFile);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine(value: $"Skipping {folderPath}: {e.Message}");
            return false;
        }

        // basically if we already have 1 (no more) mp3 then there's nothing to do, no need to merge 1 file with itself.
        if (fileList.Length < 2) return true;

        if (fileList.Any(predicate: f => f.Contains(value: FfmpegSeparatorChar)))
        {
            Console.WriteLine(value: $"Skipping {folderPath}: a filename contains '{FfmpegSeparatorChar}', which ffmpeg's concat can't handle. Rename it and re-run.");
            return false;
        }

        Console.WriteLine(value: $"{Environment.NewLine}Merging {fileList.Length} files in {folderPath}");

        // ffmpeg args are passed as a list so the framework does the quoting (spaces, trailing backslashes etc.)
        string[] ffmpegArgs =
        {
            "-nostdin", // never wait for keyboard input, which would hang the run
            "-y",
            "-i", "concat:" + string.Join(FfmpegSeparatorChar, fileList),
            "-acodec", "copy",
            "-f", "mp3", // the temp file has no recognisable extension, so say what it is
            tempFile
        };

        bool ok = RunExternalExe(fileName: ffmpegPath, arguments: ffmpegArgs) == 0 &&
                  File.Exists(path: tempFile) &&
                  new FileInfo(fileName: tempFile).Length > 0;

        if (!ok)
        {
            Console.WriteLine(value: $"ffmpeg failed for {folderPath}; original files kept.");
            TryDelete(path: tempFile);
            return false;
        }

        // Only now replace any previous output and (optionally) remove the sources.
        try
        {
            File.Move(sourceFileName: tempFile, destFileName: outputFile, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine(value: $"Could not write {outputFile}: {e.Message}; original files kept.");
            TryDelete(path: tempFile);
            return false;
        }

        if (!keepOriginalFiles)
            foreach (string fileName in fileList)
                try
                {
                    File.Delete(path: fileName);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Console.WriteLine(value: $"Could not delete {fileName}: {e.Message}");
                }

        return true;
    }

    /// <summary>Runs a process, waits for it, returns its exit code (or -1 if it could not be started).</summary>
    private static int RunExternalExe(string fileName, IEnumerable<string> arguments)
    {
        using Process process = new();

        process.StartInfo.FileName = fileName;
        foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(item: argument);

        // ffmpeg's own progress/log output goes straight to our console (nothing is redirected).
        process.StartInfo.UseShellExecute = false;

        try
        {
            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Previously this was swallowed and then ExitCode threw anyway since nothing had started.
            Console.WriteLine(value: $"Could not run {fileName}: {e.Message}");
            return -1;
        }
    }

    /// <summary>
    ///     Returns the full paths of the mp3s in a folder, in the order they should be merged. Excludes the folder's own
    ///     output file (<paramref name="outputFile" />) so we never merge (or delete) the result of an earlier run.
    /// </summary>
    private static string[] GetOrderedFileArray(string folderPath, string outputFile)
    {
        // Path.GetExtension is exact (the old EndsWith("mp3") also matched e.g. "foo.xmp3" or a file called "mp3").
        // Windows paths are case-insensitive so this also rules out the output file regardless of casing.
        return Directory
              .EnumerateFiles(path: folderPath)
              .Where(predicate: file =>
                   Path.GetExtension(path: file).Equals(value: ".mp3", comparisonType: StringComparison.OrdinalIgnoreCase) &&
                   !file.Equals(value: outputFile, comparisonType: StringComparison.OrdinalIgnoreCase))
               // Sort on the padded name. The original path is the tie-breaker, so e.g. "1.mp3" and "001.mp3"
               // (same padded name) no longer crash the app with a duplicate-key exception as they used to.
              .OrderBy(keySelector: file => PadFileName(fileName: Path.GetFileNameWithoutExtension(path: file)),
                   comparer: StringComparer.OrdinalIgnoreCase)
              .ThenBy(keySelector: file => file, comparer: StringComparer.OrdinalIgnoreCase)
              .ToArray();
    }

    // the logic here is that we take any fileName and within that take any consecutive batch of numeric chars
    // ... and then pad it to 3. generally this should avoid issues with sorting crap like "chapter 1" vs "chapter 11".
    // ... because we'll have chapter 001 and chapter 011.
    private static string PadFileName(string fileName)
    {
        return DigitRun.Replace(input: fileName,
            evaluator: m => m.Value.PadLeft(totalWidth: 3, paddingChar: '0'));
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path: path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // best effort
        }
    }

    // Load app with options parsed from args (ffmpeg path and the root folder to parse)
    private class Options
    {
        // Not Required: it has a default, so demanding it be supplied made the default pointless.
        [Option(shortName: 'f', longName: "ffmpegPath", Required = false, HelpText = "Full Path to FFMPEG.exe.",
            Default = DefaultFfmpegPath)]
        public string FfmpegPath { get; set; } = DefaultFfmpegPath;

        [Option(shortName: 'p', longName: "parseFolderPath", Required = true,
            HelpText = "Root Folder to parse (recursive).")]
        public string ParseFolderPath { get; set; } = string.Empty;

        [Option(shortName: 'k', longName: "keepOriginalFiles", Required = false,
            HelpText = "Keep (aka don't delete) original files?", Default = false)]
        public bool KeepOriginalFiles { get; set; }
    }
}
