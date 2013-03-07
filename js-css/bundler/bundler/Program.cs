using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mono.Options;
using Murray.Common;
using Newtonsoft.Json;
using Yahoo.Yui.Compressor;
using murray.common;

namespace bundler
{
    /// <summary>
    /// Purpose: easy to use js/css file minification
    /// Usage intent: as part of a build process to reduce filesize smartly.
    /// 
    /// Uses YUI Compressor .NET ( http://yuicompressor.codeplex.com/ , http://nuget.org/packages/yuicompressor.net )
    ///     which was originally a port of YUI Compressor
    ///         https://github.com/yui/yuicompressor
    ///         http://developer.yahoo.com/yui/compressor/
    /// 
    /// </summary>
    class Program
    {

        private static object _lock_fileStatuses = new object();


        enum ExitCode
        {
            Success = 0,
            Warning = 1,
            Error = 2
        }

        static int Main(string[] args)
        {
            var _startTime = DateTime.Now;
            string pathSource = null;
            string pathOutput = null;
            bool recurseDirs = false;
            bool includeGenDateInHeaderComment = false;
            bool obfuscateJavascript = false;
            bool compressFileContents = true;
            string searchPattern = "*.js";
            string headerComment = "";

            bool showHelp = false;
            bool showDebug = false;
            bool pauseWhenFinished = false;

            var p = new OptionSet() {
                { "o|pathOutput=", "[required, path and filename]",  x => pathOutput = x },
                { "s|pathSource=", "[optional, default=current folder]",  x => pathSource = x },
                { "r|recurseDirs=", "[optional, recursively compress files in subfolders, default="+recurseDirs + "]",   x => recurseDirs = str.ToBool(x)},
                { "pat|searchPattern=", "[optional, filename pattern match expression, pipe separated, default="+searchPattern + "]",   x => searchPattern = x},
                { "com|headerComment=", "[optional, comment to include in compressed file header, default="+headerComment + "]",   x => headerComment = x},
                { "id|includeGenDateInHeaderComment=", "[optional, include datestamp in output file, default=" + includeGenDateInHeaderComment + "]",   x => includeGenDateInHeaderComment = str.ToBool(x)},
                { "oj|obfuscateJavascript=", "[optional, obfuscate javascript, default="+obfuscateJavascript + "]",   x => obfuscateJavascript = str.ToBool(x)},
                { "c|compressFileContents=", "[optional, compress file contents, default="+compressFileContents + "]",   x => compressFileContents = str.ToBool(x)},
                

                //standard options for command line utils
                { "d|debug", "[optional, show debug details (verbose), default="+showDebug + "]",   x => showDebug = x != null},
                { "pause|pauseWhenFinished", "[optional, pause output window with a ReadLine when finished, default="+pauseWhenFinished + "]",   x => pauseWhenFinished = x != null},
                { "h|?|help", "show the help options",   x => showHelp = x != null },
            };
            List<string> extraArgs = p.Parse(args);


            if (string.IsNullOrWhiteSpace(pathOutput))
            {
                Console.WriteLine("Invalid pathOutput. Please specify a path and filename for the bundled output");
                showHelp = true;
            }
            else
            {
                io.EnsurePathToFile(pathOutput);
            }

            if (string.IsNullOrWhiteSpace(pathSource))
                pathSource = io.GetCurrentDirectory(); //default if not specified

            if (!Directory.Exists(pathSource))
            {
                Console.WriteLine("Invalid pathSource: " + pathSource);
                showHelp = true;
            } 

            if (showHelp)
            {
                p.WriteOptionDescriptions(Console.Out);
                return (int)ExitCode.Warning;
            }


            Console.WriteLine();
            Console.WriteLine("bundler by @AaronKMurray");
            if (showDebug)
            {
                Console.WriteLine("using options:");
                Console.WriteLine("\tpathOutput:\t" + pathOutput);
                Console.WriteLine("\tpathSource:\t" + pathSource);
                Console.WriteLine("\trecurseDirs:\t" + recurseDirs);
                Console.WriteLine("\tsearchPattern:\t" + searchPattern);
                Console.WriteLine("\theaderComment:\t" + headerComment);
                Console.WriteLine("\tincludeGenDateInHeaderComment:\t" + includeGenDateInHeaderComment);
                Console.WriteLine("\tobfuscateJavascript:\t" + obfuscateJavascript);
                Console.WriteLine("\tcompressFileContents:\t" + compressFileContents);
                Console.WriteLine();
            }

            // --- FIND THE FILES TO WORK ON --- //

            var searchOption = SearchOption.AllDirectories;
            if (!recurseDirs)
                searchOption = SearchOption.TopDirectoryOnly;

            string[] filepaths;
            try
            {
                var patterns = searchPattern.Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
                var allpaths = new List<string>();
                foreach (var pattern in patterns)
                {
                    var paths = Directory.GetFiles(pathSource, pattern, searchOption);
                    if (paths.Length > 0)
                        allpaths.AddRange(paths);
                }
                filepaths = allpaths.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error finding files in: " + pathSource);
                Console.WriteLine(ex.Message);
                return (int)ExitCode.Error;
            }

            if (showDebug)
                for (int i = 0; i < filepaths.Length; i++)
                    Console.WriteLine("{0}: {1}", i + 1, filepaths[i]);

            Console.WriteLine("Matched {0} file(s)", filepaths.Length);


            // --- BEGIN THE COMPRESSION WORK --- //
            int compressionSavings = -1;

            CompressionResult compressionResult = TryCompress(filepaths, pathOutput, compressFileContents, obfuscateJavascript, headerComment, includeGenDateInHeaderComment);
            

            if (showDebug)
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Format("Complete in {0} ms. ", compressionResult.ElapsedMilliseconds));

