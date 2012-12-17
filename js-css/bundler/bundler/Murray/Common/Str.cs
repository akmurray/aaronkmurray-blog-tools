using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Murray.Common
{
    public static class Str
    {
        /// <summary>
        /// Postpends a string with a character up to pPlaces number of char places
        /// </summary>
        /// <param name="pValue">value to postpend to (right pad)</param>
        /// <param name="pPlaces">min number of places in the returned string</param>
        /// <param name="pAppendedChar">char to repeat at the end of pValue</param>
        /// <returns>Prepend("BALL", 5, '8') returns "BALL8". Prepend("BALL", 7, '8') returns "BALL888".</returns>
        public static string Postpend(string pValue, int pPlaces, char pAppendedChar)
        {
            if (pValue == null)
                pValue = string.Empty;
            while (pValue.Length < pPlaces)
                pValue = pValue + pAppendedChar;
            return pValue;
        }
        /// <summary>
        /// Postpends a string with a character up to pPlaces number of char places
        /// </summary>
        /// <param name="pValue">value to postpend to (right pad)</param>
        /// <param name="pPlaces">min number of places in the returned string</param>
        /// <param name="pAppendedString">string to repeat at the end of pValue</param>
        /// <returns>Prepend("BALL", 5, '88') returns "BALL88". Prepend("BALL", 7, '8') returns "BALL8888".</returns>
        public static string Postpend(string pValue, int pPlaces, string pAppendedString)
        {
            if (pValue == null)
                pValue = string.Empty;
            while (pValue.Length < pPlaces)
                pValue = pValue + pAppendedString;
            return pValue;
        }


        /// <summary>
        /// Get a formatted & trimmed string safely. Will return empty string instead of null
        /// </summary>
        /// <param name="pFormat"></param>
        /// <param name="pArgs"></param>
        /// <returns></returns>
        public static string FormatSafe(string pFormat, params object[] pArgs)
        {
            if (string.IsNullOrWhiteSpace(pFormat))
                return string.Empty;
            if (pArgs != null && pArgs.Length > 0)
                pFormat = string.Format(pFormat, pArgs); //only call string format if we have args - else it'll throw an exception
            pFormat = pFormat.Trim();
            return pFormat;
        }

        /// <summary>
        /// Safely converts possibly null string to a string. Empty string if necessary
        /// </summary>
        public static string ToString(object value, string pValueIfNull = "", string pValueIfCorrupt = "")
        {

            try
            {
                if (value == null)
                    return pValueIfNull;
                if (value is DateTime || value is string)
                    return value.ToString();

                return value.ToString();
            }
            catch (Exception ex)
            {
                ConsoleHelper.LogCaughtException(ex);
                return pValueIfCorrupt;
            }
        }

        /// <summary>
        /// Used when you want to get a bool from a string that may have T/F, Y/N, etc
        /// </summary>
        /// <param name="pString"></param>
        /// <param name="pValueIfStringIsNullOrEmpty"></param>
        /// <returns>True for: "Y", "YES", "ON", "1", "T", "TRUE"</returns>
        public static bool ToBool(object pString, bool pValueIfStringIsNullOrEmpty = false)
        {
            var s = ToString(pString).Trim().ToUpper();
            if (string.IsNullOrWhiteSpace(s))
                return pValueIfStringIsNullOrEmpty;

            return s == "1"
                || s == "T"
                || s == "Y"
                || s == "TRUE"
                || s == "YES"
                || s == "YEP"
                || s == "YEA"
                || s == "YEAH"
                || s == "ON"
                || s == "FOSHO"
            ;
        }
    }
}
