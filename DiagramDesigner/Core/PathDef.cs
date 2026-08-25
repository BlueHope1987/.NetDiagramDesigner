using System;
using System.Collections.Generic;
using System.Drawing;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 路径定义。每条路径拥有独立的布尔运算类型，
    /// 该运算仅与紧邻的下层路径（背景相邻图形）计算。
    /// 支持贝塞尔曲线句柄数据。
    /// </summary>
    [Serializable]
    public class PathDef
    {
        private PointF[] _points = null;
        private BooleanOperation _boolOp = BooleanOperation.None;
        private bool _visible = true;
        private bool _isShape = true;

        // 曲线句柄数据（与 _points 等长，null 表示无句柄）
        private HandleType[] _handleTypes = null;
        private PointF[] _handleIns = null;   // 进边控制点偏移（归一化坐标）
        private PointF[] _handleOuts = null;  // 出边控制点偏移（归一化坐标）

        // 复合路径子图标记（与 _points 等长，null 表示无子图分割）
        private bool[] _isNewFigure = null;

        /// <summary>路径顶点数组（归一化坐标 0~1）</summary>
        public PointF[] Points
        {
            get { return _points; }
            set { _points = value; }
        }

        /// <summary>该路径的布尔运算类型</summary>
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

        /// <summary>是否为形状（填充+闭合），false=线体（仅描边）</summary>
        public bool IsShape
        {
            get { return _isShape; }
            set { _isShape = value; }
        }

        /// <summary>顶点句柄类型数组（null 表示全部无句柄）</summary>
        public HandleType[] HandleTypes
        {
            get { return _handleTypes; }
            set { _handleTypes = value; }
        }

        /// <summary>进边控制点偏移数组（归一化坐标）</summary>
        public PointF[] HandleIns
        {
            get { return _handleIns; }
            set { _handleIns = value; }
        }

        /// <summary>出边控制点偏移数组（归一化坐标）</summary>
        public PointF[] HandleOuts
        {
            get { return _handleOuts; }
            set { _handleOuts = value; }
        }

        /// <summary>复合路径子图标记数组（null 表示无子图分割）。
        /// true 表示该顶点是新子图（Figure）的起始点，用于孔洞等复合形状。</summary>
        public bool[] IsNewFigures
        {
            get { return _isNewFigure; }
            set { _isNewFigure = value; }
        }

        /// <summary>是否为复合路径（含子图分割标记）</summary>
        public bool IsCompound
        {
            get
            {
                if (_isNewFigure == null) return false;
                foreach (bool b in _isNewFigure) if (b) return true;
                return false;
            }
        }

        /// <summary>是否有曲线句柄数据</summary>
        public bool HasCurves
        {
            get
            {
                if (_handleTypes == null) return false;
                foreach (HandleType ht in _handleTypes)
                    if (ht != HandleType.None) return true;
                return false;
            }
        }

        public PathDef() { }

        public PathDef(PointF[] points, BooleanOperation boolOp)
        {
            _points = points;
            _boolOp = boolOp;
        }

        public PathDef(List<PointF> pointList, BooleanOperation boolOp)
        {
            if (pointList != null && pointList.Count > 0)
                _points = pointList.ToArray();
            _boolOp = boolOp;
        }

        /// <summary>从 CurveVertex 列表创建（包含句柄数据）</summary>
        public PathDef(List<CurveVertex> vertices, BooleanOperation boolOp)
        {
            if (vertices != null && vertices.Count > 0)
            {
                int n = vertices.Count;
                _points = new PointF[n];
                _handleTypes = new HandleType[n];
                _handleIns = new PointF[n];
                _handleOuts = new PointF[n];
                bool hasHandles = false;
                bool hasFigures = false;
                _isNewFigure = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    _points[i] = vertices[i].Position;
                    _handleTypes[i] = vertices[i].Handle;
                    _handleIns[i] = vertices[i].HandleIn;
                    _handleOuts[i] = vertices[i].HandleOut;
                    _isNewFigure[i] = vertices[i].IsNewFigure;
                    if (vertices[i].Handle != HandleType.None)
                        hasHandles = true;
                    if (vertices[i].IsNewFigure)
                        hasFigures = true;
                }
                if (!hasHandles)
                {
                    _handleTypes = null;
                    _handleIns = null;
                    _handleOuts = null;
                }
                if (!hasFigures)
                    _isNewFigure = null;
            }
            _boolOp = boolOp;
        }

        /// <summary>深拷贝</summary>
        public PathDef Clone()
        {
            PathDef clone = new PathDef();
            clone._boolOp = _boolOp;
            clone._visible = _visible;
            clone._isShape = _isShape;
            if (_points != null)
            {
                clone._points = new PointF[_points.Length];
                for (int i = 0; i < _points.Length; i++)
                    clone._points[i] = _points[i];
            }
            if (_handleTypes != null)
            {
                clone._handleTypes = (HandleType[])_handleTypes.Clone();
                clone._handleIns = (PointF[])_handleIns.Clone();
                clone._handleOuts = (PointF[])_handleOuts.Clone();
            }
            if (_isNewFigure != null)
                clone._isNewFigure = (bool[])_isNewFigure.Clone();
            return clone;
        }

        /// <summary>获取顶点数量</summary>
        public int VertexCount
        {
            get { return (_points != null) ? _points.Length : 0; }
        }

        /// <summary>转换为 PointF[] 顶点列表</summary>
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
            {
                _points = null;
                _handleTypes = null;
                _handleIns = null;
                _handleOuts = null;
            }
            else
            {
                _points = points.ToArray();
                _handleTypes = null;
                _handleIns = null;
                _handleOuts = null;
            }
        }

        /// <summary>转换为 CurveVertex 列表（包含句柄数据）</summary>
        public List<CurveVertex> ToCurveVertexList()
        {
            List<CurveVertex> result = new List<CurveVertex>();
            if (_points == null) return result;
            for (int i = 0; i < _points.Length; i++)
            {
                HandleType ht = (_handleTypes != null && i < _handleTypes.Length) ? _handleTypes[i] : HandleType.None;
                PointF hi = (_handleIns != null && i < _handleIns.Length) ? _handleIns[i] : PointF.Empty;
                PointF ho = (_handleOuts != null && i < _handleOuts.Length) ? _handleOuts[i] : PointF.Empty;
                bool newFig = (_isNewFigure != null && i < _isNewFigure.Length) ? _isNewFigure[i] : false;
                result.Add(new CurveVertex(_points[i], ht, hi, ho, newFig));
            }
            return result;
        }
    }
}
