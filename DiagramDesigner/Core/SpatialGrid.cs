using System;
using System.Collections.Generic;
using System.Drawing;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 均匀网格空间索引，用于加速 Shape 命中测试。
    /// 将画布划分为 CellSize × CellSize 的单元格，每个 Shape 按 Bounds
    /// 落入若干单元格。查询时只检查与查询区域相交的单元格中的 Shape，
    /// 从而将平均复杂度从 O(n) 降到 O(k)（k 为命中单元格内的 Shape 数）。
    /// 
    /// 使用方式：
    ///   - 文档结构变化（增删 Shape）时调用 Invalidate()
    ///   - Shape 位置变化（拖动/缩放结束）时调用 Rebuild()
    ///   - 查询前若 IsDirty 则自动 Rebuild
    /// </summary>
    public class SpatialGrid
    {
        private readonly float _cellSize;
        private Dictionary<long, List<ShapeBase>> _cells = new Dictionary<long, List<ShapeBase>>();
        private bool _dirty = true;

        public float CellSize { get { return _cellSize; } }
        public bool IsDirty { get { return _dirty; } }

        public SpatialGrid(float cellSize)
        {
            _cellSize = cellSize <= 0 ? 200f : cellSize;
        }

        public SpatialGrid() : this(200f) { }

        /// <summary>标记索引失效，下次查询时自动重建。</summary>
        public void Invalidate()
        {
            _dirty = true;
        }

        private static long MakeKey(int cx, int cy)
        {
            // 支持负坐标：偏移到非负区间
            return ((long)(cx + 100000)) * 200001L + (cy + 100000);
        }

        private void AddToCell(int cx, int cy, ShapeBase shape)
        {
            long key = MakeKey(cx, cy);
            List<ShapeBase> list;
            if (!_cells.TryGetValue(key, out list))
            {
                list = new List<ShapeBase>();
                _cells[key] = list;
            }
            // 同一个 Shape 可能跨多个单元格，用 Contains 去重
            if (!list.Contains(shape))
                list.Add(shape);
        }

        /// <summary>根据当前 Shape 集合重建索引。</summary>
        public void Rebuild(IEnumerable<ShapeBase> shapes)
        {
            _cells.Clear();
            if (shapes != null)
            {
                foreach (ShapeBase s in shapes)
                {
                    if (s == null) continue;
                    RectangleF b = s.Bounds;
                    int minX = (int)Math.Floor(b.Left / _cellSize);
                    int maxX = (int)Math.Floor(b.Right / _cellSize);
                    int minY = (int)Math.Floor(b.Top / _cellSize);
                    int maxY = (int)Math.Floor(b.Bottom / _cellSize);
                    for (int cx = minX; cx <= maxX; cx++)
                    {
                        for (int cy = minY; cy <= maxY; cy++)
                        {
                            AddToCell(cx, cy, s);
                        }
                    }
                }
            }
            _dirty = false;
        }

        /// <summary>查询可能包含指定点的 Shape（仅做粗筛，精确命中由调用方用 HitTest 判断）。</summary>
        public List<ShapeBase> Query(PointF pt)
        {
            int cx = (int)Math.Floor(pt.X / _cellSize);
            int cy = (int)Math.Floor(pt.Y / _cellSize);
            long key = MakeKey(cx, cy);
            List<ShapeBase> list;
            if (_cells.TryGetValue(key, out list))
                return list;
            return null;
        }

        /// <summary>查询可能与指定矩形相交的 Shape（粗筛）。</summary>
        public List<ShapeBase> Query(RectangleF rect)
        {
            // 用 HashSet 去重，因为一个 Shape 可能落在多个单元格
            HashSet<ShapeBase> set = new HashSet<ShapeBase>();
            int minX = (int)Math.Floor(rect.Left / _cellSize);
            int maxX = (int)Math.Floor(rect.Right / _cellSize);
            int minY = (int)Math.Floor(rect.Top / _cellSize);
            int maxY = (int)Math.Floor(rect.Bottom / _cellSize);
            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cy = minY; cy <= maxY; cy++)
                {
                    long key = MakeKey(cx, cy);
                    List<ShapeBase> list;
                    if (_cells.TryGetValue(key, out list))
                    {
                        foreach (ShapeBase s in list)
                            set.Add(s);
                    }
                }
            }
            return new List<ShapeBase>(set);
        }
    }
}
