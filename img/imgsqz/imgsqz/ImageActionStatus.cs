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
        /// </summary>
        public int StatusCode;

        public long FileSize;

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
                    return "Exception during compression";
            }
            return "Unknown Status";
        }
    }
}