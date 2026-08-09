using System;
using System.Collections.Generic;
using System.Drawing;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 路径定义。每条路径拥有独立的布尔运算类型，
    /// 该运算仅与紧邻的下层路径（背景相邻图形）计算。
    /// </summary>
    [Serializable]
    public class PathDef
    {
        private PointF[] _points = null;
        private BooleanOperation _boolOp = BooleanOperation.None;
        private bool _visible = true;

        /// <summary>路径顶点数组（归一化坐标 0~1）</summary>
        public PointF[] Points
        {
            get { return _points; }
            set { _points = value; }
        }

        /// <summary>
        /// 该路径的布尔运算类型。仅与紧邻的下层路径计算。
        /// 底层路径（索引 0）的 BoolOp 始终视为 None。
        /// </summary>
        public BooleanOperation BoolOp
        {
            get { return _boolOp; }
            set { _boolOp = value; }
        }

        /// <summary>路径是否可见</summary>
        public bool Visible
        {
            get { return _visible; }
            set { _visible = value; }
        }

        public PathDef() { }

        public PathDef(PointF[] points, BooleanOperation boolOp)
        {
            _points = points;
            _boolOp = boolOp;
        }

        /// <summary>从顶点列表创建</summary>
        public PathDef(List<PointF> pointList, BooleanOperation boolOp)
        {
            if (pointList != null && pointList.Count > 0)
                _points = pointList.ToArray();
            _boolOp = boolOp;
        }

        /// <summary>深拷贝</summary>
        public PathDef Clone()
        {
            PathDef clone = new PathDef();
            clone._boolOp = _boolOp;
            clone._visible = _visible;
            if (_points != null)
            {
                clone._points = new PointF[_points.Length];
                for (int i = 0; i < _points.Length; i++)
                    clone._points[i] = _points[i];
            }
            return clone;
        }

        /// <summary>获取顶点数量</summary>
        public int VertexCount
        {
            get { return (_points != null) ? _points.Length : 0; }
        }

        /// <summary>转换为 PointF[] 顶点列表（便于编辑器使用）</summary>
        public List<PointF> ToPointList()
        {
            List<PointF> result = new List<PointF>();
            if (_points != null)
            {
                foreach (PointF p in _points)
                    result.Add(p);
            }
            return result;
        }

        /// <summary>从 PointF 列表设置顶点</summary>
        public void FromPointList(List<PointF> points)
        {
            if (points == null || points.Count == 0)
                _points = null;
            else
                _points = points.ToArray();
        }
    }
}
