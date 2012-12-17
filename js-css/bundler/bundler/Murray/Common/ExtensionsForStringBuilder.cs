using System.Linq;
using System.Text;

namespace Murray.Common
{
    public static class ExtensionsForStringBuilder
    {
        /// <summary>
        /// Appends a NewLine after the format string. Safe formatting for null/empty pArgs.
        /// </summary>
        /// <returns></returns>
        public static void AppendLineFormat(this StringBuilder pSb, string pFormat, params object[] pArgs)
        {
            if (pArgs != null && pArgs.Any())
            {
                pSb.AppendFormat(pFormat, pArgs);
                pSb.AppendLine();
            }
            else
                pSb.AppendLine(pFormat);
        }
    }
}
