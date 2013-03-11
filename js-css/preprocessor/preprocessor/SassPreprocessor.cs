using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using SassAndCoffee.Ruby.Sass;
using murray.common;

namespace preprocessor
{
    internal static class SassPreprocessor
    {
        internal static PreprocessResult Run(IList<string> pSourceFilePaths, string pOutputFilePath,
        bool pCompressFileContents = true, string pHeaderComment = null, bool pIncludeGenDateInHeaderComment = true)
        {
            var sw = new Stopwatch();
            var result = new PreprocessResult();

            try
            {
                sw.Start();

                var fi = new FileInfo(pSourceFilePaths.First());
                var ext = fi.Extension.ToLower();
                //var isSass = ext.EndsWith("sass");
                var isScss = ext.EndsWith("scss");

                if (isScss)
                {
                    var results = RunForScss(pSourceFilePaths, pOutputFilePath, pCompressFileContents, pHeaderComment, pIncludeGenDateInHeaderComment);
                    if (!string.IsNullOrWhiteSpace(results))
                        result.AddNote(results);
                    var step1ms = sw.ElapsedMilliseconds;
                    result.AddNote("Scss Conversion Results: took " + step1ms + " milliseconds");
                }
                else
                {
                    result.AddNote("SassPreprocesser failed to convert: " + ext);
                }

                result.StatusCode = 1;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
                result.ErrorMessage = ex.Message;

                string marker = "Sass Template:";
                if (result.ErrorMessage.Contains(marker))
                    result.ErrorMessage = result.ErrorMessage.Substring(0, result.ErrorMessage.IndexOf(marker));
                result.StatusCode = 4;
                result.AddNote("SassPreprocesser failed");
            }
            finally
            {
                sw.Stop();
                result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            }
            return result;


        }

        private static string RunForScss(IList<string> pSourceFilePaths, string pOutputFilePath, bool pCompressFileContents, string pHeaderComment, bool pIncludeGenDateInHeaderComment)
        {
            var dependencies = new List<string>();
            string results = "";

            foreach (var sourcePath in pSourceFilePaths)
            {
                using (var compiler = new SassCompiler())
                {
                    var compiled = compiler.Compile(sourcePath, pCompressFileContents, dependencies);

                    // add prefix, etc
                    compiled = string.Format("{1}{0}{0}{2}"
                        , pCompressFileContents ? string.Empty : Environment.NewLine
                        , str.ToString(pHeaderComment)
                        , compiled
                        );

                    string outPath;
                    if (string.IsNullOrWhiteSpace(pOutputFilePath))
                    {
                        //derive path/name from source file
                        outPath = Path.Combine(Path.GetDirectoryName(sourcePath), Path.GetFileNameWithoutExtension(sourcePath) + ".css");
                    }
                    else
                    {
                        outPath = pOutputFilePath;
                    }

                    io.EnsurePathToFile(outPath);
                    io.WriteTextFile(outPath, compiled);

                    results += string.Format("SASS compiled for [{0}] to [{1}]", sourcePath, outPath);
                }
            }
            return results;
        }
    }
}
