using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mono.Options;
using Newtonsoft.Json;

namespace imgsqz
{
    /// <summary>
    /// Purpose: multi-threaded near-maximum filesize reduction for images, without re-processing them over and over.
    /// Usage intent: as part of a build process to reduce filesize smartly.
    /// Currently optipng ( http://optipng.sourceforge.net/ ), 
    ///     which uses pngcrush.exe ( http://pmt.sourceforge.net/pngcrush/ )
    /// 
    /// For a GUI compressor, I prefer PNGGauntlet ( http://pnggauntlet.com/ ) 
    ///     which uses pngout.exe by the awesome Ken Silverman: http://advsys.net/ken/utils.htm#pngout
    /// 
    /// For reducing filesize of JPG/GIF, currently using ImageMagick  ( http://www.imagemagick.org/ ) 
    /// 
    /// </summary>
    class Program
    {

        private static object _lock_fileStatuses = new object();
		private static DateTime _lastFileStatusSave = DateTime.Now;
		
		private static bool _showProgressIndicator = false;
		private static Timer _progressIndicatorTimer;


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
            bool forceRecalc = false;
            bool recurseDirs = true;
            bool removeTestImages = true;

            string searchPattern = "*.png|*.jpg|*.jpeg";
            string pathFileStatus = "filestatus.json";
            string imageMagickExePath = "convert.exe";

            int maxDegreeOfParallelism = -1;
			long maxFilesizePng = -1;
			long maxFilesizeJpg = -1;
			long maxFilesizeGif = -1;
            

            bool showHelp = false;
            bool showDebug = false;
			bool pauseWhenFinished = false;
			bool quiet = false;
			var currentDir = IOHelper.GetCurrentDirectory();

            var p = new OptionSet() {
                { "s|pathSource=", "[optional, default=current folder: "+currentDir+" ]",  x => pathSource = x },
                { "f|forceRecalc=", "[optional, force a recalculation of all images, default=" + forceRecalc + "]", x => forceRecalc = ToBool(x, true)},
                { "r|recurseDirs=", "[optional, recursively compress images in subfolders, default="+recurseDirs + "]", x => recurseDirs = ToBool(x, true)},
                { "pat|searchPattern=", "[optional, filename pattern match expression, default="+searchPattern + "]", x => searchPattern = x},
                { "pfs|pathFileStatus=", "[optional, file to store file compression status info, default="+pathFileStatus + "]", x => pathFileStatus = x},
                { "rem|removeTestImages=", "[optional, remove test images created when testing compression methods, default="+removeTestImages + "]", x => removeTestImages = ToBool(x, true)},
                { "imep|imageMagickExePath=", "[optional, path to ImageMagick's convert.exe, default="+imageMagickExePath + "]", x => imageMagickExePath = x},
                { "t|threads=", "[optional, max number of threads to use. -1=unlimited. default="+maxDegreeOfParallelism + ". CPU Count: "+Environment.ProcessorCount+"]", x => maxDegreeOfParallelism = ToInt(x, maxDegreeOfParallelism)},
                { "q|quiet=", "[optional, silence normal (non-error) screen output. Will be false if debug=true default="+quiet+"]", x => ToBool(x, true)},
                { "mfp|maxFilesizePng=", "[optional, max PNG filesize in bytes to attempt to compress. -1=unlimited. default="+maxFilesizePng + "]", x => maxFilesizePng = ToLong(x, maxFilesizePng)},
                { "mfj|maxFilesizeJpg=", "[optional, max JPG filesize in bytes to attempt to compress. -1=unlimited. default="+maxFilesizeJpg + "]", x => maxFilesizeJpg = ToLong(x, maxFilesizeJpg)},
                { "mfg|maxFilesizeGif=", "[optional, max GIF filesize in bytes to attempt to compress. -1=unlimited. default="+maxFilesizeGif + "]", x => maxFilesizeGif = ToLong(x, maxFilesizeGif)},
                

                //standard options for command line utils
                { "d|debug", "[optional, show debug details (verbose), default="+showDebug + "]", x => showDebug = ToBool(x, true)},
                { "pause|pauseWhenFinished", "[optional, pause output window with a ReadKey when finished, default="+pauseWhenFinished + "]", x => pauseWhenFinished = ToBool(x, true)},
                { "h|?|help", "show the help options", x => showHelp = ToBool(x, true)},
            };
            List<string> extraArgs = p.Parse(args);


