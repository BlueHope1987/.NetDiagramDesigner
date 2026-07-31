using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DiagramDesigner.Core
{
    public static class GraphicsUtility
    {
        public static GraphicsPath CreateRoundedRectPath(float x, float y, float width, float height, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float r = radius;
            float maxR = width / 2f;
            if (r > maxR) r = maxR;
            maxR = height / 2f;
            if (r > maxR) r = maxR;

            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + width - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + width - r * 2, y + height - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + height - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static GraphicsPath CreateRoundedRectPath(RectangleF rect, float radius)
        {
            return CreateRoundedRectPath(rect.X, rect.Y, rect.Width, rect.Height, radius);
        }

        public static void DrawRoundedRectOutline(Graphics g, Pen pen, RectangleF rect, float radius)
        {
            using (GraphicsPath path = CreateRoundedRectPath(rect, radius))
            {
                g.DrawPath(pen, path);
            }
        }
    }
}