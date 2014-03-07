namespace imgsqz
{
    public class ImageActionStatus
    {
        /// <summary>
        /// Full path and filename
        /// </summary>
        public string Path;

        /// <summary>
        /// 0 = Unknown,
        /// 1 = Success,
        /// 2 = Skip compression,
        /// 3 = Unable to compress further,
		/// 4 = Exception during compression,
		/// 5 = File not found,
		/// </summary>
        public int StatusCode;

		/// <summary>
		/// FileSize as of last check
		/// </summary>
		public long FileSize;
		
		public long OriginalFileSize;

		/// <summary>
		/// Will be populated if an error happened during compression attempt
		/// </summary>
		public string ErrorMessage;

		public string GetStatusMessage()
        {
            switch (StatusCode)
            {
                case 1:
                    return "Success";
                case 2:
                    return "Skip compression";
                case 3:
                    return "Unable to compress further";
				case 4:
					return "Error during compression";
				case 5:
					return "File not found";
			}
            return "Unknown Status";
        }
    }
}