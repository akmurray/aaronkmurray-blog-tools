using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Options;
using murray.common;

namespace preprocessor
{
    /// <summary>
    /// Purpose: easy to use js/css file preprocessor
    /// Usage intent: as part of a build process to convert sass files into css
    ///     Future: coffee, etc
    /// 
    /// Uses IronRuby ( http://www.ironruby.net/ ) and SassAndCoffee ( https://github.com/xpaulbettsx/SassAndCoffee ) 
    /// 
    /// Influenced by Sassifier ( https://github.com/zaus/Sassifier ) but I wanted something with more command-line control
    /// 
    /// </summary>
    class Program
    {
        enum ExitCode
        {
            Success = 0,
            Warning = 1,
            Error = 2
        }

        static int Main(string[] args)
        {
            var _startTime = DateTime.Now;
            var exitCode = ExitCode.Success;
            
            string pathSource = null;
            string pathOutput = null;
            bool recurseDirs = false;
            bool includeGenDateInHeaderComment = false;
            bool compressFileContents = false;
            string searchPattern = "*.scss";
            string headerComment = "";
            string errorFile = Path.Combine(io.GetCurrentDirectory(), "_preprocessor.errors.txt");

            bool showHelp = false;
            bool showDebug = false;
            bool pauseWhenFinished = false;

            var p = new OptionSet() {
                { "s|pathSource=", "[optional, default=current folder files]",  x => pathSource = x },
                { "o|pathOutput=", "[optional, path to output file, default=current path for each source file]",  x => pathOutput = x },
                { "r|recurseDirs=", "[optional, recursively process files in subfolders, default="+recurseDirs + "]",   x => recurseDirs = str.ToBool(x)},
                { "pat|searchPattern=", "[optional, filename pattern match expression, pipe separated, default="+searchPattern + "]",   x => searchPattern = x},
                { "com|headerComment=", "[optional, comment to include in compressed file header, default="+headerComment + "]",   x => headerComment = x},
                { "id|includeGenDateInHeaderComment=", "[optional, include datestamp in output file, default=" + includeGenDateInHeaderComment + "]",   x => includeGenDateInHeaderComment = str.ToBool(x)},
                { "c|compressFileContents=", "[optional, compress file contents, default="+compressFileContents + "]",   x => compressFileContents = str.ToBool(x)},
                { "err|errorFile=", "[optional, filename for writing errors to, default="+errorFile + "]",   x => errorFile = x},
                

                //standard options for command line utils
                { "d|debug", "[optional, show debug details (verbose), default="+showDebug + "]",   x => showDebug = x != null},
                { "pause|pauseWhenFinished", "[optional, pause output window with a ReadLine when finished, default="+pauseWhenFinished + "]",   x => pauseWhenFinished = x != null},
                { "h|?|help", "show the help options",   x => showHelp = x != null },
            };
            List<string> extraArgs = p.Parse(args);


            if (string.IsNullOrWhiteSpace(pathSource))
                pathSource = io.GetCurrentDirectory(); //default if not specified

            if (!Directory.Exists(pathSource) && !File.Exists(pathSource))
            {
                Console.WriteLine("Invalid pathSource: " + pathSource);
                showHelp = true;
            }

            if (showHelp)
            {
                p.WriteOptionDescriptions(Console.Out);
                return (int)ExitCode.Warning;
            }

            if (!string.IsNullOrWhiteSpace(pathOutput) && (!pathOutput.Contains("/") && !pathOutput.Contains("\\")))
                pathOutput = Path.Combine(io.GetCurrentDirectory(), pathOutput); //only a filename specified

            Console.WriteLine();
            Console.WriteLine("preprocessor by @AaronKMurray");
            if (showDebug)
            {
                Console.WriteLine("using options:");
                Console.WriteLine("\tpathSource:\t" + pathSource);
                Console.WriteLine("\tpathOutput:\t" + pathOutput);
                Console.WriteLine("\trecurseDirs:\t" + recurseDirs);
                Console.WriteLine("\tsearchPattern:\t" + searchPattern);
                Console.WriteLine("\theaderComment:\t" + headerComment);
                Console.WriteLine("\tincludeGenDateInHeaderComment:\t" + includeGenDateInHeaderComment);
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
                    if (Directory.Exists(pathSource))
                    {
                        var paths = Directory.GetFiles(pathSource, pattern, searchOption);
                        if (paths.Length > 0)
                            allpaths.AddRange(paths);
                    }
                    else
                    {
                        //single file
                        allpaths.Add(Path.Combine(io.GetCurrentDirectory(), pathSource));
                    }
                }
                filepaths = allpaths.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error finding files in: " + pathSource);
                Console.WriteLine(ex.Message);
                return (int)ExitCode.Error;
            }

            if (!filepaths.Any())
            {
                Console.WriteLine("No matching files in: " + pathSource + " using searchOption: " + searchOption);
                exitCode = ExitCode.Warning;
            }
            else
            {
                if (showDebug)
                {
                    Console.WriteLine("Files to process:");
                    for (int i = 0; i < filepaths.Length; i++)
                        Console.WriteLine("\t{0}: {1}", i + 1, filepaths[i]);
                }
                Console.WriteLine("Matched {0} file(s)", filepaths.Length);


                // --- BEGIN THE WORK --- //

                PreprocessResult preprocessResult = SassPreprocesser.Run(filepaths, pathOutput, compressFileContents, headerComment, includeGenDateInHeaderComment);


                if (showDebug)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Format("Complete in {0} ms. ", preprocessResult.ElapsedMilliseconds));

                    if (preprocessResult.Notes.Any())
                        sb.AppendLine("\t" + preprocessResult.Notes.Count + " notes: ");
                    foreach (var note in preprocessResult.Notes)
                        sb.AppendLine("\t\t" + note);

                    Console.WriteLine(sb.ToString());

                }


                //handle errors
                if (File.Exists(errorFile))
                    File.Delete(errorFile);

                if (!string.IsNullOrWhiteSpace(preprocessResult.ErrorMessage))
                {
                    Console.WriteLine("ERROR: " + preprocessResult.ErrorMessage);
                    exitCode = ExitCode.Error;

                    if (preprocessResult.Exception == null)
                        io.WriteTextFile(errorFile, preprocessResult.ErrorMessage);
                    else
                        io.WriteTextFile(errorFile, preprocessResult.ErrorMessage + Environment.NewLine + preprocessResult.Exception.Message);
                }
                if (showDebug && preprocessResult.Exception != null)
                {
                    if (preprocessResult.Exception.Message != preprocessResult.ErrorMessage)
                        Console.WriteLine("Exception: " + preprocessResult.Exception.Message);
                    exitCode = ExitCode.Error;
                }

                if (showDebug)
                    Console.WriteLine("Complete at " + DateTime.Now.ToLongTimeString() + ". Took " + DateTime.Now.Subtract(_startTime).TotalSeconds + " seconds to run");

                if (pauseWhenFinished)
                {
                    Console.WriteLine("Press any key to complete");
                    Console.ReadLine(); //just here to pause the output window during testing
                }

            }

            return (int)exitCode;
        }
    }
}
