using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CommandLine;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace MP3AppenderNinja;

public class Program
{
    // Load app with options parsed from args (ffmpeg path and the root folder to parse)
    private class Options
    {
        [Option(shortName: 'f', longName: "ffmpegPath", Required = true, HelpText = "Full Path to FFMPEG.exe.",
            Default = @"C:\Program Files\FFMpeg\bin\ffmpeg.exe")]
        public string FfmpegPath { get; set; }

        [Option(shortName: 'p', longName: "parseFolderPath", Required = true,
            HelpText = "Root Folder to parse (recursive).")]
        public string ParseFolderPath { get; set; }

        [Option(shortName: 'k', longName: "keepOriginalFiles", Required = false,
            HelpText = "Keep (aka don't delete) original files?", Default = false)]
        public bool KeepOriginalFiles { get; set; }
    }

    private static void Main(string[] args)
    {
        string ffmpegPath = string.Empty;
        string parseFolderPath = string.Empty;
        bool keepOriginalFiles = false;

        string exitMsg = string.Empty;
        const char ffmpegSeparatorChar = '|';
        const string ffmpegStringStart = " -y -i \"concat:"; // needed for ffmpeg to do our bidding
        const string ffmpegStringEnd = "\" -acodec copy "; // needed for ffmpeg to do our bidding

        // cls
        Console.Clear();

        // ascertain that the args seem valid, else -> exit.
        Parser.Default.ParseArguments<Options>(args: args)
              .WithParsed(action: o =>
               {
                   char[] trimChars = { '\\', ' ', '\"' };

                   o.FfmpegPath = o.FfmpegPath.TrimEnd(trimChars: trimChars);
                   o.ParseFolderPath = o.ParseFolderPath.TrimEnd(trimChars: trimChars);
                   ffmpegPath = o.FfmpegPath;
                   parseFolderPath = o.ParseFolderPath;
                   keepOriginalFiles = o.KeepOriginalFiles;
               });

        if (!File.Exists(path: ffmpegPath)) exitMsg += Environment.NewLine + "FFMPEG path invalid.";

        if (!Directory.Exists(path: parseFolderPath))
            exitMsg += Environment.NewLine + $"ParseFolderPath ({parseFolderPath}) path invalid.";

        if (exitMsg != string.Empty)
        {
            exitMsg += Environment.NewLine + "Exiting.";
            Console.WriteLine(value: exitMsg);
            Environment.Exit(exitCode: 1);
        }
        else
        {
            Console.WriteLine(value: $"Starting app with {ffmpegPath} for FFMPEG and {parseFolderPath} to be parsed.");
        }

        // get a list (array, really) of subfolders within root
        string[] folders = Directory.GetDirectories(path: parseFolderPath, searchPattern: "*",
            searchOption: SearchOption.AllDirectories);

        // if root has no subfolders try the root itself
        if (folders.Length == 0) folders = new string[1] { parseFolderPath };

        // for each of the above folders run ffmpeg
        foreach (string folder in folders) RunFFMpeg(folderPath: folder);

        Console.WriteLine(value: "Process finished. Exiting." +
                                 Environment.NewLine +
                                 "If the app has been useful consider a donation at https://www.buymeacoffee.com/nemethv or https://www.paypal.com/donate/?hosted_button_id=R5GSBXW8A5NNN" +
                                 Environment.NewLine +
                                 "Alternatively, raise a ticket pls.");
        return;

        // This runs ffmpeg (shocker, right?)
        // ReSharper disable once InconsistentNaming
        void RunFFMpeg(string folderPath)
        {
            DirectoryInfo directoryInfo = new(path: folderPath);

            // get a list of mp3s (actually this does a lot more things, check inside.)
            string[] fileList = GetOrderedFileArray(folderPath: folderPath);

            // basically if we already have 1 (no more) mp3 then there's nothing to do, no need to merge 1 file with itself.
            if (fileList.Length > 1)
            {
                int ffmpegResult = RunExternalExe(fileName: ffmpegPath, arguments: ffmpegStringStart +
                    string.Join(separator: ffmpegSeparatorChar,
                        value: fileList) +
                    ffmpegStringEnd +
                    $" \"{folderPath + "\\" + directoryInfo.Name}" +
                    ".mp3\"");
                if (ffmpegResult == 0 &&
                    !keepOriginalFiles)
                    foreach (string fileName in fileList)
                        File.Delete(path: fileName);
            }
        }

        // hopefully obvious
        int RunExternalExe(string fileName, string arguments)
        {
            Process process = new();

            process.StartInfo.FileName = fileName;
            if (!string.IsNullOrEmpty(value: arguments)) process.StartInfo.Arguments = arguments;

            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.UseShellExecute = false;

            process.StartInfo.RedirectStandardError = false;
            process.StartInfo.RedirectStandardOutput = false;
            StringBuilder stdOutput = new();
            process.OutputDataReceived +=
                (sender, dataReceivedEventArgs) =>
                    stdOutput.AppendLine(value: dataReceivedEventArgs
                       .Data);

            try
            {
                process.Start();
                process.WaitForExit();
            }
            catch
            {
                // nothing
            }

            return process.ExitCode;
        }

        // see comments inside
        string[] GetOrderedFileArray(string folderPath)
        {
            DirectoryInfo directoryInfo = new(path: folderPath);
            List<string> fileListPadded = new();

            // get every mp3 in the folder that's not the same as the foldername
            // the above is to ascertain that we don't end up merging some aborted merge with itself. 
            // also as to make sure it doesn't get deleted afterwards
            IEnumerable<string> fileList = Directory
                                          .EnumerateFiles(path: folderPath)
                                          .Where(predicate: file =>
                                               file.ToLower().EndsWith(value: "mp3") &&
                                               !file.ToLower()
                                                    .EndsWith(value: "\\" + directoryInfo.Name.ToLower() + ".mp3"));

            // sorted dictionary where the padded name is the key
            SortedDictionary<string, string> sortedFileDict = new();
            foreach (string s in fileList)
                sortedFileDict.Add(key: PadFileName(fileName: Path.GetFileNameWithoutExtension(path: s)),
                    value: Path.GetFileNameWithoutExtension(path: s));

            // return the original filenames in proper sorted order
            foreach (KeyValuePair<string, string> keyValuePair in sortedFileDict)
                fileListPadded.Add(item: Path.Combine(path1: folderPath, path2: keyValuePair.Value + ".mp3"));
            return fileListPadded.ToArray();
        }

        // the logic here is that we take any fileName and within that take any consecutive batch of numeric chars
        // ... and then pad it to 3. generally this should avoid issues with sorting crap like "chapter 1" vs "chapter 11".
        // ... because we'll have chapter 001 and chapter 011. 
        static string PadFileName(string fileName)
        {
            Regex regex = new(pattern: @"\d+");
            string paddedFileName = regex.Replace(input: fileName,
                evaluator: m => m.Value.PadLeft(totalWidth: 3, paddingChar: '0'));
            return paddedFileName;
        }
    }
}
