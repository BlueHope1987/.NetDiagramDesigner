using System;
using System.Collections.Generic;
using System.Drawing;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 顶点句柄类型。决定顶点处曲线的控制方式。
    /// </summary>
    public enum HandleType
    {
        /// <summary>无控制柄：直线连接，无曲线处理</summary>
        None,
        /// <summary>一个控制柄：顶点两边对称处理（镜像）</summary>
        Symmetric,
        /// <summary>两个控制柄：进出边各自独立控制</summary>
        Asymmetric
    }

    /// <summary>
    /// 带贝塞尔曲线句柄的顶点。
    /// HandleIn/HandleOut 为相对于顶点位置的偏移量（像素坐标）。
    /// Symmetric 模式下 HandleIn 自动镜像 HandleOut。
    /// </summary>
    [Serializable]
    public class CurveVertex
    {
        private PointF _position;
        private HandleType _handle = HandleType.None;
        private PointF _handleIn = PointF.Empty;   // 进边控制点偏移
        private PointF _handleOut = PointF.Empty;  // 出边控制点偏移

        public PointF Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public HandleType Handle
        {
            get { return _handle; }
            set { _handle = value; }
        }

        /// <summary>进边控制点偏移（相对于顶点位置）</summary>
        public PointF HandleIn
        {
            get { return _handleIn; }
            set
            {
                _handleIn = value;
                if (_handle == HandleType.Symmetric)
                    _handleOut = new PointF(-value.X, -value.Y);
            }
        }

        /// <summary>出边控制点偏移（相对于顶点位置）</summary>
        public PointF HandleOut
        {
            get { return _handleOut; }
            set
            {
                _handleOut = value;
                if (_handle == HandleType.Symmetric)
                    _handleIn = new PointF(-value.X, -value.Y);
            }
        }

        public CurveVertex() { }

        public CurveVertex(PointF position)
        {
            _position = position;
        }

        public CurveVertex(PointF position, HandleType handle, PointF handleIn, PointF handleOut)
        {
            _position = position;
            _handle = handle;
            _handleIn = handleIn;
            _handleOut = handleOut;
        }

        /// <summary>从 PointF 隐式转换（默认无句柄）</summary>
        public static implicit operator CurveVertex(PointF p)
        {
            return new CurveVertex(p);
        }

        /// <summary>获取进边控制点的绝对位置</summary>
        public PointF GetHandleInAbs()
        {
            return new PointF(_position.X + _handleIn.X, _position.Y + _handleIn.Y);
        }

        /// <summary>获取出边控制点的绝对位置</summary>
        public PointF GetHandleOutAbs()
        {
            return new PointF(_position.X + _handleOut.X, _position.Y + _handleOut.Y);
        }

        /// <summary>设置出边控制点（绝对位置），自动处理 Symmetric 镜像</summary>
        public void SetHandleOutAbs(PointF absPos)
        {
            _handleOut = new PointF(absPos.X - _position.X, absPos.Y - _position.Y);
            if (_handle == HandleType.Symmetric)
                _handleIn = new PointF(-_handleOut.X, -_handleOut.Y);
        }

        /// <summary>设置进边控制点（绝对位置），自动处理 Symmetric 镜像</summary>
        public void SetHandleInAbs(PointF absPos)
        {
            _handleIn = new PointF(absPos.X - _position.X, absPos.Y - _position.Y);
            if (_handle == HandleType.Symmetric)
                _handleOut = new PointF(-_handleIn.X, -_handleIn.Y);
        }

        /// <summary>
        /// 为顶点生成默认句柄方向（垂直于邻边角平分线）。
        /// 句柄长度为邻边平均长度的 1/3。
        /// </summary>
        public void InitDefaultHandles(PointF prevPos, PointF nextPos)
        {
            // 计算进边和出边方向
            float dx1 = _position.X - prevPos.X;
            float dy1 = _position.Y - prevPos.Y;
            float dx2 = nextPos.X - _position.X;
            float dy2 = nextPos.Y - _position.Y;

            float len1 = (float)Math.Sqrt(dx1 * dx1 + dy1 * dy1);
            float len2 = (float)Math.Sqrt(dx2 * dx2 + dy2 * dy2);
            float avgLen = (len1 + len2) * 0.5f;
            if (avgLen < 1f) avgLen = 20f;

            // 归一化方向
            if (len1 < 0.001f) { dx1 = -dx2; dy1 = -dy2; len1 = len2; }
            if (len2 < 0.001f) { dx2 = -dx1; dy2 = -dy1; len2 = len1; }
            if (len1 > 0.001f) { dx1 /= len1; dy1 /= len1; }
            if (len2 > 0.001f) { dx2 /= len2; dy2 /= len2; }

            // 角平分线方向（进出方向的平均）
            float bx = (dx1 + dx2) * 0.5f;
            float by = (dy1 + dy2) * 0.5f;
            float bLen = (float)Math.Sqrt(bx * bx + by * by);

            if (bLen < 0.001f)
            {
                // 退化为直线，使用垂直方向
                bx = -dy2;
                by = dx2;
                bLen = (float)Math.Sqrt(bx * bx + by * by);
                if (bLen < 0.001f) { bx = 1f; by = 0f; bLen = 1f; }
            }
            bx /= bLen;
            by /= bLen;

            // HandleOut 沿平分线方向，HandleIn 反向
            float handleLen = avgLen / 3f;
            _handleOut = new PointF(bx * handleLen, by * handleLen);
            _handleIn = new PointF(-bx * handleLen, -by * handleLen);
        }

        public CurveVertex Clone()
        {
            return new CurveVertex(_position, _handle, _handleIn, _handleOut);
        }

        public PointF ToPointF()
        {
            return _position;
        }

        /// <summary>从 PointF 列表创建 CurveVertex 列表（全部无句柄）</summary>
        public static List<CurveVertex> FromPointList(List<PointF> points)
        {
            List<CurveVertex> result = new List<CurveVertex>();
            if (points == null) return result;
            foreach (PointF p in points)
                result.Add(new CurveVertex(p));
            return result;
        }

        /// <summary>从 CurveVertex 列表提取 PointF 列表</summary>
        public static List<PointF> ToPointList(List<CurveVertex> vertices)
        {
            List<PointF> result = new List<PointF>();
            if (vertices == null) return result;
            foreach (CurveVertex v in vertices)
                result.Add(v.Position);
            return result;
        }

        /// <summary>从 PointF 数组创建 CurveVertex 数组（全部无句柄）</summary>
        public static CurveVertex[] FromPointArray(PointF[] points)
        {
            if (points == null) return null;
            CurveVertex[] result = new CurveVertex[points.Length];
            for (int i = 0; i < points.Length; i++)
                result[i] = new CurveVertex(points[i]);
            return result;
        }
    }
}
