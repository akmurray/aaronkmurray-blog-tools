using System;
using System.Collections.Generic;

namespace preprocessor
{
    internal class PreprocessResult
    {
        public PreprocessResult()
        {
            Notes = new List<string>();
        }

        public List<String> Notes;

        public Exception Exception { get; set; }

        public string ErrorMessage { get; set; }

        public object ElapsedMilliseconds { get; set; }

        public int StatusCode { get; set; }

        public void AddNote(string pNote)
        {
            Notes.Add(pNote);
        }
    }
}
