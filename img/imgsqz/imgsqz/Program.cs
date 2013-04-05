using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            string searchPattern = "*.png|*.jpg|*.jpeg";
            string pathFileStatus = "filestatus.json";
            

            bool showHelp = false;
            bool showDebug = false;
            bool pauseWhenFinished = false;

            var p = new OptionSet() {
                { "s|pathSource=", "[optional, default=current folder]",  x => pathSource = x },
                { "f|forceRecalc=", "[optional, force a recalculation of all images, default=" + forceRecalc + "]",   x => forceRecalc = (x != null) || bool.Parse(x)},
                { "r|recurseDirs=", "[optional, recursively compress images in subfolders, default="+recurseDirs + "]",   x => recurseDirs = (x == null) || bool.Parse(x)},
                { "pat|searchPattern=", "[optional, filename pattern match expression, default="+searchPattern + "]",   x => searchPattern = x},
                { "pfs|pathFileStatus=", "[optional, file to store file compression status info, default="+pathFileStatus + "]",   x => pathFileStatus = x},
                

                //standard options for command line utils
                { "d|debug", "[optional, show debug details (verbose), default="+showDebug + "]",   x => showDebug = x != null},
                { "pause|pauseWhenFinished", "[optional, pause output window with a ReadLine when finished, default="+pauseWhenFinished + "]",   x => pauseWhenFinished = (x != null)},
                { "h|?|help", "show the help options",   x => showHelp = x != null },
            };
            List<string> extraArgs = p.Parse(args);


            if (string.IsNullOrWhiteSpace(pathSource))
                pathSource = IOHelper.GetCurrentDirectory(); //default if not specified

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
            Console.WriteLine("imgsqz by @AaronKMurray");
            if (showDebug)
            {
                Console.WriteLine("using options:");
                Console.WriteLine("\tpathSource:\t" + pathSource);
                Console.WriteLine("\trecurseDirs:\t" + recurseDirs);
                Console.WriteLine("\tsearchPattern:\t" + searchPattern);
                Console.WriteLine("\tforceRecalc:\t" + forceRecalc);
                Console.WriteLine("\tpathFileStatus:\t" + pathFileStatus);
                Console.WriteLine();
            }

            // --- FIND THE FILES TO WORK ON --- //

            var searchOption = SearchOption.AllDirectories;
            if (!recurseDirs)
                searchOption = SearchOption.TopDirectoryOnly;

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
                Console.WriteLine("Error finding files in: " + pathSource);
                Console.WriteLine(ex.Message);
                return (int)ExitCode.Error;
            }

            if (showDebug)
                for (int i=0; i<filepaths.Length;i++)
                    Console.WriteLine("{0}: {1}", i + 1, filepaths[i]);

            Console.WriteLine("Matched {0} file(s) to compress", filepaths.Length);


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
                Console.WriteLine("Error creating status file: " + pathFileStatus);
                Console.WriteLine(ex.Message);
                return (int)ExitCode.Error;
            }

            try
            {
                fileStatuses = FromJson(IOHelper.ReadASCIITextFile(pathFileStatus));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading status file: " + pathFileStatus);
                Console.WriteLine(ex.Message);
                //ugh, quick hack just in case
                File.Delete(pathFileStatus);
                IOHelper.WriteTextFile(pathFileStatus, ToJson(new List<ImageActionStatus>()));
                return (int)ExitCode.Error;
            }


            //go through the matched images, and ensure that we have the proper entry/status set
            foreach (var filepath in filepaths)
            {
                var status = fileStatuses.FirstOrDefault(x => x.Path == filepath);
                if (status == null)
                    fileStatuses.Add(new ImageActionStatus { Path = filepath, StatusCode = 0, FileSize = 0});
            }



            // --- BEGIN THE COMPRESSION WORK --- //
            int compressedCount = 0;
            long compressionSavings = 0;

            var po = new ParallelOptions();
            po.MaxDegreeOfParallelism = -1; // If MaxDegreeOfParallelism is -1, then there is no limit placed on the number of concurrently running operations.
            Parallel.ForEach(filepaths, po, filepath =>
            {
                var status = fileStatuses.First(x => x.Path == filepath);
                var fi = new FileInfo(filepath);
                if (status.StatusCode == 0 || status.FileSize != fi.Length || forceRecalc) //we haven't tried to compress this yet, or the image has changed since we last tried
                {
                    //let's try to compress this image

                    CompressionResult compressionResult = TryCompress(status.Path);
                    status.StatusCode = compressionResult.StatusCode;
                    if (status.StatusCode == 1)
                    {
                        //thread-safe updating of out-of-current-scope variables
                        Interlocked.Increment(ref compressedCount);
                        Interlocked.Add(ref compressionSavings, compressionResult.SizeDelta);
                    }

                    if (!string.IsNullOrWhiteSpace(compressionResult.ErrorMessage) || compressionResult.Exception != null)
                    {
                        Console.WriteLine("ERROR: " + filepath);
                        Console.WriteLine(compressionResult.ErrorMessage);
                        Console.WriteLine(compressionResult.Exception);
                    }

                    fi = new FileInfo(filepath);
                    status.FileSize = fi.Length;

                    if (showDebug)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine(string.Format("Compression complete in {1} ms. New status: {2}. Bytes saved: {3}. {0}",
                                          status.Path, compressionResult.ElapsedMilliseconds, status.StatusCode, compressionResult.SizeDelta));
                        
                        if (compressionResult.Notes.Any())
                            sb.AppendLine("\t" + compressionResult.Notes.Count + " notes: ");
                        foreach (var note in compressionResult.Notes)
                            sb.AppendLine("\t\t" + note);

                        Console.WriteLine(sb.ToString());
                    }

                    try
                    {
                        //save current status in case the program gets killed before run completes. Eat multi-threaded IO exceptions here because this isn't a critical save
                        lock (_lock_fileStatuses)
                            IOHelper.WriteTextFile(pathFileStatus, ToJson(fileStatuses));
                    } catch { }

                } 
                else
                {
                    if (showDebug)
                        Console.WriteLine("Status {0} [{1}], skipping: {2}", status.StatusCode, status.GetStatusMessage(), status.Path);
                }
            });


            // --- SAVE OUR CURRENT FILE STATUS --- //
            lock (_lock_fileStatuses)
                IOHelper.WriteTextFile(pathFileStatus, ToJson(fileStatuses));

            Console.WriteLine();
            Console.WriteLine(string.Format("Compressed {0} files for a total savings of {1} bytes", compressedCount, compressionSavings));

            if (showDebug)
            {
                Console.WriteLine("Complete at " + DateTime.Now.ToLongTimeString() + ". Took " + DateTime.Now.Subtract(_startTime).TotalSeconds +" seconds to run");
            }

            if (pauseWhenFinished)
            {
                Console.WriteLine("Press any key to complete");
                Console.ReadLine(); //just here to pause the output window during testing
            }
            return (int)ExitCode.Success;
        }

        private static CompressionResult TryCompress(string path)
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
                var isGif = ext.EndsWith("gif");
                var isWebp = ext.EndsWith("webp");

                result.StartSize = fi.Length;

                if (isPng)
                {

                    //first try optipng
                    var resultsOptipng = RunOptipng(path);
                    result.AddNote(resultsOptipng);
                    fi = new FileInfo(path);
                    result.EndSize = fi.Length;
                    var step1ms = sw.ElapsedMilliseconds;
                    result.AddNote("Size after Optipng: " + fi.Length + " bytes (" + (result.StartSize - result.EndSize) +
                                   " savings), took " + step1ms + " milliseconds");

                    //last ditch effort to crush size using pngout
                    var startSize = result.EndSize;
                    var resultsPngout = RunPngout(path);
                    result.AddNote(resultsPngout);
                    fi = new FileInfo(path);
                    result.EndSize = fi.Length;
                    result.AddNote("Size after Pngout: " + fi.Length + " bytes (" + (startSize - result.EndSize) +
                                   " savings), took " + (sw.ElapsedMilliseconds - step1ms) + " milliseconds");

                } 
                else if (isJpg)
                {
                    var resultsJpg = CompressJpg(path);
                    result.AddNote(resultsJpg);
                    fi = new FileInfo(path);
                    result.EndSize = fi.Length;
                    var step1ms = sw.ElapsedMilliseconds;
                    result.AddNote("Size after JPG Compression: " + fi.Length + " bytes (" + (result.StartSize - result.EndSize) +
                                   " savings), took " + step1ms + " milliseconds");

                }
                else if (isGif)
                {
                    result.AddNote("TODO GIF Compression");

                    //var resultsGif = CompressGif(path);
                    //result.AddNote(resultsGif);
                    //fi = new FileInfo(path);
                    //result.EndSize = fi.Length;
                    //var step1ms = sw.ElapsedMilliseconds;
                    //result.AddNote("Size after GIF Compression: " + fi.Length + " bytes (" + (result.StartSize - result.EndSize) +
                    //               " savings), took " + step1ms + " milliseconds");

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


        static string RunOptipng(string pImagePathInput) 
        {
            string exePath = "optipng.exe";
            var output = ProcessHelper.RunProcessAndReturnOutput(exePath, "-o7 -v \"" + pImagePathInput + "\"");
            return output;
        }

        static string RunPngout(string pImagePathInput) 
        {
            string exePath = "pngout.exe";
            var output = ProcessHelper.RunProcessAndReturnOutput(exePath, "/y \"" + pImagePathInput + "\"");
            return output;
        }

        static string RunImageMagick(string pImagePathInput, string pArguments, string pImagePathOutput = null)
        {
            if (string.IsNullOrWhiteSpace(pImagePathOutput))
                pImagePathOutput = pImagePathInput; //default to overwrite original

            string exePath = "convert.exe";
            var output = ProcessHelper.RunProcessAndReturnOutput(exePath, pArguments + " \"" + pImagePathInput + "\" \"" + pImagePathOutput + "\"");
            return output;
        }

        /// <summary>
        /// Compress a jpg by trying baseline and progressive options - keeping the smaller and overwriting the original file
        /// </summary>
        /// <param name="pImagePathInput"></param>
        /// <param name="pImagePathOutput">if null/empty, overwrite original file</param>
        /// <param name="pIsDebug">if true, adds extra debug-level results text </param>
        /// <returns></returns>
        static string CompressJpg(string pImagePathInput, string pImagePathOutput = null, bool pIsDebug = false)
        {
            if (string.IsNullOrWhiteSpace(pImagePathOutput))
                pImagePathOutput = pImagePathInput; //default to overwrite original


            var sb = new StringBuilder();

            FileInfo fiSource = new FileInfo(pImagePathInput), fiBaseline = null, fiProgressive = null;

            var guid = Guid.NewGuid();
            var filepathBaseline = fiSource.Name + "-" + guid + "-baseline.jpg";
            var filepathProgressive = fiSource.Name + "-" + guid + "-progressive.jpg";

            var outputB = RunImageMagick(pImagePathInput, "-strip", filepathBaseline);
            if (pIsDebug)
                sb.AppendLine(outputB);

            var outputP = RunImageMagick(pImagePathInput, "-strip -interlace Plane", filepathProgressive);
            if (pIsDebug)
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
                sb.AppendLine("Compressed jpg using Baseline method. Saved " + (fiSource.Length - smallestBytes) + " bytes");
                ReplaceImage(fiSource, fiBaseline);
            }
            else if (usingProgressive)
            {
                sb.AppendLine("Compressed jpg using Progressive method. Saved " + (fiSource.Length - smallestBytes) + " bytes");
                ReplaceImage(fiSource, fiProgressive);
            }
            else
            {
                sb.AppendLine("Unable to compress jpg further. " + pImagePathInput);
            }

            File.Delete(filepathBaseline);
            File.Delete(filepathProgressive);

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
            } 
            catch (Exception)
            {
                success = false;
            }

            if (!success && tryMoveOriginalBack)
            {
                pFiOriginal.MoveTo(targetFullName); //failed to move the new file over, leave the original
            }

            try
            {
                File.Delete(tempfile);
            }
            catch(Exception){}

            return success;
        }


        
    }
}
