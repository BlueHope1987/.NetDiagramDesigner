using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DiagramDesigner.Config;
using DiagramDesigner.Core;
using DiagramDesigner.Shapes;

namespace DiagramDesigner.Controls
{
    public partial class DrawingCanvas : Control
    {
        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            DrawFixedBackground(pevent.Graphics);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;

            if (GlobalConfig.Instance.AntiAlias)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            }

            g.TranslateTransform(_offset.X, _offset.Y);
            g.ScaleTransform(_zoom, _zoom);

            DrawGrid(g);
            DrawShapes(g);
            DrawConnections(g);
            DrawRubberBand(g);
            DrawSelectionRect(g);

            g.ResetTransform();
        }

        private void DrawFixedBackground(Graphics g)
        {
            Color bgColor = GlobalConfig.Instance.CanvasBackground;
            Color centerColor = GlobalConfig.Instance.GradientCenterColor;

            int w = ClientSize.Width;
            int h = ClientSize.Height;
            if (w <= 0 || h <= 0)
                return;

            // 底层纯色填充
            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            {
                g.FillRectangle(bgBrush, 0, 0, w, h);
            }

            // 放射渐变模拟：从右下角到左上角
            // 用两个线性渐变交叉叠加，再用中心椭圆柔化
            // 渐变1：水平方向（右侧亮→左侧暗）
            using (LinearGradientBrush hBrush = new LinearGradientBrush(
                new PointF(w, 0), new PointF(0, 0),
                centerColor, bgColor))
            {
                hBrush.WrapMode = WrapMode.TileFlipXY;
                g.FillRectangle(hBrush, 0, 0, w, h);
            }

            // 渐变2：垂直方向（下方亮→上方暗），半透明叠加
            using (LinearGradientBrush vBrush = new LinearGradientBrush(
                new PointF(0, h), new PointF(0, 0),
                centerColor, bgColor))
            {
                using (SolidBrush overlay = new SolidBrush(Color.FromArgb(80,
                    centerColor.R, centerColor.G, centerColor.B)))
                {
                    // 将垂直渐变与半透明色混合
                    // 这里改用直接绘制带透明度的渐变矩形
                    Color transCenter = Color.FromArgb(100,
                        Math.Min(255, centerColor.R + 20),
                        Math.Min(255, centerColor.G + 20),
                        Math.Min(255, centerColor.B + 25));
                    Color transBg = Color.FromArgb(0, bgColor);
                    using (LinearGradientBrush vBrush2 = new LinearGradientBrush(
                        new PointF(0, h), new PointF(0, 0),
                        transCenter, transBg))
                    {
                        g.FillRectangle(vBrush2, 0, 0, w, h);
                    }
                }
            }

            // 中心高光椭圆柔化（在右下角）
            float rw = (float)w * 0.5f;
            float rh = (float)h * 0.5f;
            float cx = (float)(w - w * 0.15);
            float cy = (float)(h - h * 0.15);
            if (rw > 1f && rh > 1f)
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(cx - rw / 2f, cy - rh / 2f, rw, rh);
                    using (PathGradientBrush pgb = new PathGradientBrush(path))
                    {
                        Color highlightColor = Color.FromArgb(
                            Math.Min(255, centerColor.R + 25),
                            Math.Min(255, centerColor.G + 25),
                            Math.Min(255, centerColor.B + 35));
                        pgb.CenterColor = highlightColor;
                        pgb.SurroundColors = new Color[] { Color.Transparent };
                        pgb.SetSigmaBellShape(0.6f, 1.0f);
                        g.FillPath(pgb, path);
                    }
                }
            }
        }

        private void DrawGrid(Graphics g)
        {
            if (!GlobalConfig.Instance.ShowGrid)
                return;

            float gridSize = GlobalConfig.Instance.GridSize;
            float left = (-_offset.X) / _zoom;
            float top = (-_offset.Y) / _zoom;
            float right = left + ClientSize.Width / _zoom;
            float bottom = top + ClientSize.Height / _zoom;

            float startX = (float)Math.Floor(left / gridSize) * gridSize;
            float startY = (float)Math.Floor(top / gridSize) * gridSize;

            Color gridColor = GlobalConfig.Instance.GridColor;
            using (Pen pen = new Pen(gridColor))
            {
                pen.Width = 0.5f / _zoom;

                for (float x = startX; x <= right; x += gridSize)
                {
                    g.DrawLine(pen, x, top, x, bottom);
                }
                for (float y = startY; y <= bottom; y += gridSize)
                {
                    g.DrawLine(pen, left, y, right, y);
                }
            }
        }

        private void DrawShapes(Graphics g)
        {
            List<ShapeBase> shapes = _document.Shapes;
            for (int i = 0; i < shapes.Count; i++)
            {
                ShapeBase shape = shapes[i];
                if (shape.Visible && shape.Parent == null)
                {
                    shape.Draw(g, _zoom);

                    if (shape is ContainerShape)
                    {
                        ContainerShape cs = (ContainerShape)shape;
                        RectangleF bodyRect = cs.Bounds;
                        bodyRect.Y += cs.HeaderHeight;
                        bodyRect.Height -= cs.HeaderHeight;
                        DrawConnectionsForContainer(g, cs, bodyRect);
                    }
                }
            }
        }

        private void DrawConnections(Graphics g)
        {
            List<Connection> connections = _document.Connections;
            for (int i = 0; i < connections.Count; i++)
            {
                Connection conn = connections[i];
                bool bothInSameContainer = false;
                if (conn.FromShape != null && conn.ToShape != null &&
                    conn.FromShape.Parent != null && conn.FromShape.Parent == conn.ToShape.Parent)
                {
                    bothInSameContainer = true;
                }
                if (!bothInSameContainer)
                {
                    conn.Draw(g, _zoom);
                }
            }
        }

        private void DrawConnectionsForContainer(Graphics g, ShapeBase parentShape, RectangleF clipRect)
        {
            List<Connection> connections = _document.Connections;
            for (int i = 0; i < connections.Count; i++)
            {
                Connection conn = connections[i];
                if (conn.FromShape != null && conn.ToShape != null &&
                    conn.FromShape.Parent == parentShape && conn.ToShape.Parent == parentShape)
                {
                    GraphicsState state = g.Save();
                    g.Clip = new Region(clipRect);
                    conn.Draw(g, _zoom);
                    g.Restore(state);
                }
            }
        }

        private void DrawRubberBand(Graphics g)
        {
            if (!_isConnecting)
                return;

            Color rbColor = GlobalConfig.Instance.RubberBandColor;
            using (Pen pen = new Pen(rbColor, 1.5f / _zoom))
            {
                pen.DashStyle = DashStyle.Dash;
                g.DrawLine(pen, _connectStartPoint, _connectCurrentPoint);
            }
        }

        private void DrawSelectionRect(Graphics g)
        {
            if (!_isSelecting)
                return;

            Color selColor = GlobalConfig.Instance.RubberBandColor;
            using (Brush brush = new SolidBrush(Color.FromArgb(30,
                selColor.R, selColor.G, selColor.B)))
            using (Pen pen = new Pen(selColor, 1f / _zoom))
            {
                pen.DashStyle = DashStyle.Dash;
                g.FillRectangle(brush, _selectionRect);
                g.DrawRectangle(pen, _selectionRect.X, _selectionRect.Y,
                    _selectionRect.Width, _selectionRect.Height);
            }
        }
    }
}
