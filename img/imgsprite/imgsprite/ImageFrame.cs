using System;
using System.Collections.Generic;

namespace imgsprite
{
    public class ImageFrame
    {
        private int _width;
        public int Width
        {
            get { return _width; }
            private set { _width = value; }
        }

        private int _height;
        public int Height
        {
            get { return _height; }
            private set { _height = value; }
        }

        private string _id;
        public string Id
        {
            get { return _id; }
            private set { _id = value; }
        }

        /// <summary>
        /// Trim the path info, if any. Useful for debugging
        /// </summary>
        public string IdShort
        {
            get
            {
                if (_id != null && _id.Contains("\\"))
                    return _id.Substring(_id.LastIndexOf("\\") + 1);
                return _id;
            }
        }

        private int _offsetX;
        public int OffsetX
        {
            get { return _offsetX; }
            private set { _offsetX = value; }
        }

        private int _offsetY;
        public int OffsetY
        {
            get { return _offsetY; }
            private set { _offsetY = value; }
        }

        public int SemiPerimeter
        {
            get { return Width + Height; }
        }

        public int Area
        {
            get { return Width * Height; }
        }

        public ImageFrame(string id, int width, int height)
        {
            Width = width;
            Height = height;
            Id = id;
        }

        private ImageFrame(string id, int width, int height, int offsetX, int offsetY)
            : this(id, width, height)
        {
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        ImageFrame[] _subFrames;
        private ImageFrame[] SubFrames
        {
            get { return _subFrames; }
            set { _subFrames = value; }
        }

        public ImageFrame(ImageFrame f1, ImageFrame f2, CombineMode mode)
        {
            switch (mode)
            {
                case CombineMode.SideBySide:
                    f1.OffsetX = 0;
                    f2.OffsetX = f1.Width;
                    Width = f1.Width + f2.Width;
                    Height = Math.Max(f1.Height, f2.Height);
                    break;
                case CombineMode.OneBelowOther:
                    f1.OffsetY = 0;
                    f2.OffsetY = f1.Height;
                    Width = Math.Max(f1.Width, f2.Width);
                    Height = f1.Height + f2.Height;
                    break;
                default:
                    throw new ArgumentException("invalid combine mode");
            }
            //Console.WriteLine(string.Format("New Combo: {0}x{1}, from f1 {2}x{3}, f2 {4}x{5}", Width, Height, f1.Width, f1.Height, f2.Width, f2.Height));
            SubFrames = new ImageFrame[2];
            SubFrames[0] = f1;
            SubFrames[1] = f2;
        }

        private void GetFlatList(ref List<ImageFrame> list, int parentOffsetX, int parentOffsetY)
        {
            if (String.IsNullOrEmpty(Id))
            {
                SubFrames[0].GetFlatList(ref list, OffsetX + parentOffsetX, OffsetY + parentOffsetY);
                SubFrames[1].GetFlatList(ref list, OffsetX + parentOffsetX, OffsetY + parentOffsetY);
            }
            else
            {
                list.Add(new ImageFrame(Id, Width, Height, parentOffsetX + OffsetX, parentOffsetY + OffsetY));
            }
        }

        public List<ImageFrame> GetFlatList()
        {
            var list = new List<ImageFrame>();
            GetFlatList(ref list, 0, 0);
            return list;
        }

        public override string ToString()
        {
            return string.Format("{0} = x:{1} y:{2} w:{3} h:{4}", Id, OffsetX, OffsetY, Width, Height);
        }

        public enum CombineMode
        {
            SideBySide,
            OneBelowOther
        }
    }
}
