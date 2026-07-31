using System;
using System.Drawing;

namespace DiagramDesigner.Core
{
    [Serializable]
    public class XmlColor
    {
        private int _argb = Color.White.ToArgb();

        public int Argb
        {
            get { return _argb; }
            set { _argb = value; }
        }

        public XmlColor() { }

        public XmlColor(Color color)
        {
            _argb = color.ToArgb();
        }

        public Color ToColor()
        {
            return Color.FromArgb(_argb);
        }

        public static implicit operator Color(XmlColor xc)
        {
            if (xc == null)
                return Color.White;
            return xc.ToColor();
        }

        public static implicit operator XmlColor(Color c)
        {
            return new XmlColor(c);
        }
    }
}
