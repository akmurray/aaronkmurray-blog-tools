using System;
using System.Diagnostics;

namespace imgsqz
{
    public static class ProcessHelper
    {
        public static string RunProcessAndReturnOutput(string pExePath, string pCommandLineArgs)
        {

            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = pExePath;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.Arguments = pCommandLineArgs;

            string output;
            if (process.Start())
            {
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
            }
            else
            {
                throw new Exception(pExePath + " failed to start with args: " + pCommandLineArgs);
            }
            return output;
        }
    }
}
