using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using ImageManipulation;

namespace imgsprite
{
    public partial class ImagePacker
    {


        /// <summary>
        /// Turn a list of images into a packed sprite image and matching css file
        /// </summary>
        /// <param name="pOptions"></param>
        /// <param name="oResults">result messages</param>
        /// <returns>true/false for success</returns>
        public static bool CombineImages(PackOptions pOptions, out List<string> oResults)
        {
            oResults = new List<string>();

            long inputFileTotalSize = 0;
            long outputFileSize = 0;
            long areaTotalImages = 0;
            long areaTotalFrames = 0;
            long areaImage = 0;
            long areaFrame = 0;
            int widestImage = 0;
            int tallestImage = 0;

            var cssBuffer = new StringBuilder();
            var testPage = new StringBuilder();


            if (!string.IsNullOrWhiteSpace(pOptions.CssHeaderText))
                cssBuffer.AppendLine(pOptions.CssHeaderText);

            pOptions.ImageFilePaths.Sort(); //doing this as a debugging test

            var frameArray = new List<ImageFrame>();
            foreach (string imageFilePath in pOptions.ImageFilePaths)
            {
                if (File.Exists(imageFilePath))
                {
                    inputFileTotalSize += new FileInfo(imageFilePath).Length;

                    using (var image = Image.FromFile(imageFilePath))
                    {
                        var frame = new ImageFrame(imageFilePath, image.Width, image.Height);
                        frameArray.Add(frame);
                        #region Debugging and Error Checking
                        if (image.Width > widestImage)
                            widestImage = image.Width;
                        if (image.Height > tallestImage)
                            tallestImage = image.Height;

                        areaImage = (image.Width * image.Height);
                        areaFrame = (frame.Width * frame.Height);

                        areaTotalImages += areaImage;
                        areaTotalFrames += areaFrame;

                        if (image.Width != frame.Width
                            || image.Height != frame.Height
                            || areaImage != frame.Area)
                        {
                            oResults.Add(string.Format("Error: Image>Frame conversion size is incorrect: {0} ", imageFilePath));
                            oResults.Add(string.Format("H/W/A: Image:{0}/{1}/{2}, Frame:{3}/{4}/{5}"
                                , image.Height, image.Width, areaImage
                                , frame.Height, frame.Width, frame.Area
                                ));
                        }
                        #endregion
                    }
                }
                else
                {
                    oResults.Add(string.Format("Could not find file: {0} ", imageFilePath));
                    if (pOptions.TreatInputFileNotFoundAsError)
                        return false;
                }
            }

            long minArea = tallestImage * widestImage;
            if (areaTotalFrames > minArea)
                minArea = areaTotalFrames;
            var pref = ImageLayoutHelper.PackPreference.ByWidth;
            if (widestImage > tallestImage)
                pref = ImageLayoutHelper.PackPreference.ByHeight;

            int optimalTotalWidth = (int)Math.Ceiling(Math.Sqrt(minArea));
            if (optimalTotalWidth < widestImage)
                optimalTotalWidth = widestImage;
            optimalTotalWidth = GetNextPowerOf2(optimalTotalWidth, false);

            oResults.Add(string.Format("Tallest Image: {0:0,0} ", tallestImage));
            oResults.Add(string.Format("Widest Image: {0:0,0} ", widestImage));
            oResults.Add(string.Format("Total Image Area: {0:0,0} ", areaTotalFrames));
            oResults.Add(string.Format("Smallest Possible Area: {0:0,0} ", minArea));
            oResults.Add(string.Format("Optimal Width: {0:0,0} ", optimalTotalWidth));
            oResults.Add(string.Format("PackPreference: {0} ", pref));


            ImageFrame layoutFrame = ImageLayoutHelper.LayImageFrames(frameArray, optimalTotalWidth);
            List<ImageFrame> framesList = layoutFrame.GetFlatList();

            using (var bitmap = new Bitmap(layoutFrame.Width, layoutFrame.Height))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic; //http://msdn.microsoft.com/en-us/library/system.drawing.drawing2d.interpolationmode.aspx
                    g.SmoothingMode = SmoothingMode.None;                       //http://msdn.microsoft.com/en-us/library/z714w2y9.aspx
                    g.PixelOffsetMode = PixelOffsetMode.None;                   //http://msdn.microsoft.com/en-us/library/system.drawing.drawing2d.pixeloffsetmode.aspx
                    g.CompositingQuality = CompositingQuality.HighQuality;      //http://msdn.microsoft.com/en-us/library/system.drawing.drawing2d.compositingquality.aspx

                    if (pOptions.GenerateTestHtmlPage)
                        testPage.AppendFormat("<h1>{0} Images</h1>", framesList.Count);
                    var testPageAltClass = "a";

                    foreach (var f in framesList)
                    {
                        using (var image = Image.FromFile(f.Id))
                        {
                            g.DrawImage(image, f.OffsetX, f.OffsetY, f.Width, f.Height);
                            pOptions.CssPlaceholderValues[pOptions.CSS_PLACEHOLDER_CSS_CLASS_NAME_BASE] = GetOutputCssClassNameFromFilename(
                                    Path.GetFileName(f.Id).Substring(0, Path.GetFileName(f.Id).LastIndexOf("."))
                                    , pOptions.CssClassNameInvalidCharReplacement
                                    , pOptions.ValidSpriteClassNameCharacters);
                            pOptions.CssPlaceholderValues[pOptions.CSS_PLACEHOLDER_WIDTH] = f.Width.ToString();
                            pOptions.CssPlaceholderValues[pOptions.CSS_PLACEHOLDER_HEIGHT] = f.Height.ToString();
                            pOptions.CssPlaceholderValues[pOptions.CSS_PLACEHOLDER_IMAGE_FILE_NAME] = Path.GetFileName(pOptions.ImageOutputFilePath);
                            pOptions.CssPlaceholderValues[pOptions.CSS_PLACEHOLDER_OFFSET_X] = (-f.OffsetX).ToString();
                            pOptions.CssPlaceholderValues[pOptions.CSS_PLACEHOLDER_OFFSET_Y] = (-f.OffsetY).ToString();
                            

                            var currentStyle = new StringBuilder(pOptions.CssFormatStringForSpriteDefinition);
                            foreach (KeyValuePair<string, string> entry in pOptions.CssPlaceholderValues)
                                currentStyle.Replace(entry.Key, entry.Value);


                            cssBuffer.AppendLine(currentStyle.ToString());
                            if (pOptions.GenerateTestHtmlPage)
                            {
                                testPageAltClass = testPageAltClass == "a" ? "" : "a";
                                testPage.AppendFormat("<tr class='{4}'><td>{0}{1}</td><td>{2}</td><td>{3}</td><td><img src='{5}' class='{0}{1}' title='{0}{1}' /></td></tr>"
                                    , pOptions.CssPlaceholderValues[pOptions.CSS_PLACEHOLDER_CSS_CLASS_NAME_PREFIX] + pOptions.CssPlaceholderValues[pOptions.CSS_PLACEHOLDER_CSS_CLASS_NAME_BASE]
                                    , pOptions.CssPlaceholderValues[pOptions.CSS_PLACEHOLDER_CSS_CLASS_NAME_SUFFIX]
                                    , f.Height
                                    , f.Width
                                    , testPageAltClass
                                    , pOptions.TestHtmlClearGifUrl
                                );
                            }
                        }
                    }
                }