            if (string.IsNullOrWhiteSpace(pathSource))
				pathSource = currentDir; //default if not specified

            if (!Directory.Exists(pathSource))
            {
                ConsoleLogger.WriteLine("Invalid pathSource: " + pathSource);
                showHelp = true;
            }

            if (maxDegreeOfParallelism == 0)
                maxDegreeOfParallelism = -1; //-1 is unlimited. 0 is not a valid setting, but might be sent intending unlimited

			if (showDebug && quiet)
				quiet = false;

            if (showHelp)
            {
                p.WriteOptionDescriptions(Console.Out);
                return (int)ExitCode.Warning;
            }


            ConsoleLogger.WriteLine();
			ConsoleLogger.WriteLine("imgsqz by @AaronKMurray");
			if (!quiet)
            {
                ConsoleLogger.WriteLine("using options:");
                ConsoleLogger.WriteLine("\tpathSource:\t" + pathSource);
                ConsoleLogger.WriteLine("\trecurseDirs:\t" + recurseDirs);
                ConsoleLogger.WriteLine("\tsearchPattern:\t" + searchPattern);
                ConsoleLogger.WriteLine("\tforceRecalc:\t" + forceRecalc);
                ConsoleLogger.WriteLine("\tpathFileStatus:\t" + pathFileStatus);
                ConsoleLogger.WriteLine("\timageMagickExePath:\t" + imageMagickExePath);
                ConsoleLogger.WriteLine("\tremoveTestImages:\t" + removeTestImages);
                ConsoleLogger.WriteLine("\tthreadMax:\t" + (maxDegreeOfParallelism == -1 ? "unlimited" : maxDegreeOfParallelism.ToString()) + ", CPU Count: " + Environment.ProcessorCount);
				ConsoleLogger.WriteLine("\tdebug:\t" + showDebug);
				ConsoleLogger.WriteLine("\tquiet:\t" + quiet);
				ConsoleLogger.WriteLine("\tmaxFilesizePng:\t" + (maxFilesizePng == -1 ? "unlimited" : maxFilesizePng.ToString() + " bytes"));
				ConsoleLogger.WriteLine("\tmaxFilesizeJpg:\t" + (maxFilesizeJpg == -1 ? "unlimited" : maxFilesizeJpg.ToString() + " bytes"));
				ConsoleLogger.WriteLine("\tmaxFilesizeGif:\t" + (maxFilesizeGif == -1 ? "unlimited" : maxFilesizeGif.ToString() + " bytes"));
				ConsoleLogger.WriteLine();
            }

            // --- FIND THE FILES TO WORK ON --- //

            var searchOption = SearchOption.AllDirectories;
            if (!recurseDirs)
                searchOption = SearchOption.TopDirectoryOnly;

			//TODO progress indicator
			//_showProgressIndicator = true;
			//AutoResetEvent autoEvent = new AutoResetEvent(false);
			//_progressIndicatorTimer = new Timer(ProgressIndicatorTimerCallback, autoEvent, 1000, 250);
			

            string[] filepaths;
            try
            {
                var patterns = searchPattern.Split(new [] {"|"}, StringSplitOptions.RemoveEmptyEntries);
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
                ConsoleLogger.WriteLine("Error finding files in: " + pathSource);
                ConsoleLogger.WriteLine(ex.Message);
                return (int)ExitCode.Error;
            }


            // --- ENSURE/LOAD UP THE STATUS FILE --- //
            List<ImageActionStatus> fileStatuses;

            if (forceRecalc)
            {
                try
                {
                    File.Delete(pathFileStatus);
                } catch {}
            }

            try
            {
                if (!File.Exists(pathFileStatus))
                    IOHelper.WriteTextFile(pathFileStatus, ToJson(new List<ImageActionStatus>()));
            } 
            catch (Exception ex)
            {
                ConsoleLogger.WriteLine("Error creating status file: " + pathFileStatus);
                ConsoleLogger.WriteLine(ex.Message);
                return (int)ExitCode.Error;
            }

