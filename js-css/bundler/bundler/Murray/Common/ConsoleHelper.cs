using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Murray.Common
{
    public static class ConsoleHelper
    {

        private const int MAX_CALLING_METHOD_NAME_DEPTH = 10;

        /// <summary>
        /// Safely log exception details to the console, with added notes about the calling method / stack info
        /// </summary>
        /// <param name="pException"></param>
        /// <param name="pNote"></param>
        /// <param name="pMaxCallingMethodNameDepth">Max number of calling method names to retrieve, excluding this method and any subcalls</param>
        public static void LogCaughtException(Exception pException, string pNote = null, int pMaxCallingMethodNameDepth = MAX_CALLING_METHOD_NAME_DEPTH)
        {
            const string segmentDelimeter = " | ";
            var msg = new StringBuilder();
            var methodNames = new List<string>();
            var paramInfo = new List<ParameterInfo[]>();

            msg.Append("LogCaughtException");

            var stackTrace = new StackTrace();
            var maxDepth = Math.Min(pMaxCallingMethodNameDepth + 1, stackTrace.FrameCount);

            for (var i = 1; i < maxDepth; i++) //i = 0 is this method - skip it
            {
                var stackFrame = stackTrace.GetFrame(i);
                var methodBase = stackFrame.GetMethod();
                methodNames.Add(methodBase.Name);
                paramInfo.Add(methodBase.GetParameters());
            }

            if (methodNames.Count > 0)
            {
                //calling method info
                msg.Append("Method: ");
                var mn = methodNames[0];
                var pis = paramInfo[0];
                msg.Append(segmentDelimeter + mn + "(");
                foreach (var pi in pis)
                    msg.Append(", ").Append(pi.Name).Append("={").Append(pi).Append("}");
                msg.Append(")");
            }

            if (!string.IsNullOrWhiteSpace(pNote))
                msg.Append(pNote);

            //parent calling method info
            if (methodNames.Count > 1)
            {
                const string subMethodDelimiter = " in ... ";
                msg.Append(segmentDelimeter + "Parent Methods: ");
                for (var i = 1; i < methodNames.Count; i++) //i = 0 is first method - skip it
                {
                    var mn = methodNames[i];
                    var pis = paramInfo[i];
                    msg.Append(subMethodDelimiter + mn + "(");
                    foreach (var pi in pis)
                        msg.Append(", ").Append(pi.Name).Append("={").Append(pi).Append("}");
                    msg.Append(")");
                    if (i < methodNames.Count - 1)
                        msg.Append(subMethodDelimiter);
                }
            }

            //exception info
            if (pException != null)
            {
                msg.Append(
                             segmentDelimeter + "Exception.Message: " + pException.Message
                           + segmentDelimeter + "Exception.TargetSite: " + pException.TargetSite
                           + segmentDelimeter + "Exception.StackTrace: " + pException.StackTrace
                );

                if (pException.InnerException != null)
                    msg.Append(
                                 segmentDelimeter + "Exception.InnerException.Message: " + pException.InnerException.Message
                               + segmentDelimeter + "Exception.InnerException.TargetSite: " + pException.InnerException.TargetSite
                               + segmentDelimeter + "Exception.InnerException.StackTrace: " + pException.InnerException.StackTrace
                    );

            }
            Console.WriteLine(Environment.NewLine + msg); //prefix with newline because we may have been writing inline text at the time
        }

        /// <summary>
        /// Safely log exception details to the console, with added notes about the calling method / stack info
        /// </summary>
        /// <param name="pException"></param>
        /// <param name="pNotes"></param>
        /// <param name="pMaxCallingMethodNameDepth">Max number of calling method names to retrieve, excluding this method and any subcalls</param>
        public static void LogCaughtException(Exception pException, IList<string> pNotes, int pMaxCallingMethodNameDepth = MAX_CALLING_METHOD_NAME_DEPTH)
        {
            string note = null;
            if (pNotes != null && pNotes.Any())
                note = string.Join(", ", pNotes);
            LogCaughtException(pException, note, pMaxCallingMethodNameDepth);
        }

        /// <summary>
        /// Safely log exception details to the console, with added notes about the calling method / stack info
        /// </summary>
        public static void LogCaughtExceptionFormat(Exception pException, string pFormat, params object[] pArgs)
        {
            string note = pFormat;
            if (pArgs != null && pArgs.Length > 0)
            {
                try
                {
                    note = Str.FormatSafe(pFormat, pArgs);
                }
                catch (Exception ex)
                {
                    LogCaughtException(ex, "bad format params"); //log so that we can fix caller, but we also want to continue to log original problem
                    note = pFormat;
                }
            }
            LogCaughtException(pException, note);
        }
    }
}
