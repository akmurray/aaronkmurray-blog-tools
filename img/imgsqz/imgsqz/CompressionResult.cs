using System;
using System.Collections.Generic;

namespace imgsqz
{
    internal class CompressionResult
    {
        public CompressionResult()
        {
            Notes = new List<string>();
        }

		/// <summary>
		/// Ordered list of notes during compression. Each element is a "line"
		/// </summary>
        public List<String> Notes;

		/// <summary>
		/// Original size in Bytes
		/// </summary>
		public long StartSize { get; set; }

		/// <summary>
		/// Final size in Bytes
		/// </summary>
        public long EndSize { get; set; }

		/// <summary>
		/// Null if no exception occurred
		/// </summary>
        public Exception Exception { get; set; }

		/// <summary>
		/// Null if no exception occurred
		/// </summary>
		public string ErrorMessage { get; set; }

		/// <summary>
		/// Bytes saved. Positve means file size was reduced. Negative values mean the compression failed and the file actually got larger.
		/// </summary>
        public long SizeDelta { get; set; }

		/// <summary>
		/// Total milliseconds that it took to complete the full compression
		/// </summary>
        public object ElapsedMilliseconds { get; set; }

		/// <summary>
		/// 1: Compressed
		/// 2: 
		/// 3: Compression attempted, but didn't reduce file size
		/// 4: Error during compression
		/// </summary>
        public int StatusCode { get; set; }

        public void AddNote(string pNote)
        {
            Notes.Add(pNote);
        }
    }
}