            try
            {
                fileStatuses = FromJson(IOHelper.ReadASCIITextFile(pathFileStatus));
            }
            catch (Exception ex)
            {
                ConsoleLogger.WriteLine("Error loading status file: " + pathFileStatus);
                ConsoleLogger.WriteLine(ex.Message);
                //ugh, quick hack just in case
                File.Delete(pathFileStatus);
                IOHelper.WriteTextFile(pathFileStatus, ToJson(new List<ImageActionStatus>()));
                return (int)ExitCode.Error;
            }


			if (showDebug)
				for (int i = 0; i < filepaths.Length; i++)
					ConsoleLogger.WriteLine("{0}: {1}", i + 1, filepaths[i]);

			var countPreviouslyProcessed = fileStatuses.Count(x => filepaths.Contains(x.Path) && x.StatusCode != 0); //count the number of files that we've already processed

			if (!quiet)
			{
				if (showDebug)
					ConsoleLogger.WriteLine("Searching {0} [{1}] using: {2}", pathSource, recurseDirs ? "recursively" : "root only", searchPattern);
				ConsoleLogger.WriteLine("Matched {0} file(s) to compress", filepaths.Length);
					
				if (!forceRecalc && countPreviouslyProcessed > 0)
				{
					ConsoleLogger.WriteLine("Skipping {0} file(s) because they have been previously processed", countPreviouslyProcessed);
					ConsoleLogger.WriteLine("Attempting {0} to compress file(s)", filepaths.Length - countPreviouslyProcessed);
				}
			}


            //go through the matched images, and ensure that we have the proper entry/status set
            foreach (var filepath in filepaths)
            {
                var status = fileStatuses.FirstOrDefault(x => x.Path == filepath);
                if (status == null) 
                    fileStatuses.Add(new ImageActionStatus { Path = filepath, StatusCode = 0, FileSize = 0, OriginalFileSize = 0});
            }
			

            // --- BEGIN THE COMPRESSION WORK --- //
            int compressedCount = 0;
            long compressionSavings = 0;
			int workingThreads = 0;

			if (maxDegreeOfParallelism == 0 || maxDegreeOfParallelism == 1) 
			{
				//Parallel.ForEach still launches multiple threads with maxDegreeOfParallelism == 1 ... but the user is saying "1 thread max" so we'll use a regular foreach
				foreach (var filepath in filepaths)
				{
					workingThreads++;
					ProcessFilepath(fileStatuses, filepath, pathFileStatus, forceRecalc, quiet, showDebug, removeTestImages, imageMagickExePath, ref compressedCount, ref compressionSavings, maxFilesizePng, maxFilesizeJpg, maxFilesizeGif);
					workingThreads--;
				}
			}
			else 
			{
				var po = new ParallelOptions();
				po.MaxDegreeOfParallelism = maxDegreeOfParallelism; // If MaxDegreeOfParallelism is -1, then there is no limit placed on the number of concurrently running operations.
				Parallel.ForEach(filepaths, po, filepath =>
				{
					while (maxDegreeOfParallelism > 0 && workingThreads >= maxDegreeOfParallelism)
						Thread.Sleep(500);
					workingThreads++;
					ProcessFilepath(fileStatuses, filepath, pathFileStatus, forceRecalc, quiet, showDebug, removeTestImages, imageMagickExePath, ref compressedCount, ref compressionSavings, maxFilesizePng, maxFilesizeJpg, maxFilesizeGif);
					workingThreads--;
				});
			}


            // --- SAVE OUR CURRENT FILE STATUS --- //
			try
			{
				//save current status in case the program gets killed before run completes. 
				lock (_lock_fileStatuses)
					IOHelper.WriteTextFile(pathFileStatus, ToJson(fileStatuses));
			}
			catch (Exception ex)
			{
				//Eat multi-threaded IO exceptions here because this isn't a critical save
				ConsoleLogger.WriteLine("ERROR: When saving the compression status file: {0}", pathFileStatus);
				ConsoleLogger.WriteLine("ERROR Message: {0}", ex.Message);
			} 