                string imageOutputFilePath = pOptions.ImageOutputFilePath;


                File.WriteAllText(pOptions.CssOutputFilePath, cssBuffer.ToString());
                if (pOptions.GenerateTestHtmlPage)
                {
                    var css = cssBuffer.ToString();
                    if (!string.IsNullOrWhiteSpace(pOptions.TestHtmlImageDeployUrlBase))
                        css = css.Replace(pOptions.CssPlaceholderValues[pOptions.CSS_PLACEHOLDER_IMAGE_DEPLOY_URL_BASE], pOptions.TestHtmlImageDeployUrlBase);

                    var htmlForRows = testPage.ToString();
                    var html = string.Format("<html><head><title>CSS Sprite Preview for: {0}</title>{3}<style>body {1}background-color:#eeeeff;{2} img {1}margin:1px;border:solid 1px red;{2} TH {1}background-color:#bbbbbb;{2} TD {1}background-color:#cccccc;{2} TR.a TD {1}background-color:#DDDDDD;{2}  .clickable {1}cursor:pointer;cursor:hand;{2} {4}</style></head><body><table class='sortable' cellspacing=0 cellpadding=5><tr><td class=clickable>Css Class</td><td class=clickable>Height</td><td class=clickable>Width</td><td>Preview</td></tr>",
                        Path.GetFileName(pOptions.CssOutputFilePath), "{", "}", pOptions.TestHtmlHeadIncludes, css) 
                        + htmlForRows
                        + "</table></body></html>";

                    var testFilePath = pOptions.CssOutputFilePath + pOptions.TestHtmlFilenameSuffix;
                    if (!string.IsNullOrWhiteSpace(pOptions.TestHtmlPath))
                        testFilePath = Path.Combine(pOptions.TestHtmlPath, Path.GetFileName(pOptions.CssOutputFilePath) + pOptions.TestHtmlFilenameSuffix);

                    File.WriteAllText(testFilePath, html);
                    oResults.Add("Successfully generated the CSS test html file to: " + testFilePath);
                }

