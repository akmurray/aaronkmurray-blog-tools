using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Murray.Common
{
    public static class IOHelper
    {
        /// <summary>
        /// Writes a string to a text file
        /// </summary>
        /// <param name="path">file to be written</param>
        /// <param name="text">text to be written</param>
        public static void WriteTextFile(string path, string text)
        {
            FileStream fs = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            var sw = new StreamWriter(fs);
            sw.Write(text);
            sw.Close();
        }

        /// <summary>
        /// This method uses about 50% less RAM than the regular ReadTextFile...
        /// </summary>
        /// <param name="path">file to be read</param>
        /// <returns>a string containing the contents of the text file</returns>
        public static string ReadASCIITextFile(string path)
        {
            using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] buffer = ReadFullStream(fs);
                string result = Encoding.ASCII.GetString(buffer);
                if (result.Length > 1000000) // ReadFullStream creates a large temporary buffer and we don't want to wait too long to get rid of it.
                {
                    buffer = null;
                    GC.Collect();
                }
                return result;
            }
        }

        /// <summary>
        /// Returns a byte array with the entire contents of the stream
        /// </summary>
        public static byte[] ReadFullStream(Stream stream)
        {
            if (stream is MemoryStream)
                return ((MemoryStream)stream).ToArray();

            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public static string GetCurrentDirectory()
        {
            string path = Path.GetTempFileName(); //just in case
            bool success = false;
            try
            {
                path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                success = true;
            }
            catch { }

            if (!success)
            {
                try
                {
                    path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);
                    success = true;
                }
                catch { }
            }
            return path.Replace("file:\\", string.Empty);
        }


        /// <summary>
        /// Ensures that the full path (all directories) to the given filename exists,
        /// so that the file can be created without any problems.
        /// This usually takes under 5ms, but can take up to 80ms or more.
        /// Will throw exceptions if the path is invalid, 
        /// or if security prevents the directory from being created.
        /// </summary>
        /// <param name="pPathAndFilename">path to create directories for. With or without filename</param>
        /// <param name="pUseCachedResultsMs">If > 0, then will use cached results for up to X ms (useful when doing a lot of file processing in a single folder). Using cache saves an avg of 1 second per 300 calls</param>
        public static void EnsurePathToFile(string pPathAndFilename, int pUseCachedResultsMs = 0)
        {
            //don't do any arg checking...let exceptions get thrown
            var dir = Path.GetDirectoryName(pPathAndFilename);

            if (pUseCachedResultsMs > 0 //we want to use cache
                && dir != null          //don't throw exceptions from a dictionary fail...wait and throw later with a more relevant directory exception
                && _EnsurePathToFileCache.ContainsKey(dir) //we have a cached result
                && DateTime.Now.Subtract(_EnsurePathToFileCache[dir]).TotalMilliseconds <= pUseCachedResultsMs //it was created within our acceptable cache length window
                )
            {
                return;
            }

            if (!Directory.Exists(dir))
                //this check saves over 50% of the overhead vs calling CreateDirectory() blindly
                Directory.CreateDirectory(dir);

            if (pUseCachedResultsMs > 0)
                _EnsurePathToFileCache[dir] = DateTime.Now;
                    //only save this if we're interested in using the cache...otherwise we'll fill up from calls that don't want to use cache
        }

        /// <summary>
        /// Key is path (no filename)
        /// Value is when path was last verified
        /// </summary>
        private static IDictionary<string, DateTime> _EnsurePathToFileCache = new ConcurrentDictionary<string, DateTime>();

    }
}