			if (!quiet)
			{
				var filesAttempted = forceRecalc ? filepaths.Length : countPreviouslyProcessed;

				ConsoleLogger.WriteLine();
				if (compressionSavings > 1048576) //over 1MB 1024x1024
					ConsoleLogger.WriteLine("Compressed {0}/{1} files for a total savings of {2:0.0} MB", compressedCount, filesAttempted, compressionSavings / 1024.0 / 1024.0);
				else //bytes
					ConsoleLogger.WriteLine("Compressed {0}{1} files for a total savings of {2} bytes", compressedCount, filesAttempted, compressionSavings);

				var seconds = DateTime.Now.Subtract(_startTime).TotalSeconds;
				if (seconds > 3599)
					ConsoleLogger.WriteLine("Complete at {0}. Took {1:0.00} hours to run", DateTime.Now.ToLongTimeString(), (seconds / 60.0 / 60.0));
				else if (seconds > 59)
					ConsoleLogger.WriteLine("Complete at {0}. Took {1:0.00} minutes to run", DateTime.Now.ToLongTimeString(), (seconds / 60));
				else
					ConsoleLogger.WriteLine("Complete at {0}. Took {1:0.0} seconds to run", DateTime.Now.ToLongTimeString(), seconds);
			}

            if (pauseWhenFinished)
            {
                ConsoleLogger.WriteLine("Press any key to complete");
				Console.ReadKey(); //just here to pause the output window during testing
            }
            return (int)ExitCode.Success;
        }


		private static void SaveCurrentFileStatusIfNeeded(string pathFileStatus, List<ImageActionStatus> fileStatuses)
		{
			try
			{
				//save current status in case the program gets killed before run completes. 
				lock (_lock_fileStatuses)
				{
					if (DateTime.Now.Subtract(_lastFileStatusSave).TotalSeconds > 30) //don't save too often
					{
						IOHelper.WriteTextFile(pathFileStatus, ToJson(fileStatuses));
						_lastFileStatusSave = DateTime.Now;
					}
				}
			}
			catch
			{
				//Eat multi-threaded IO exceptions here because this isn't a critical save
			}
		}

