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
    /// </summary>
    class Program
    {

        private static object _lock_fileStatuses = new object();

        static void Main(string[] args)
        {
            var _startTime = DateTime.Now;
            string pathSource = null;
            bool forceRecalc = false;
            bool recurseDirs = true;
            string searchPattern = "*.png";
            string pathFileStatus = "filestatus.json";
            

            bool showHelp = false;
            bool showDebug = false;
            bool pauseWhenFinished = false;

            var p = new OptionSet() {
                { "s|pathSource=", "[optional, default=current folder]",  x => pathSource = x },
                { "f|forceRecalc", "[optional, force a recalculation of all images, default=" + forceRecalc + "]",   x => forceRecalc = (x != null) || bool.Parse(x)},
                { "r|recurseDirs", "[optional, recursively compress images in subfolders, default="+recurseDirs + "]",   x => recurseDirs = (x == null) || bool.Parse(x)},
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
                return;
            }



            Console.WriteLine();
            Console.WriteLine("imgsqz by @AaronKMurray using options:");
            Console.WriteLine("\tpathSource:\t" + pathSource);
            Console.WriteLine("\trecurseDirs:\t" + recurseDirs);
            Console.WriteLine("\tsearchPattern:\t" + searchPattern);
            Console.WriteLine("\tforceRecalc:\t" + forceRecalc);
            Console.WriteLine("\tpathFileStatus:\t" + pathFileStatus);
            Console.WriteLine();


            // --- FIND THE FILES TO WORK ON --- //

            var searchOption = SearchOption.AllDirectories;
            if (!recurseDirs)
                searchOption = SearchOption.TopDirectoryOnly;

            string[] filepaths;
            try
            {
                filepaths = Directory.GetFiles(pathSource, searchPattern, searchOption);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error finding files in: " + pathSource);
                Console.WriteLine(ex.Message);
                return;
            }

            if (showDebug)
                for (int i=0; i<filepaths.Length;i++)
                    Console.WriteLine("{0}: {1}", i + 1, filepaths[i]);

            Console.WriteLine("Matched {0} files to compress", filepaths.Length);


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
                return;
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
                return;
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
        }

        private static CompressionResult TryCompress(string path)
        {
            var sw = new Stopwatch();
            var result = new CompressionResult();

            try
            {
                sw.Start();

                var fi = new FileInfo(path);
                result.StartSize = fi.Length;

                //first try optipng
                var resultsOptipng = RunOptipng(path);
                result.AddNote(resultsOptipng);
                fi = new FileInfo(path);
                result.EndSize = fi.Length;
                var step1ms = sw.ElapsedMilliseconds;
                result.AddNote("Size after Optipng: " + fi.Length + " bytes (" + (result.StartSize - result.EndSize) + " savings), took " + sw.ElapsedMilliseconds + " milliseconds");

                //last ditch effort to crush size using pngout
                var startSize = result.EndSize;
                var resultsPngout = RunPngout(path);
                result.AddNote(resultsPngout);
                fi = new FileInfo(path);
                result.EndSize = fi.Length;
                result.AddNote("Size after Pngout: " + fi.Length + " bytes (" + (startSize - result.EndSize) + " savings), took " + (sw.ElapsedMilliseconds - step1ms) + " milliseconds");

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

        
        static string RunOptipng(string pImagePath) 
        {
            string pngoutExePath = "optipng.exe";
            var output = ProcessHelper.RunProcessAndReturnOutput(pngoutExePath, "-o7 -v " + pImagePath);
            return output;
        }

        static string RunPngout(string pImagePath) 
        {
            string pngoutExePath = "pngout.exe";
            var output = ProcessHelper.RunProcessAndReturnOutput(pngoutExePath, "/y " + pImagePath);
            return output;
        }


        
    }
}
