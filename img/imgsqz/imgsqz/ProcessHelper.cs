using System;
using System.Diagnostics;
using System.IO;

namespace imgsqz
{
    public static class ProcessHelper
    {
        public static string RunProcessAndReturnOutput(string pExeFilename, string pCommandLineArgs, string pExeWorkingDirectory = null)
        {

            var process = new Process();
            if (!string.IsNullOrEmpty(pExeWorkingDirectory))
                process.StartInfo.WorkingDirectory = pExeWorkingDirectory;
            process.StartInfo.FileName = pExeFilename;
            process.StartInfo.UseShellExecute = false;
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
                throw new Exception(pExeWorkingDirectory + pExeFilename + " failed to start with args: " + pCommandLineArgs);
            }
            return output;
        }
    }
}