		private static void ProcessFilepath(List<ImageActionStatus> fileStatuses, string filepath, string pathFileStatus, bool forceRecalc, bool quiet, bool showDebug, bool removeTestImages, string imageMagickExePath, ref int compressedCount, ref long compressionSavings, long maxFilesizePng, long maxFilesizeJpg, long maxFilesizeGif)
		{
			var status = fileStatuses.First(x => x.Path == filepath);
			var fi = new FileInfo(filepath);
			if (fi == null)
			{
				//file has probably been moved
				status.StatusCode = 5;
				if (showDebug)
					ConsoleLogger.WriteLine("{0} {1}", status.GetStatusMessage(), filepath);
				SaveCurrentFileStatusIfNeeded(pathFileStatus, fileStatuses);
				return;
			}


			if (status.OriginalFileSize <= 0)
				status.OriginalFileSize = fi.Length; //only set this if we haven't set it before because we may try to compress it multiple times, but we want to capture the change between the very first attempt, and the current state
			if (fi.Length < status.OriginalFileSize)
				status.FileSize = fi.Length; //we've probably compressed this using one of our multiple alogorithms...so while it isn't fully scanned, it is partially complete

			SaveCurrentFileStatusIfNeeded(pathFileStatus, fileStatuses); //do this before the compression of each in case long running images are being processed, and the proc gets killed over and over - we'll at least hopefully have the OriginalFileSize saved to determine if we should set the status to 2 and skip attempting to processes this file


			if (status.StatusCode == 0 || forceRecalc || (status.FileSize > 0 && status.FileSize != fi.Length && status.StatusCode != 2)) //we haven't tried to compress this yet, or we want to reprocess it no matter what, or the image has changed since we last tried and we aren't skipping it intentionally
			{
				//let's try to compress this image
				if (!quiet)
					ConsoleLogger.WriteLine("Init @ {0:MM/dd/yy H:mm:ss}: {2:0,0} KB > [{1}]", DateTime.Now, filepath, fi.Length / 1024.0);

				CompressionResult compressionResult = TryCompress(status.Path, showDebug, removeTestImages, imageMagickExePath, maxFilesizePng, maxFilesizeJpg, maxFilesizeGif);
				status.StatusCode = compressionResult.StatusCode;
				if (status.StatusCode == 1)
				{
					//thread-safe updating of out-of-current-scope variables
					Interlocked.Increment(ref compressedCount);
					Interlocked.Add(ref compressionSavings, compressionResult.SizeDelta);
				}

				if (!string.IsNullOrWhiteSpace(compressionResult.ErrorMessage) || compressionResult.Exception != null)
				{
					ConsoleLogger.WriteLine("ERROR: " + filepath);
					ConsoleLogger.WriteLine(compressionResult.ErrorMessage);
					ConsoleLogger.WriteLine(compressionResult.Exception);
					status.ErrorMessage = compressionResult.ErrorMessage;
				}

				status.FileSize = compressionResult.EndSize;

				if (!quiet)
				{
					var sb = new StringBuilder();
					sb.AppendLine("---");
					var ms = Double.Parse(compressionResult.ElapsedMilliseconds.ToString());
					if (ms > 59999)
						sb.AppendLine(string.Format("Compression complete in {0:0.0} min. Status code: {1}. [{2}] ", ms / 60000.0, status.StatusCode, status.GetStatusMessage()));
					else if (ms > 999)
						sb.AppendLine(string.Format("Compression complete in {0:0} sec. Status code: {1}. [{2}] ", ms / 1000.0, status.StatusCode, status.GetStatusMessage()));
					else
						sb.AppendLine(string.Format("Compression complete in {0:0} ms. Status code: {1}. [{2}] ", compressionResult.ElapsedMilliseconds, status.StatusCode, status.GetStatusMessage()));
					sb.AppendLine(string.Format("Completed Path: {0}", status.Path));
					sb.AppendLine(string.Format("{0:0}% smaller. Bytes saved: {1:0,0}. Final size: {2:0,0} KB", compressionResult.SizeDelta / (double)compressionResult.StartSize * 100.0, compressionResult.SizeDelta, compressionResult.EndSize / 1024.0));

					if (compressionResult.Notes.Any())
					{
						sb.AppendLine("" + compressionResult.Notes.Count + " notes: ");
						foreach (var note in compressionResult.Notes)
							sb.AppendLine("\t" + note);
					}

					ConsoleLogger.WriteLine(sb.ToString());
				}

			}
			else
			{
				if (showDebug)
					ConsoleLogger.WriteLine("Status {0} [{1}], skipping: {2}", status.StatusCode, status.GetStatusMessage(), status.Path);
			}

			SaveCurrentFileStatusIfNeeded(pathFileStatus, fileStatuses);
		}

