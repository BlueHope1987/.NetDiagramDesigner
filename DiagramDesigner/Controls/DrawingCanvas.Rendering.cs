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
        // === 背景渲染缓存 ===
        // 将多层渐变背景预渲染到 Bitmap，仅在尺寸/主题色变化时重生成，
        // 避免每次 OnPaint 都创建多个渐变 Brush/Path 造成性能开销。
        private Bitmap _backgroundCache = null;
        private int _cachedBgWidth = 0;
        private int _cachedBgHeight = 0;
        private Color _cachedBgColor = Color.Empty;
        private Color _cachedCenterColor = Color.Empty;

        // === 连线渲染索引 ===
        // 每帧构建一次：将连线按"所属容器"分组，避免每个容器都遍历全部连线（O(C×N)）。
        private Dictionary<ShapeBase, List<Connection>> _containerConnectionIndex =
            new Dictionary<ShapeBase, List<Connection>>();
        private List<Connection> _nonContainerConnections = new List<Connection>();

        /// <summary>
        /// 重建连线索引。每帧 OnPaint 开始时调用一次，O(N) 遍历全部连线，
        /// 按 FromShape.Parent 分组到容器索引或非容器列表。
        /// </summary>
        private void RebuildConnectionIndex()
        {
            _containerConnectionIndex.Clear();
            _nonContainerConnections.Clear();

            List<Connection> connections = _document.Connections;
            for (int i = 0; i < connections.Count; i++)
            {
                Connection conn = connections[i];
                ShapeBase from = conn.FromShape;
                ShapeBase to = conn.ToShape;

                if (from != null && to != null &&
                    from.Parent != null && from.Parent == to.Parent)
                {
                    // 两端在同一容器内：归入该容器的连线列表
                    List<Connection> bucket;
                    if (!_containerConnectionIndex.TryGetValue(from.Parent, out bucket))
                    {
                        bucket = new List<Connection>();
                        _containerConnectionIndex[from.Parent] = bucket;
                    }
                    bucket.Add(conn);
                }
                else
                {
                    // 跨容器或一端为空：归入非容器连线
                    _nonContainerConnections.Add(conn);
                }
            }
        }

        /// <summary>使背景缓存失效，下次绘制时重新生成。</summary>
        private void InvalidateBackgroundCache()
        {
            if (_backgroundCache != null)
            {
                _backgroundCache.Dispose();
                _backgroundCache = null;
            }
            _cachedBgWidth = 0;
            _cachedBgHeight = 0;
            _cachedBgColor = Color.Empty;
            _cachedCenterColor = Color.Empty;
        }

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

            // 每帧构建一次连线索引，供 DrawConnections / DrawConnectionsForContainer 复用
            RebuildConnectionIndex();

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

            // 缓存命中：尺寸与颜色均未变化时直接绘制缓存 Bitmap
            if (_backgroundCache != null &&
                _cachedBgWidth == w && _cachedBgHeight == h &&
                _cachedBgColor == bgColor && _cachedCenterColor == centerColor)
            {
                g.DrawImageUnscaled(_backgroundCache, 0, 0);
                return;
            }

            // 缓存失效：释放旧缓存，渲染到新 Bitmap
            if (_backgroundCache != null)
            {
                _backgroundCache.Dispose();
                _backgroundCache = null;
            }

            _backgroundCache = new Bitmap(w, h);
            using (Graphics bg = Graphics.FromImage(_backgroundCache))
            {
                RenderBackgroundToGraphics(bg, w, h, bgColor, centerColor);
            }

            _cachedBgWidth = w;
            _cachedBgHeight = h;
            _cachedBgColor = bgColor;
            _cachedCenterColor = centerColor;

            g.DrawImageUnscaled(_backgroundCache, 0, 0);
        }

        /// <summary>将多层渐变背景渲染到指定 Graphics（原 DrawFixedBackground 的绘制逻辑）。</summary>
        private static void RenderBackgroundToGraphics(Graphics g, int w, int h, Color bgColor, Color centerColor)
        {
            // 底层纯色填充
            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            {
                g.FillRectangle(bgBrush, 0, 0, w, h);
            }

            // 放射渐变模拟：从右下角到左上角
            // 渐变1：水平方向（右侧亮→左侧暗）
            using (LinearGradientBrush hBrush = new LinearGradientBrush(
                new PointF(w, 0), new PointF(0, 0),
                centerColor, bgColor))
            {
                hBrush.WrapMode = WrapMode.TileFlipXY;
                g.FillRectangle(hBrush, 0, 0, w, h);
            }

            // 渐变2：垂直方向（下方亮→上方暗），半透明叠加
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
            // 设置主画布模式：设计模式下显示 DesignTime/RunTime 区域，运行模式下仅显示 RunTime
            GenericShape.RenderZoneVisuals = _config.DesignMode;

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
            // 仅绘制非容器内连线（跨容器或一端为空的连线）
            List<Connection> connections = _nonContainerConnections;
            for (int i = 0; i < connections.Count; i++)
            {
                connections[i].Draw(g, _zoom);
            }
        }

        private void DrawConnectionsForContainer(Graphics g, ShapeBase parentShape, RectangleF clipRect)
        {
            // 直接从预构建的容器连线索引取该容器的连线列表，O(k) 而非 O(N)
            List<Connection> bucket;
            if (!_containerConnectionIndex.TryGetValue(parentShape, out bucket))
                return;

            GraphicsState state = g.Save();
            g.Clip = new Region(clipRect);
            for (int i = 0; i < bucket.Count; i++)
            {
                bucket[i].Draw(g, _zoom);
            }
            g.Restore(state);
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
