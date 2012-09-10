using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace imgsqz
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


    }
}