		private static CompressionResult TryCompress(string path, bool pIsDebug, bool pRemoveTestImages, string pImageMagickExePath = null, long maxFilesizePng = -1, long maxFilesizeJpg = -1, long maxFilesizeGif = -1)
        {
            var sw = new Stopwatch();
            var result = new CompressionResult();

            try
            {
                sw.Start();

                var fi = new FileInfo(path);
                var ext = fi.Extension.ToLower();
                var isPng = ext.EndsWith("png");
                var isJpg = ext.EndsWith("jpg") || ext.EndsWith("jpeg");
                var isGif = ext.EndsWith("gif");    //TODO
                var isWebp = ext.EndsWith("webp");  //TODO 

				bool attemptedCompression = false;

                result.StartSize = fi.Length;

				if (isPng)
                {
					if (maxFilesizePng > 0 && result.StartSize > maxFilesizePng)
					{
						result.AddNote("Skipping because filesize (" + result.StartSize + ") is greater than max (" + maxFilesizePng + ") bytes for " + fi.Name);
						result.EndSize = fi.Length;
					}
					else
					{
						attemptedCompression = true;

						//first try optipng
						var resultsOptipng = RunOptipng(path, pIsDebug);
						if (!string.IsNullOrWhiteSpace(resultsOptipng))
							result.AddNote(resultsOptipng);
						fi = new FileInfo(path);
						result.EndSize = fi.Length;
						var step1ms = sw.ElapsedMilliseconds;
						if (pIsDebug)
							result.AddNote("Size after Optipng: " + fi.Length + " bytes (" + (result.StartSize - result.EndSize) + " savings), took " + step1ms + " milliseconds");

						//last ditch effort to crush size using pngout
						var startSize = result.EndSize;
						var resultsPngout = RunPngout(path, pIsDebug);
						if (!string.IsNullOrWhiteSpace(resultsPngout))
							result.AddNote(resultsPngout);
						fi = new FileInfo(path);
						result.EndSize = fi.Length;
						if (pIsDebug)
							result.AddNote("Size after Pngout: " + fi.Length + " bytes (" + (startSize - result.EndSize) + " savings), took " + (sw.ElapsedMilliseconds - step1ms) + " milliseconds");
					}
                } 
                else if (isJpg)
                {
					if (maxFilesizeJpg > 0 && result.StartSize > maxFilesizeJpg)
					{
						result.AddNote("Skipping because filesize (" + result.StartSize + ") is greater than max (" + maxFilesizeJpg + ") bytes for " + fi.Name);
					}
					else
					{
						attemptedCompression = true;

						var resultsJpg = CompressJpg(path, pImageMagickExePath, null, pIsDebug, pRemoveTestImages);
						if (!string.IsNullOrWhiteSpace(resultsJpg))
							result.AddNote(resultsJpg);
						fi = new FileInfo(path);
						result.EndSize = fi.Length;
						var step1ms = sw.ElapsedMilliseconds;
						if (pIsDebug)
							result.AddNote("Size after JPG Compression: " + fi.Length + " bytes (" + (result.StartSize - result.EndSize) + " savings), took " + step1ms + " milliseconds");
					}
                }
				else if (isGif)
				{
					if (pIsDebug)
						result.AddNote("TODO GIF Compression");

					if (maxFilesizeGif > 0 && result.StartSize > maxFilesizeGif)
					{
						result.AddNote("Skipping because filesize (" + result.StartSize + ") is greater than max (" + maxFilesizeGif + ") bytes for " + fi.Name);
					}
					else
					{
						//attemptedCompression = true;

						//var resultsGif = CompressGif(path);
						//if (!string.IsNullOrWhiteSpace(resultsGif))
						//	result.AddNote(resultsGif);
						//fi = new FileInfo(path);
						//result.EndSize = fi.Length;
						//var step1ms = sw.ElapsedMilliseconds;
						//if (pIsDebug)
						//result.AddNote("Size after GIF Compression: " + fi.Length + " bytes (" + (result.StartSize - result.EndSize) + " savings), took " + step1ms + " milliseconds");
					}
				}
				else if (isWebp)
				{
					if (pIsDebug)
						result.AddNote("TODO WEBP Compression");


					//if (maxFilesizeWebp > 0 && result.StartSize > maxFilesizeWebp)
					//{
					//	result.AddNote("Skipping because filesize (" + result.StartSize + ") is greater than max (" + maxFilesizeWebp + ") bytes for " + fi.Name);
					//}
					//else
					//{
					//attemptedCompression = true;
					//	//var resultsWebp = CompressWebp(path);
					//	//if (!string.IsNullOrWhiteSpace(resultsWebp))
					//	//	result.AddNote(resultsWebp);
					//	//fi = new FileInfo(path);
					//	//result.EndSize = fi.Length;
					//	//var step1ms = sw.ElapsedMilliseconds;
					//	//result.AddNote("Size after WEBP Compression: " + fi.Length + " bytes (" + (result.StartSize - result.EndSize) +
					//	//               " savings), took " + step1ms + " milliseconds");
					//}
				}

                result.SizeDelta = result.StartSize - result.EndSize;

				if (attemptedCompression) //don't set the statusCode if we skipped it
				{
					if (result.SizeDelta > 0)
						result.StatusCode = 1;
					else
						result.StatusCode = 3;
				}
            }
			catch (FileNotFoundException ex)
			{
				result.Exception = ex;
				result.ErrorMessage = ex.Message;
				result.StatusCode = 5;
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
        public static string ToJson(List<ImageActionStatus> obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        /// <summary>
        /// Covert a json string back to our .NET object
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static List<ImageActionStatus> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<ImageActionStatus>>(json);
        }


		static string RunOptipng(string pImagePathInput, bool pIsDebug) 
        {
            string exePath = "optipng.exe";
            var output = ProcessHelper.RunProcessAndReturnOutput(exePath, "-o7 -v \"" + pImagePathInput + "\"");
			if (pIsDebug)
	            return output.Trim();
			return string.Empty;
        }

		static string RunPngout(string pImagePathInput, bool pIsDebug) 
        {
            string exePath = "pngout.exe";
            var output = ProcessHelper.RunProcessAndReturnOutput(exePath, "/y \"" + pImagePathInput + "\"");
			if (pIsDebug)
				return output.Trim();
			return string.Empty;
		}

        static string RunImageMagick(string pImageMagickExePath, string pImagePathInput, string pArguments, string pImagePathOutput = null, bool pIsDebug = false)
        {
            if (string.IsNullOrWhiteSpace(pImagePathOutput))
                pImagePathOutput = pImagePathInput; //default to overwrite original
            if (pArguments == null)
                pArguments = "";

            if (pIsDebug)
                pArguments = "-debug \"All\" " + pArguments;
            var args = pArguments + " \"" + pImagePathInput + "\" \"" + pImagePathOutput + "\"";

            string output = string.Empty;
            if (pIsDebug)
                output += pImageMagickExePath + " " + args + Environment.NewLine;

            string workingDir = null;
            if (pImageMagickExePath != null && pImageMagickExePath.Contains("\\"))
            {
                workingDir = pImageMagickExePath.Substring(0, pImageMagickExePath.LastIndexOf("\\"));

                var filename = Path.GetFileName(pImageMagickExePath);

                if (pIsDebug)
                {
                    output += "workingDir: " + workingDir + Environment.NewLine;
                    output += "exe filename: " + filename + Environment.NewLine;
                }
            }

            try
            {
                output += ProcessHelper.RunProcessAndReturnOutput(pImageMagickExePath, args, workingDir);
            }
            catch (Exception ex)
            {
                output += "ERROR running ImageMagick: " + ex.Message;
            }
            return output;
        }

        /// <summary>
        /// Compress a jpg by trying baseline and progressive options - keeping the smaller and overwriting the original file
        /// </summary>
        /// <param name="pImagePathInput"></param>
        /// <param name="pImageMagickExePath">C:\Program Files\ImageMagick-6.8.8-Q16\convert.exe</param>
        /// <param name="pImagePathOutput">if null/empty, overwrite original file</param>
        /// <param name="pIsDebug">if true, adds extra debug-level results text </param>
        /// <param name="pRemoveTestImages">if false, keep any generated test images</param>
        /// <returns></returns>
        static string CompressJpg(string pImagePathInput, string pImageMagickExePath, string pImagePathOutput = null, bool pIsDebug = false, bool pRemoveTestImages = true)
        {
            if (string.IsNullOrWhiteSpace(pImagePathOutput))
                pImagePathOutput = pImagePathInput; //default to overwrite original


            var sb = new StringBuilder();

            FileInfo fiSource = new FileInfo(pImagePathInput), fiBaseline = null, fiProgressive = null;

            var guid = Guid.NewGuid();
            var filepathBaseline = Path.Combine(fiSource.DirectoryName, fiSource.Name + "-" + guid + "-baseline.jpg");
            var filepathProgressive = Path.Combine(fiSource.DirectoryName, fiSource.Name + "-" + guid + "-progressive.jpg");

            var outputB = RunImageMagick(pImageMagickExePath, pImagePathInput, "-strip", filepathBaseline, pIsDebug);
            if (pIsDebug && !string.IsNullOrEmpty(outputB))
                sb.AppendLine(outputB);

            var outputP = RunImageMagick(pImageMagickExePath, pImagePathInput, "-strip -interlace Plane", filepathProgressive, pIsDebug);
            if (pIsDebug && !string.IsNullOrEmpty(outputP))
                sb.AppendLine(outputP);

            bool usingBaseline = false, usingProgressive = false;

            var smallestBytes = fiSource.Length;

            if (File.Exists(filepathBaseline))
            {
                fiBaseline = new FileInfo(filepathBaseline);
                if (fiBaseline.Length < smallestBytes)
                {
                    usingProgressive = false;
                    usingBaseline = true;
                    smallestBytes = fiBaseline.Length;
                }
            }

            if (File.Exists(filepathProgressive))
            {
                fiProgressive = new FileInfo(filepathProgressive);
                if (fiProgressive.Length < smallestBytes)
                {
                    usingBaseline = false;
                    usingProgressive = true;
                    smallestBytes = fiProgressive.Length;
                }
            }

            if (usingBaseline)
            {
				if (pIsDebug)
					sb.AppendLine("Compressed jpg using Baseline method. Saved " + (fiSource.Length - smallestBytes) + " bytes");
                ReplaceImage(fiSource, fiBaseline);
            }
            else if (usingProgressive)
            {
				if (pIsDebug)
	                sb.AppendLine("Compressed jpg using Progressive method. Saved " + (fiSource.Length - smallestBytes) + " bytes");
                ReplaceImage(fiSource, fiProgressive);
            }
            else
            {
				if (pIsDebug)
					sb.AppendLine("Unable to compress jpg further. " + pImagePathInput);
            }

            if (pRemoveTestImages)
            {
                File.Delete(filepathBaseline);
                File.Delete(filepathProgressive);
            }

            return sb.ToString();
        }


        /// <summary>
        /// Use pFiNew to replace pFiOriginal
        /// </summary>
        /// <param name="pFiOriginal"></param>
        /// <param name="pFiNew"></param>
        /// <returns></returns>
        private static bool ReplaceImage(FileInfo pFiOriginal, FileInfo pFiNew)
        {
            var targetFullName = pFiOriginal.FullName;
            var tempfile = pFiOriginal.Name + "-" + Guid.NewGuid() + pFiOriginal.Extension;
            bool tryMoveOriginalBack = false, success = false;
            try
            {
                pFiOriginal.MoveTo(tempfile);
                tryMoveOriginalBack = true;
                pFiNew.MoveTo(targetFullName);
                success = true;
                tryMoveOriginalBack = false;
            } 
            catch (Exception)
            {
                success = false;
            }

            try
            {
                if (!success && tryMoveOriginalBack)
                {
                    var tempFi = new FileInfo(tempfile);
                        tempFi.MoveTo(targetFullName); //failed to move the new file over, leave the original
                }
            }
            catch (Exception)
            {
                pFiOriginal.MoveTo(targetFullName); //failed to move the new file over, leave the original
            }

            try
            {
                if (success)
                    File.Delete(tempfile);
            }
            catch(Exception){}

            return success;
        }


		private static int ToInt(object o, int defaultValue = 0)
		{
			if (o == null)
				return defaultValue;
			int x;
			if (int.TryParse(o.ToString(), out x))
				return x;
			return defaultValue;
		}

		private static long ToLong(object o, long defaultValue = 0)
		{
			if (o == null)
				return defaultValue;
			long x;
			if (long.TryParse(o.ToString(), out x))
				return x;
			return defaultValue;
		}

		private static bool ToBool(object o, bool defaultValue)
        {
            if (o == null)
				return defaultValue;

			//bool x;
			var s = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(o.ToString().Trim().ToLower());
            if (string.IsNullOrWhiteSpace(s))
                return defaultValue;
            
			//Don't use bool.TryParse because it returns a value of false a lot
			//if (bool.TryParse(s, out x))
            //    return x;
			
			switch (s[0])
			{
				case '1':
				case 'Y':
				case 'T':
					return true;
				case '0':
				case 'N':
				case 'F':
					return false;
				default:
					return defaultValue;
			}
        }


		private static void ProgressIndicatorTimerCallback(object state)
		{
			if (_showProgressIndicator)
				ConsoleLogger.WriteNextSpinnerChar();
		}

    }
}
