using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Options;

namespace imgsprite
{
    /// <summary>
    /// Purpose: Create images for use as CSS sprites. 
    /// Usage intent: as part of a build process to reduce number of http requests for images.
    /// </summary>
    class Program
    {


        enum ExitCode
        {
            Success = 0,
            Warning = 1,
            Error = 2
        }

        public const string PARAM_IN = "-in:";
        public const string PARAM_IMAGE_OUT = "-img-out:";
        public const string PARAM_CSS_OUT = "-css-out:";
        public const string PARAM_CSS_CLASS_NAME_PREFIX = "-css-class-name-prefix:";
        public const string PARAM_CSS_CLASS_NAME_SUFFIX = "-css-class-name-suffix:";
        public const string PARAM_IMAGE_DEPLOY_URL_BASE = "-image-deploy-url-base:";
        public const string PARAM_GENERATE_TEST_HTML_PAGE = "-gen-test-html:";
        public const string PARAM_TEST_HTML_PATH = "-test-html-path:";
        public const string PARAM_TEST_HTML_IMAGE_DEPLOY_URL_BASE = "-test-html-deploy-url-base:";
        public const string PARAM_LIMIT_BIT_DEPTH = "-limit-bit-depth:";
        


        static string UsageText
        {
            get
            {
                return GetStringFromLocalFile("imgsprite-usage.txt");
            }
        }

