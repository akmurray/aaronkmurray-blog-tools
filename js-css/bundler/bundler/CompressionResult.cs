using System;
using System.Collections.Generic;

namespace bundler
{
    internal class CompressionResult
    {
        public CompressionResult()
        {
            Notes = new List<string>();
        }

        public List<String> Notes;

        public long StartSize { get; set; }

        public long EndSize { get; set; }

        public Exception Exception { get; set; }

        public string ErrorMessage { get; set; }

        public long SizeDelta { get; set; }

        public object ElapsedMilliseconds { get; set; }

        public int StatusCode { get; set; }

        public void AddNote(string pNote)
        {
            Notes.Add(pNote);
        }
    }
}