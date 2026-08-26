using System;
using System.Drawing;
using System.IO;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 图片图层模型。用于在自定义图形中嵌入 PNG 图片作为独立图层。
    /// 图片数据以 base64 字符串存储，随元件缩放。
    /// </summary>
    [Serializable]
    public class ImageLayer
    {
        private string _name = "Image";
        private float _x = 0f;        // 归一化左上角 X (0~1)
        private float _y = 0f;        // 归一化左上角 Y (0~1)
        private float _width = 0.5f;  // 归一化宽度 (0~1)
        private float _height = 0.5f; // 归一化高度 (0~1)
        private string _pngDataBase64 = ""; // PNG 图片的 base64 编码
        private bool _visible = true;
        private float _aspectRatio = 1f; // 原始宽高比（width/height），用于保持比例

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public float X
        {
            get { return _x; }
            set { _x = value; }
        }

        public float Y
        {
            get { return _y; }
            set { _y = value; }
        }

        public float Width
        {
            get { return _width; }
            set { _width = value; }
        }

        public float Height
        {
            get { return _height; }
            set { _height = value; }
        }

        /// <summary>PNG 图片的 base64 编码数据</summary>
        public string PngDataBase64
        {
            get { return _pngDataBase64; }
            set { _pngDataBase64 = value; }
        }

        public bool Visible
        {
            get { return _visible; }
            set { _visible = value; }
        }

        /// <summary>原始图片的宽高比（width/height）</summary>
        public float AspectRatio
        {
            get { return _aspectRatio; }
            set { _aspectRatio = value; }
        }

        public ImageLayer() { }

        /// <summary>从 PNG 字节数据创建 ImageLayer</summary>
        public static ImageLayer FromPngBytes(byte[] pngBytes, string name)
        {
            ImageLayer layer = new ImageLayer();
            layer.Name = name;
            layer._pngDataBase64 = Convert.ToBase64String(pngBytes);

            // 读取图片尺寸计算宽高比
            try
            {
                using (MemoryStream ms = new MemoryStream(pngBytes))
                using (Bitmap bmp = new Bitmap(ms))
                {
                    if (bmp.Height > 0)
                        layer._aspectRatio = (float)bmp.Width / (float)bmp.Height;
                }
            }
            catch { }

            return layer;
        }

        /// <summary>解码 PNG 数据为 Image 对象</summary>
        public Image ToImage()
        {
            if (string.IsNullOrEmpty(_pngDataBase64))
                return null;
            try
            {
                byte[] bytes = Convert.FromBase64String(_pngDataBase64);
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    return Image.FromStream(ms);
                }
            }
            catch { return null; }
        }

        /// <summary>获取图片的绝对矩形（在元件边界内）</summary>
        public RectangleF GetBounds(RectangleF shapeBounds)
        {
            return new RectangleF(
                shapeBounds.X + _x * shapeBounds.Width,
                shapeBounds.Y + _y * shapeBounds.Height,
                _width * shapeBounds.Width,
                _height * shapeBounds.Height);
        }

        /// <summary>深拷贝</summary>
        public ImageLayer Clone()
        {
            ImageLayer clone = new ImageLayer();
            clone._name = _name;
            clone._x = _x;
            clone._y = _y;
            clone._width = _width;
            clone._height = _height;
            clone._pngDataBase64 = _pngDataBase64;
            clone._visible = _visible;
            clone._aspectRatio = _aspectRatio;
            return clone;
        }
    }
}