        static string GetStringFromLocalFile(string pFilename)
        {
            using (var sr = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Program), pFilename)))
            {
                return sr.ReadToEnd();
            }
        }

        static int Main(string[] args)
        {

            var _startTime = DateTime.Now;
            var exitCode = ExitCode.Error;
            
            bool showHelp = false;
            bool showDebug = false;
            bool pauseWhenFinished = false;




            var p = new OptionSet() {
                //standard options for command line utils
                { "d|debug", "[optional, show debug details (verbose), default="+showDebug + "]", x => showDebug = x != null},
                { "pause|pauseWhenFinished", "[optional, pause output window with a ReadLine when finished, default="+pauseWhenFinished + "]",   x => pauseWhenFinished = (x != null)},
                { "h|?|help", "show the help options",   x => showHelp = x != null },
            };

            List<string> extraArgs = p.Parse(args);

            if (showHelp)
            {
                exitCode = ExitCode.Error;
                try
                {
                    Console.WriteLine(UsageText);
                } 
                catch (Exception)
                {
                    Console.WriteLine("Help file unavailable; expected to find imgsprite-usage.txt");
                }
            } 
            else
            {
                try
                {
                    var packOptions = ParseCommandLineArgs(extraArgs);
                    packOptions.CssFormatStringForSpriteDefinition = GetStringFromLocalFile("imgsprite_cssclass_format.css");


                    List<string> oResults;
                    if (!ImagePacker.CombineImages(packOptions, out oResults))
                        throw new Exception("ImagePacker.CombineImages failed. " + string.Join(Environment.NewLine, oResults));

                    if (showDebug)
                        foreach (var result in oResults)
                            Console.WriteLine(result);

                    exitCode = ExitCode.Success;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine();
                    exitCode = ExitCode.Error;
                }

                if (showDebug)
                {
                    Console.WriteLine("Complete at " + DateTime.Now.ToLongTimeString() + ". Took " + DateTime.Now.Subtract(_startTime).TotalSeconds + " seconds to run");
                }

            }
            
            if (pauseWhenFinished)
            {
                Console.WriteLine("Press any key to complete");
                Console.ReadLine(); //just here to pause the output window during testing
            }
            return (int)exitCode;
        
        }



        static ImagePacker.PackOptions ParseCommandLineArgs(IList<string> args)
        {
            var packOptions = new ImagePacker.PackOptions();


            for (int i = 0; i < args.Count; ++i)
            {
                var arg = args[i];

                if (arg.StartsWith(PARAM_IN, StringComparison.InvariantCultureIgnoreCase))
                {
                    string inputPath = arg.Substring(PARAM_IN.Length);
                    if (!Path.IsPathRooted(inputPath) && !inputPath.StartsWith(".."))
                    {
                        inputPath = Path.Combine(Environment.CurrentDirectory, inputPath);
                    }

                    if (inputPath.Contains("*") || inputPath.Contains("?"))
                    {
                        string directoryPath = Path.GetDirectoryName(inputPath);
                        if (!Directory.Exists(directoryPath))
                            throw new ArgumentException(string.Format("{0} does not exist.", directoryPath));


                        string[] inputFilesPath = Directory.GetFiles(directoryPath, Path.GetFileName(inputPath));
                        if (inputFilesPath.Length == 0)
                        {
                            //throw new ArgumentException(string.Format("{0} path does not represent any file.", inputPath));
                            Console.WriteLine(string.Format("{0} path does not match any files.", inputPath));
                        } 
                        else
                        {
                            packOptions.ImageFilePaths.AddRange(inputFilesPath);
                        }
                    }
                    else
                    {
                        if (!File.Exists(inputPath))
                            throw new ArgumentException(string.Format("{0} does not exist.", inputPath));

                        packOptions.ImageFilePaths.Add(inputPath);
                    }
                }
                else if (arg.StartsWith(PARAM_IMAGE_OUT, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (string.IsNullOrEmpty(packOptions.ImageOutputFilePath))
                    {
                        packOptions.ImageOutputFilePath = arg.Substring(PARAM_IMAGE_OUT.Length);
                        if (!Path.IsPathRooted(packOptions.ImageOutputFilePath) && !packOptions.ImageOutputFilePath.StartsWith(".."))
                        {
                            packOptions.ImageOutputFilePath = Path.Combine(Environment.CurrentDirectory, packOptions.ImageOutputFilePath);
                        }

                        string outputDirectory = Path.GetDirectoryName(packOptions.ImageOutputFilePath);
                        if (!Directory.Exists(outputDirectory))
                        {
                            throw new ArgumentException(string.Format("Output directory {0} does not exist.", outputDirectory));
                        }
                    }
                    else
                    {
                        throw new ArgumentException(PARAM_IMAGE_OUT + " parameter is specified more than once!");
                    }
                }
                else if (arg.StartsWith(PARAM_CSS_OUT, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (string.IsNullOrEmpty(packOptions.CssOutputFilePath))
                    {
                        packOptions.CssOutputFilePath = arg.Substring(PARAM_CSS_OUT.Length);
                        if (!Path.IsPathRooted(packOptions.CssOutputFilePath) && !packOptions.CssOutputFilePath.StartsWith(".."))
                        {
                            packOptions.CssOutputFilePath = Path.Combine(Environment.CurrentDirectory, packOptions.CssOutputFilePath);
                        }

                        string outputDirectory = Path.GetDirectoryName(packOptions.CssOutputFilePath);
                        if (!Directory.Exists(outputDirectory))
                        {
                            throw new ArgumentException(string.Format("Output directory {0} does not exist.", outputDirectory));
                        }
                    }
                    else
                    {
                        throw new ArgumentException(PARAM_CSS_OUT + " parameter is specified more than once!");
                    }
                }
                else if (arg.StartsWith(PARAM_CSS_CLASS_NAME_PREFIX, StringComparison.InvariantCultureIgnoreCase))
                {
                    packOptions.CssPlaceholderValues[packOptions.CSS_PLACEHOLDER_CSS_CLASS_NAME_PREFIX] = arg.Substring(PARAM_CSS_CLASS_NAME_PREFIX.Length);
                }
                else if (arg.StartsWith(PARAM_CSS_CLASS_NAME_SUFFIX, StringComparison.InvariantCultureIgnoreCase))
                {
                    packOptions.CssPlaceholderValues[packOptions.CSS_PLACEHOLDER_CSS_CLASS_NAME_SUFFIX] = arg.Substring(PARAM_CSS_CLASS_NAME_SUFFIX.Length);
                }
                else if (arg.StartsWith(PARAM_IMAGE_DEPLOY_URL_BASE, StringComparison.InvariantCultureIgnoreCase))
                {
                    packOptions.CssPlaceholderValues[packOptions.CSS_PLACEHOLDER_IMAGE_DEPLOY_URL_BASE] = arg.Substring(PARAM_IMAGE_DEPLOY_URL_BASE.Length);
                }
                else if (arg.StartsWith(PARAM_TEST_HTML_PATH, StringComparison.InvariantCultureIgnoreCase))
                {
                    packOptions.TestHtmlPath = arg.Substring(PARAM_TEST_HTML_PATH.Length);
                }
                else if (arg.StartsWith(PARAM_TEST_HTML_IMAGE_DEPLOY_URL_BASE, StringComparison.InvariantCultureIgnoreCase))
                {
                    packOptions.TestHtmlImageDeployUrlBase = arg.Substring(PARAM_TEST_HTML_IMAGE_DEPLOY_URL_BASE.Length);
                }
                else if (arg.StartsWith(PARAM_GENERATE_TEST_HTML_PAGE, StringComparison.InvariantCultureIgnoreCase))
                {
                    packOptions.GenerateTestHtmlPage = ToBool(arg.Substring(PARAM_GENERATE_TEST_HTML_PAGE.Length));
                }
                else if (arg.StartsWith(PARAM_LIMIT_BIT_DEPTH, StringComparison.InvariantCultureIgnoreCase))
                {
                    packOptions.LimitOutputImageTo8Bits = int.Parse(arg.Substring(PARAM_LIMIT_BIT_DEPTH.Length)) == 8;
                } 
                else
                {
                    throw new ArgumentException(string.Format("Unrecognized parameter: {0}", arg));
                }
            }


            //Validate options and add defaults so that the program can run with minimal input

            if (packOptions.ImageFilePaths.Count == 0)
            {
                var filesPaths = Directory.GetFiles(Environment.CurrentDirectory, "*.*")
                    .Where(
                        file => file.EndsWith("gif", StringComparison.InvariantCultureIgnoreCase)
                            || file.EndsWith("png", StringComparison.InvariantCultureIgnoreCase)
                            || file.EndsWith("jpg", StringComparison.InvariantCultureIgnoreCase)
                            || file.EndsWith("jpeg", StringComparison.InvariantCultureIgnoreCase)
                    )
                    .ToList();
                if (filesPaths.Count > 0)
                    packOptions.ImageFilePaths.AddRange(filesPaths);

                if (packOptions.ImageFilePaths.Count == 0)
                    throw new ArgumentException(string.Format("No input images were specified or found in the current directory."));

                if (!packOptions.CssPlaceholderValues.ContainsKey(packOptions.CSS_PLACEHOLDER_CSS_CLASS_NAME_PREFIX))
                    packOptions.CssPlaceholderValues[packOptions.CSS_PLACEHOLDER_CSS_CLASS_NAME_PREFIX] = "img-"; //reasonable default value for this case where a user didn't specify command line options.
            }

            if (string.IsNullOrEmpty(packOptions.ImageOutputFilePath))
            {
                //throw new ArgumentException("Image output file path is not mentioned.");
                packOptions.ImageOutputFilePath = "sprite.png";
            }

            if (string.IsNullOrEmpty(packOptions.CssOutputFilePath))
            {
                //throw new ArgumentException("Css output file path is not mentioned.");
                packOptions.CssOutputFilePath = "sprite.css";
            }

            if (!string.Equals(Path.GetExtension(packOptions.CssOutputFilePath), ".css", StringComparison.InvariantCultureIgnoreCase))
                throw new ArgumentException("Css output file should be of type CSS.");

            if (!packOptions.CssPlaceholderValues.ContainsKey(packOptions.CSS_PLACEHOLDER_IMAGE_DEPLOY_URL_BASE))
                packOptions.CssPlaceholderValues[packOptions.CSS_PLACEHOLDER_IMAGE_DEPLOY_URL_BASE] = string.Empty;
            if (!packOptions.CssPlaceholderValues.ContainsKey(packOptions.CSS_PLACEHOLDER_CSS_CLASS_NAME_SUFFIX))
                packOptions.CssPlaceholderValues[packOptions.CSS_PLACEHOLDER_CSS_CLASS_NAME_SUFFIX] = string.Empty;
            if (!packOptions.CssPlaceholderValues.ContainsKey(packOptions.CSS_PLACEHOLDER_CSS_CLASS_NAME_SUFFIX))
                packOptions.CssPlaceholderValues[packOptions.CSS_PLACEHOLDER_CSS_CLASS_NAME_SUFFIX] = string.Empty;

            return packOptions;
        }



        /// <summary>
        /// Used when you want to get a bool from a string that may have T/F, Y/N, etc
        /// </summary>
        /// <param name="pString"></param>
        /// <returns>True for: "Y", "YES", "ON", "1", "T", "TRUE"</returns>
        public static bool ToBool(string pString)
        {
            pString = pString == null ? "" : pString.Trim().ToUpper();

            return pString == "Y" ||
                   pString == "YES" ||
                   pString == "ON" ||
                   pString == "1" ||
                   pString == "T" ||
                   pString == "TRUE";
        }
    }
}