                oResults.Add("Successfully generated the corresponding CSS file to: " + pOptions.CssOutputFilePath);

                //option for reducing the output bit depth using OctreeQuantizer
                if (pOptions.LimitOutputImageTo8Bits)
                {
                    var octreeQuantizer = new OctreeQuantizer(OctreeQuantizer.BitDepth.Bits8);
                    using (var quantizedBitmap = octreeQuantizer.Quantize(bitmap))
                    {
                        quantizedBitmap.Save(imageOutputFilePath, pOptions.OutputImageFormat);
                        oResults.Add(string.Format("Limited {0} to {1} bits using OctreeQuantizer", imageOutputFilePath, 8));
                    } 
                } 
                else
                {
                    bitmap.Save(imageOutputFilePath, pOptions.OutputImageFormat);
                }

                //if (pOptions.MakeTransparentUsingColorKey)
                //{
                //    Color c = bitmap.GetPixel(0, 0); or use passed in coordinate or color
                //    bitmap.MakeTransparent(c);
                //    var bitmapT = Transparency.MakeTransparent(bitmap, Color.Transparent);
                //}

                oResults.Add(string.Format("New sprite: {0} ", imageOutputFilePath));
                outputFileSize = new FileInfo(imageOutputFilePath).Length;
                oResults.Add(string.Format("Successfully converted {0} files of cumulative size {1} bytes into one file of size {2} bytes."
                    , pOptions.ImageFilePaths.Count, inputFileTotalSize, outputFileSize));

            }
            return true;
        }

        private static int GetNextPowerOf2(int pNumber, bool pJumpToNextBoundaryOnDirectHit)
        {
            var powers = new[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536, 131072, 262144, 524288, 1048576 };

            for (var i=0; i < powers.Length; i++)
            {
                var pow = powers[i];
                if (pNumber < pow)
                {
                    return pow;
                }
                if (pNumber == pow)
                {
                    if (pJumpToNextBoundaryOnDirectHit)
                        return powers[i + 1];
                    return pow;
                }
            }
            //ugh - we have a huge number that isn't in our list
            var p = powers[powers.Length - 1];
            while (true)
            {
                p = p * 2;
                if (pNumber < p)
                {
                    return p;
                }
                if (pNumber == p)
                {
                    if (pJumpToNextBoundaryOnDirectHit)
                        return p * 2;
                    return p;
                }
            }

        }


        private static string GetOutputCssClassNameFromFilename(string pFilename, string pInvalidCharReplacement, string pValidSpriteClassNameCharacters)
        {
            if (pFilename.LastIndexOf('.') > 0)
                pFilename = pFilename.Substring(0, pFilename.LastIndexOf('.')); //trim the extension

            pFilename = OnlyAllowTheseCharacters(pValidSpriteClassNameCharacters, pFilename, pInvalidCharReplacement); //remove the invalid chars that will break the css class declaration
            return pFilename;
        }

        /// <summary>
        /// Strip out all non-numeric (and decimal) chars
        /// </summary>
        private static string GetValidCssClassName(string pStringToCheck, string pValidSpriteClassNameCharacters)
        {
            return OnlyAllowTheseCharacters(pValidSpriteClassNameCharacters, pStringToCheck, string.Empty);
        }

        /// <summary>
        /// Strip out all non-numeric (and decimal) chars
        /// </summary>
        private static string OnlyAllowTheseCharacters(string pAllowableCharacters, string pStringToCheck, string pInvalidCharReplacement)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < pStringToCheck.Length; i++)
                if (pAllowableCharacters.IndexOf(pStringToCheck[i]) >= 0)
                    sb.Append(pStringToCheck[i]);
                else
                    sb.Append(pInvalidCharReplacement);

            return sb.ToString();
        }

    }

}