                if (compressionResult.Notes.Any())
                    sb.AppendLine("\t" + compressionResult.Notes.Count + " notes: ");
                foreach (var note in compressionResult.Notes)
                    sb.AppendLine("\t\t" + note);

                Console.WriteLine(sb.ToString());

                Console.WriteLine("Complete at " + DateTime.Now.ToLongTimeString() + ". Took " + DateTime.Now.Subtract(_startTime).TotalSeconds + " seconds to run");
            }

            var exitCode = ExitCode.Success;

            //handle errors
            if (compressionResult.Exception != null)
            {
                Console.WriteLine("Exception: " + compressionResult.Exception.Message);
                exitCode = ExitCode.Success;
            }
            if (!string.IsNullOrWhiteSpace(compressionResult.ErrorMessage))
            {
                Console.WriteLine("ERROR: " + compressionResult.ErrorMessage);
                exitCode = ExitCode.Success;
            }

            {
                Console.WriteLine("Press any key to complete");
                Console.ReadLine(); //just here to pause the output window during testing
            }
            return (int)exitCode;
        }

        private static CompressionResult TryCompress(IList<string> pSourceFilePaths, string pOutputFilePath,
            bool pCompressFileContents = true, bool pObfuscateJavascript = false,
            string pHeaderComment = null, bool pIncludeGenDateInHeaderComment = true)
        {
            var sw = new Stopwatch();
            var result = new CompressionResult();

            try
            {
                sw.Start();

                var fi = new FileInfo(pSourceFilePaths.First());
                var ext = fi.Extension.ToLower();
                var isJs = ext.EndsWith("js");
                var isCss = ext.EndsWith("css");

                result.StartSize = fi.Length;

                if (isJs)
                {
                    var results = RunYuiCompressorForJs(pSourceFilePaths, pOutputFilePath, pCompressFileContents, pObfuscateJavascript, pHeaderComment, pIncludeGenDateInHeaderComment);
                    result.AddNote(results);
                    var step1ms = sw.ElapsedMilliseconds;
                    result.AddNote("Js Compression Results: took " + step1ms + " milliseconds");

                }
                else if (isCss)
                {
                    var results = RunYuiCompressorForCss(pSourceFilePaths, pOutputFilePath, pCompressFileContents, pHeaderComment, pIncludeGenDateInHeaderComment);
                    result.AddNote(results);
                    var step1ms = sw.ElapsedMilliseconds;
                    result.AddNote("Js Compression Results: took " + step1ms + " milliseconds");
                }
               

                result.SizeDelta = result.StartSize - result.EndSize;
                if (result.SizeDelta > 0)
                    result.StatusCode = 1;
                else
                    result.StatusCode = 3;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
                result.ErrorMessage = ex.Message;
                result.StatusCode = 4;
            }
            finally
            {
                sw.Stop();
                result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            }
            return result;
        }


        /// <summary>
        /// Get an ASCII string version of an object
        /// </summary>
        public static string ToJson(List<FileActionStatus> obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        /// <summary>
        /// Covert a json string back to our .NET object
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static List<FileActionStatus> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<FileActionStatus>>(json);
        }


        static string RunYuiCompressorForJs(IList<string> pSourceFilePaths, string pOutputFilePath, 
            bool pCompressFileContents = true, bool pObfuscateJavascript = false, 
            string pHeaderComment = null, bool pIncludeGenDateInHeaderComment = true)
        {

            var compressor = new JavaScriptCompressor();
            compressor.LoggingType = LoggingType.Info;
            compressor.PreserveAllSemicolons = true;
            compressor.ObfuscateJavascript = pObfuscateJavascript;

            return RunYuiCompressor(compressor, pSourceFilePaths, pOutputFilePath, pCompressFileContents, pHeaderComment, pIncludeGenDateInHeaderComment);
        }


        static string RunYuiCompressorForCss(IList<string> pSourceFilePaths, string pOutputFilePath,
            bool pCompressFileContents = true, string pHeaderComment = null, bool pIncludeGenDateInHeaderComment = true)
        {
            var compressor = new CssCompressor();
            compressor.RemoveComments = true;

            return RunYuiCompressor(compressor, pSourceFilePaths, pOutputFilePath, pCompressFileContents, pHeaderComment, pIncludeGenDateInHeaderComment);
        }

        private static string RunYuiCompressor(ICompressor compressor, IList<string> pSourceFilePaths,
                                               string pOutputFilePath, bool pCompressFileContents = true,
                                               string pHeaderComment = null, bool pIncludeGenDateInHeaderComment = true)
        {
            compressor.CompressionType = pCompressFileContents ? CompressionType.Standard : CompressionType.None;
            compressor.LineBreakPosition = 0; //Default is -1 (never add a line break)... 0 (zero) means add a line break after every semicolon (good for debugging)
            var sbSourceText = new StringBuilder();
            var sbHeadComment = new StringBuilder();

            sbHeadComment.AppendLine("/*");
            sbHeadComment.AppendLine();

            if (pHeaderComment != null)
                sbHeadComment.AppendLine(pHeaderComment);
            if (pIncludeGenDateInHeaderComment)
                sbHeadComment.AppendLineFormat(" Generated: {0}", DateTime.Now);

            sbHeadComment.AppendLine(" Merged File List:");
            var postpendLength = pSourceFilePaths.Select(Path.GetFileName).Max(x => x.Length) + 4;

            string sourceFilePath = null;
            foreach (var filepath in pSourceFilePaths)
            {
                if (string.IsNullOrWhiteSpace(sourceFilePath))
                    sourceFilePath = Path.GetDirectoryName(filepath);

                var contentsOrig = io.ReadASCIITextFile(filepath);
                var contentsMin = compressor.Compress(contentsOrig);

                var sizeNote = string.Format("[Orig: {0} KB", contentsOrig.Length / 1024);
                if (pCompressFileContents)
                    sizeNote += string.Format(", Min'd: {0} KB", contentsMin.Length / 1024);
                sizeNote += "]";

                var fileComment = string.Format("{0} : {1}", str.Postpend(Path.GetFileName(filepath), postpendLength, ' '), sizeNote);
                sbHeadComment.AppendLineFormat(" -> {0}", fileComment);

                sbSourceText.AppendLine(contentsMin); //concat the file contents
            }

            sbHeadComment.AppendLine();
            sbHeadComment.AppendLine("*/");
            sbHeadComment.AppendLine();

            if (pSourceFilePaths.Count == 1 && !str.ToString(Path.GetFileName(pOutputFilePath)).Contains(".")) //single file w/o a diff filename
                io.WriteTextFile(Path.Combine(pOutputFilePath, Path.GetFileName(pSourceFilePaths.First())), sbHeadComment.ToString() + sbSourceText.ToString());
            else
                io.WriteTextFile(pOutputFilePath, sbHeadComment.ToString() + sbSourceText.ToString()); //bundled file or file w/a diff filename

            return sbHeadComment.ToString();
        }
    }
}
