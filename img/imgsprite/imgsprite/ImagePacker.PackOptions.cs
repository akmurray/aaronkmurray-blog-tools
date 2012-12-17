using System.Collections.Generic;
using System.Drawing.Imaging;

namespace imgsprite
{
    partial class ImagePacker
    {
        /// <summary>
        /// Options used by ImagePacker.CombineImages()
        /// </summary>
        public class PackOptions
        {
            /// <summary>
            /// w/ Defaults
            /// </summary>
            public PackOptions()
            {
                OutputImageFormat = ImageFormat.Png;
                //ValidSpriteReplacementCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-_";
                ValidSpriteClassNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-_0123456789";
                TestHtmlHeadIncludes = "<script src='http://files.aaronkmurray.com/blog-tools/imgsprite/sorttable.js'></script>";
                TestHtmlClearGifUrl = "http://files.aaronkmurray.com/blog-tools/imgsprite/clear.gif";
                TestHtmlFilenameSuffix = "_test.html";
                TreatInputFileNotFoundAsError = true;
                LimitOutputImageTo8Bits = false;

                ImageFilePaths = new List<string>();

                //This has the one-time values for the entries: CSS_PLACEHOLDER_IMAGE_DEPLOY_URL_BASE, CSS_PLACEHOLDER_CSS_CLASS_NAME_PREFIX, CSS_PLACEHOLDER_CSS_CLASS_NAME_BASE, CSS_PLACEHOLDER_CSS_CLASS_NAME_SUFFIX, 
                CssPlaceholderValues = new Dictionary<string, string>();

                //single value for all sprites - values stored in CssPlaceholderValues
                CSS_PLACEHOLDER_IMAGE_DEPLOY_URL_BASE = "[ImageDeployUrlBase]";
                CSS_PLACEHOLDER_CSS_CLASS_NAME_PREFIX = "[CssClassNamePrefix]";
                CSS_PLACEHOLDER_CSS_CLASS_NAME_BASE = "[CssClassNameBase]";
                CSS_PLACEHOLDER_CSS_CLASS_NAME_SUFFIX = "[CssClassNameSuffix]";

                //calculated for each sprite
                CSS_PLACEHOLDER_IMAGE_FILE_NAME = "[ImageFileName]";
                CSS_PLACEHOLDER_WIDTH = "[Width]";
                CSS_PLACEHOLDER_HEIGHT = "[Height]";
                CSS_PLACEHOLDER_OFFSET_X = "[OffsetX]";
                CSS_PLACEHOLDER_OFFSET_Y = "[OffsetY]";
            }


            /// <summary>
            /// Input paths for all images that will be placed in the sprite
            /// </summary>
            public List<string> ImageFilePaths;
            /// <summary>
            /// path for newly created sprite css file
            /// </summary>
            public string CssOutputFilePath;
            /// <summary>
            /// path for the newly created sprite image file
            /// </summary>
            public string ImageOutputFilePath;
            
            /// <summary>
            /// Format that the file in ImageOutputFilePath will be rendered as
            /// </summary>
            public ImageFormat OutputImageFormat;


            /// <summary>
            /// 
            /// </summary>
            public Dictionary<string, string> CssPlaceholderValues;

            /// <summary>
            /// Valid chars for sprite class names
            /// </summary>
            public string ValidSpriteClassNameCharacters;

            /// <summary>
            /// Replacement char to use when removing chars that aren't in ValidSpriteClassNameCharacters
            /// </summary>
            public string CssClassNameInvalidCharReplacement;

            /// <summary>
            /// If true, the output image will be reduced to 8bits
            /// </summary>
            public bool LimitOutputImageTo8Bits;

            /// <summary>
            /// if true, a test html page will be generated to view all of the sprites
            /// </summary>
            public bool GenerateTestHtmlPage;
            /// <summary>
            /// optional output path for test html file. Useful if the CSS_PLACEHOLDER_IMAGE_DEPLOY_URL_BASE is not a full url
            /// </summary>
            public string TestHtmlPath;
            /// <summary>
            /// optional - will override the value in CSS_PLACEHOLDER_IMAGE_DEPLOY_URL_BASE
            /// </summary>
            public string TestHtmlImageDeployUrlBase;
            /// <summary>
            /// Allows override of default script include in test page
            /// </summary>
            public string TestHtmlHeadIncludes;
            /// <summary>
            /// Allows override of clear gif url stub
            /// </summary>
            public string TestHtmlClearGifUrl;

            /// <summary>
            /// Allows override of test html file page name suffix. Default is "_test.html"
            /// </summary>
            public string TestHtmlFilenameSuffix;

            /// <summary>
            /// If true, whenever a file in ImageFilePaths is not found, execution will stop resulting in an error
            /// </summary>
            public bool TreatInputFileNotFoundAsError;

            /// <summary>
            /// Format string for output css class definition for each image in the css sprite 
            /// </summary>
            public string CssFormatStringForSpriteDefinition;


            /*
             * These CSS_PLACEHOLDER_XXX variables are mapped to specific variables that will be used to generate the output css file.
             * Use the strings set in these in: CssFormatStringForSpriteDefinition
             * */
            public string CSS_PLACEHOLDER_CSS_CLASS_NAME_PREFIX;
            public string CSS_PLACEHOLDER_CSS_CLASS_NAME_BASE;
            public string CSS_PLACEHOLDER_CSS_CLASS_NAME_SUFFIX;
            public string CSS_PLACEHOLDER_IMAGE_FILE_NAME;
            public string CSS_PLACEHOLDER_WIDTH;
            public string CSS_PLACEHOLDER_HEIGHT;
            public string CSS_PLACEHOLDER_OFFSET_X;
            public string CSS_PLACEHOLDER_OFFSET_Y;
            public string CSS_PLACEHOLDER_IMAGE_DEPLOY_URL_BASE;

            /// <summary>
            /// Text to prepend to css file. Can be used for extra pre-styles or comments
            /// </summary>
            public string CssHeaderText { get; set; }
        }

    }
}
