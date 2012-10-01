using System;
using System.Collections.Generic;

namespace imgsprite
{
    public static class ImageLayoutHelper
    {
        public enum PackPreference
        {
            ByHeight,
            ByWidth
        }
        
        private static void Log(bool pDebug, string pFormat, params object[] pArgs)
        {
            if (pDebug)
                Console.WriteLine(string.Format(pFormat, pArgs));
        }


        public static ImageFrame LayImageFrames(List<ImageFrame> pFrameList, int pMaxWidth, bool pDebug = false)
        {
            if (pFrameList == null)
                return null;
            if (pFrameList.Count == 1)
                return pFrameList[0];

            pFrameList.Sort(CompareImageFrameHeightDescending);
            pFrameList.Sort(CompareImageFrameWidthDescending); //Now we should have them sorted by Height and Width
            var rowList = new List<ImageFrame>(); //we'll stack these together at the end

            var rowFrame = pFrameList[0];
            pFrameList.RemoveAt(0);
            int currentWidth = rowFrame.Width;

            Log(pDebug, "Row Loops: {0}", pFrameList.Count - 1);

            foreach (var f in pFrameList)
            {
                Log(pDebug, "Row Loop {0}x{1}: {2}", f.Width, f.Height, f.IdShort);
                int proposedWidth = currentWidth + f.Width;
                if (proposedWidth > pMaxWidth)
                {
                    //we don't want to add this to the current row we're building
                    //close our our current row, and start a new row
                    rowList.Add(rowFrame);
                    rowFrame = f;
                    Log(pDebug, "Sealing row at {0}px width", currentWidth);
                    Log(pDebug, "Next row starting with {0}x{1}: {2}", rowFrame.Width, rowFrame.Height, rowFrame.IdShort);
                    currentWidth = rowFrame.Width;
                }
                else
                {
                    currentWidth += f.Width;
                    rowFrame = new ImageFrame(rowFrame, f, ImageFrame.CombineMode.SideBySide);
                    Log(pDebug, "Adding to row {0}x{1}: {2}", f.Width, f.Height, f.IdShort);
                }
            }
            rowList.Add(rowFrame);

            //now stack all of the rowList frames
            ImageFrame stackedFrame = rowList[0];
            for (int i = 1; i < rowList.Count; i++)
            {
                Log(pDebug, "Stacking row {0}x{1}: {2}", rowList[i].Width, rowList[i].Height, rowList[i].IdShort);
                stackedFrame = new ImageFrame(stackedFrame, rowList[i], ImageFrame.CombineMode.OneBelowOther);
            }

            return stackedFrame;
        }

        public static int CompareImageFrameWidth(ImageFrame f1, ImageFrame f2)
        {
            if (f1.Width < f2.Width)
                return -1;
            if (f1.Width == f2.Width)
                return 0;
            return 1;
        }

        public static int CompareImageFrameHeight(ImageFrame f1, ImageFrame f2)
        {
            if (f1.Height < f2.Height)
                return -1;
            if (f1.Height == f2.Height)
                return 0;
            return 1;
        }
        public static int CompareImageFrameWidthDescending(ImageFrame f1, ImageFrame f2)
        {
            if (f1.Width < f2.Width)
                return 1;
            if (f1.Width == f2.Width)
                return 0;
            return -1;
        }

        public static int CompareImageFrameHeightDescending(ImageFrame f1, ImageFrame f2)
        {
            if (f1.Height < f2.Height)
                return 1;
            if (f1.Height == f2.Height)
                return 0;
            return -1;
        }

        public static int CompareImageFrameByPerimeter(ImageFrame f1, ImageFrame f2)
        {
            //Original...doesn't work propertly when there are really tall/wide items
            //along with square items with 1 larger dimension. It results in images
            //being packed in over other images. Do a 10 item test with the following sizes:
            //      (7) 502w by 36h
            //      (4) 31w  by 31h 
            //      (4) 62w  by 62h 
            if (f1.SemiPerimeter < f2.SemiPerimeter)
                return -1;
            if (f1.SemiPerimeter == f2.SemiPerimeter)
                return f1.Area - f2.Area;
            return 1;
        }
    }
}
