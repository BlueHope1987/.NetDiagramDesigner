using System;
using System.Collections.Generic;
using System.Drawing;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 在可缩放区域边界延长线与图形路径边的交点处自动插入顶点。
    /// 直线边插入无句柄顶点；曲线边按 DeCasteljau 分割插入带句柄顶点，
    /// 使差分缩放时每条边都完全落在单一缩放区间内，避免变形失真。
    /// 
    /// 本工具支持两种模式：
    /// 1. 设计时（InsertIntersectionVertices）：直接修改路径顶点列表，序列化保存
    /// 2. 渲染时（SplitEdgeAtZoneLines）：不修改原始数据，仅在渲染时虚拟分割边
    /// </summary>
    public static class ZoneIntersectionHelper
    {
        // === 贝塞尔子段 ===
        private struct BezierSeg
        {
            public PointF P0, C1, C2, P3;
            public BezierSeg(PointF p0, PointF c1, PointF c2, PointF p3)
            { P0 = p0; C1 = c1; C2 = c2; P3 = p3; }
        }

        private struct SplitResult
        {
            public BezierSeg Left, Right;
            public PointF SplitPoint;
        }

        // === 渲染时虚拟分割的子边 ===
        /// <summary>
        /// 一条子边（直线或贝塞尔曲线），所有坐标均为归一化（0-1）。
        /// 渲染时各点独立通过顶点变换器映射，实现差分缩放。
        /// </summary>
        public class SubEdge
        {
            public PointF P1, P2;
            public bool IsCurve;
            public PointF C1, C2;
        }

        /// <summary>
        /// 将一条边在可缩放区域边界线交点处分割为多条子边。
        /// 不修改原始数据，返回子边列表供渲染使用。
        /// 若无可缩放区域或无交点，返回包含原始边的单元素列表。
        /// </summary>
        /// <param name="p1">起点（归一化坐标）</param>
        /// <param name="p2">终点（归一化坐标）</param>
        /// <param name="h1">起点句柄类型</param>
        /// <param name="h2">终点句柄类型</param>
        /// <param name="handleOut1">起点出边句柄偏移（归一化）</param>
        /// <param name="handleIn2">终点进边句柄偏移（归一化）</param>
        /// <param name="zoneBounds">可缩放区域边界（归一化）</param>
        public static List<SubEdge> SplitEdgeAtZoneLines(
            PointF p1, PointF p2,
            HandleType h1, HandleType h2,
            PointF handleOut1, PointF handleIn2,
            RectangleF zoneBounds)
        {
            List<SubEdge> result = new List<SubEdge>();

            bool isCurve = (h1 != HandleType.None || h2 != HandleType.None);
            float[] xLines = { zoneBounds.Left, zoneBounds.Right };
            float[] yLines = { zoneBounds.Top, zoneBounds.Bottom };

            if (!isCurve)
            {
                // === 直线边 ===
                List<KeyValuePair<float, PointF>> intersections =
                    new List<KeyValuePair<float, PointF>>();

                foreach (float xLine in xLines)
                {
                    float? t = LineVsVertical(p1, p2, xLine);
                    if (t.HasValue && t.Value > 0.001f && t.Value < 0.999f)
                        intersections.Add(new KeyValuePair<float, PointF>(t.Value,
                            new PointF(xLine, p1.Y + t.Value * (p2.Y - p1.Y))));
                }
                foreach (float yLine in yLines)
                {
                    float? t = LineVsHorizontal(p1, p2, yLine);
                    if (t.HasValue && t.Value > 0.001f && t.Value < 0.999f)
                        intersections.Add(new KeyValuePair<float, PointF>(t.Value,
                            new PointF(p1.X + t.Value * (p2.X - p1.X), yLine)));
                }

                if (intersections.Count == 0)
                {
                    result.Add(new SubEdge { P1 = p1, P2 = p2, IsCurve = false });
                    return result;
                }

                intersections.Sort((a, b) => a.Key.CompareTo(b.Key));
                // 去重
                List<KeyValuePair<float, PointF>> unique = new List<KeyValuePair<float, PointF>>();
                foreach (var kv in intersections)
                {
                    bool dup = false;
                    foreach (var u in unique)
                        if (Math.Abs(kv.Key - u.Key) < 0.005f) { dup = true; break; }
                    if (!dup) unique.Add(kv);
                }

                PointF curP = p1;
                foreach (var kv in unique)
                {
                    result.Add(new SubEdge { P1 = curP, P2 = kv.Value, IsCurve = false });
                    curP = kv.Value;
                }
                result.Add(new SubEdge { P1 = curP, P2 = p2, IsCurve = false });
            }
            else
            {
                // === 曲线边 ===
                PointF c1 = (h1 != HandleType.None) ?
                    new PointF(p1.X + handleOut1.X, p1.Y + handleOut1.Y) : p1;
                PointF c2 = (h2 != HandleType.None) ?
                    new PointF(p2.X + handleIn2.X, p2.Y + handleIn2.Y) : p2;

                BezierSeg bez = new BezierSeg(p1, c1, c2, p2);
                List<KeyValuePair<float, PointF>> intersections =
                    FindBezierZoneIntersections(bez, xLines, yLines);

                if (intersections.Count == 0)
                {
                    result.Add(new SubEdge
                    {
                        P1 = p1, P2 = p2, IsCurve = true,
                        C1 = c1, C2 = c2
                    });
                    return result;
                }

                intersections.Sort((a, b) => a.Key.CompareTo(b.Key));
                List<KeyValuePair<float, PointF>> unique = new List<KeyValuePair<float, PointF>>();
                foreach (var kv in intersections)
                {
                    bool dup = false;
                    foreach (var u in unique)
                        if (Math.Abs(kv.Key - u.Key) < 0.005f) { dup = true; break; }
                    if (!dup) unique.Add(kv);
                }

                PointF curP0 = bez.P0, curC1 = bez.C1, curC2 = bez.C2, curP3 = bez.P3;
                float tOffset = 0f;

                foreach (var kv in unique)
                {
                    float t = kv.Key;
                    float remappedT = (t - tOffset) / (1f - tOffset);
                    if (remappedT <= 0.001f || remappedT >= 0.999f)
                        continue;

                    SplitResult sr = DeCasteljauSplit(
                        new BezierSeg(curP0, curC1, curC2, curP3), remappedT);

                    // 左子段作为子边
                    result.Add(new SubEdge
                    {
                        P1 = sr.Left.P0, P2 = sr.SplitPoint,
                        IsCurve = true,
                        C1 = sr.Left.C1, C2 = sr.Left.C2
                    });

                    curP0 = sr.SplitPoint;
                    curC1 = sr.Right.C1;
                    curC2 = sr.Right.C2;
                    curP3 = bez.P3;
                    tOffset = t;
                }

                // 最后一段
                result.Add(new SubEdge
                {
                    P1 = curP0, P2 = bez.P3,
                    IsCurve = true,
                    C1 = curC1, C2 = curC2
                });
            }

            return result;
        }

        /// <summary>
        /// 在路径中，于可缩放区域边界延长线与路径边的交点处插入新顶点。
        /// 修改 path 原地插入顶点。返回插入的顶点数。
        /// </summary>
        /// <param name="path">编辑器中的路径顶点列表</param>
        /// <param name="closed">路径是否闭合</param>
        /// <param name="zoneRect">可缩放区域在编辑器画布上的像素矩形</param>
        public static int InsertIntersectionVertices(
            List<CurveVertex> path, bool closed, RectangleF zoneRect)
        {
            if (path == null || path.Count < 2)
                return 0;

            // 4 条区域边界延长线
            float[] xLines = { zoneRect.Left, zoneRect.Right };
            float[] yLines = { zoneRect.Top, zoneRect.Bottom };

            int edgeCount = closed ? path.Count : path.Count - 1;
            int totalInserted = 0;

            // 从最后一条边向前处理，避免插入后索引偏移
            for (int edgeIdx = edgeCount - 1; edgeIdx >= 0; edgeIdx--)
            {
                int nextIdx = (edgeIdx + 1) % path.Count;
                CurveVertex v1 = path[edgeIdx];
                CurveVertex v2 = path[nextIdx];

                // 跨子图边界不处理
                if (v2.IsNewFigure)
                    continue;

                bool isCurve = (v1.Handle != HandleType.None || v2.Handle != HandleType.None);

                if (!isCurve)
                {
                    totalInserted += ProcessStraightEdge(path, edgeIdx, v1, v2, xLines, yLines);
                }
                else
                {
                    totalInserted += ProcessCurveEdge(path, edgeIdx, v1, v2, xLines, yLines);
                }
            }

            return totalInserted;
        }

        // ====================================================================
        // 直线边处理
        // ====================================================================

        private static int ProcessStraightEdge(
            List<CurveVertex> path, int edgeIdx,
            CurveVertex v1, CurveVertex v2,
            float[] xLines, float[] yLines)
        {
            List<KeyValuePair<float, PointF>> intersections =
                new List<KeyValuePair<float, PointF>>();

            PointF p1 = v1.Position;
            PointF p2 = v2.Position;

            // 垂直区域线（x = const）
            foreach (float xLine in xLines)
            {
                float? t = LineVsVertical(p1, p2, xLine);
                if (t.HasValue && t.Value > 0.001f && t.Value < 0.999f)
                {
                    PointF ip = new PointF(xLine, p1.Y + t.Value * (p2.Y - p1.Y));
                    intersections.Add(new KeyValuePair<float, PointF>(t.Value, ip));
                }
            }

            // 水平区域线（y = const）
            foreach (float yLine in yLines)
            {
                float? t = LineVsHorizontal(p1, p2, yLine);
                if (t.HasValue && t.Value > 0.001f && t.Value < 0.999f)
                {
                    PointF ip = new PointF(p1.X + t.Value * (p2.X - p1.X), yLine);
                    intersections.Add(new KeyValuePair<float, PointF>(t.Value, ip));
                }
            }

            if (intersections.Count == 0)
                return 0;

            // 去重：参数值相差小于 0.005 的视为同一点
            intersections.Sort((a, b) => a.Key.CompareTo(b.Key));
            List<KeyValuePair<float, PointF>> unique = new List<KeyValuePair<float, PointF>>();
            foreach (var kv in intersections)
            {
                bool dup = false;
                foreach (var u in unique)
                {
                    if (Math.Abs(kv.Key - u.Key) < 0.005f) { dup = true; break; }
                }
                if (!dup) unique.Add(kv);
            }

            // 从后向前插入（保持索引稳定）
            unique.Reverse();
            int inserted = 0;
            foreach (var kv in unique)
            {
                CurveVertex newVert = new CurveVertex(kv.Value);
                path.Insert(edgeIdx + 1, newVert);
                inserted++;
            }
            return inserted;
        }

        // ====================================================================
        // 曲线边处理
        // ====================================================================

        private static int ProcessCurveEdge(
            List<CurveVertex> path, int edgeIdx,
            CurveVertex v1, CurveVertex v2,
            float[] xLines, float[] yLines)
        {
            // 重建完整贝塞尔
            PointF p0 = v1.Position;
            PointF c1 = (v1.Handle != HandleType.None) ? v1.GetHandleOutAbs() : v1.Position;
            PointF c2 = (v2.Handle != HandleType.None) ? v2.GetHandleInAbs() : v2.Position;
            PointF p3 = v2.Position;

            BezierSeg bez = new BezierSeg(p0, c1, c2, p3);

            // 查找所有交点
            List<KeyValuePair<float, PointF>> intersections =
                FindBezierZoneIntersections(bez, xLines, yLines);

            if (intersections.Count == 0)
                return 0;

            // 去重
            intersections.Sort((a, b) => a.Key.CompareTo(b.Key));
            List<KeyValuePair<float, PointF>> unique = new List<KeyValuePair<float, PointF>>();
            foreach (var kv in intersections)
            {
                bool dup = false;
                foreach (var u in unique)
                {
                    if (Math.Abs(kv.Key - u.Key) < 0.005f) { dup = true; break; }
                }
                if (!dup) unique.Add(kv);
            }

            if (unique.Count == 0)
                return 0;

            // 按 t 升序逐次分割贝塞尔
            PointF curP0 = bez.P0, curC1 = bez.C1, curC2 = bez.C2, curP3 = bez.P3;
            float tOffset = 0f;
            List<CurveVertex> newVerts = new List<CurveVertex>();
            bool v1HandleUpdated = false;

            foreach (var kv in unique)
            {
                float t = kv.Key;
                float remappedT = (t - tOffset) / (1f - tOffset);
                if (remappedT <= 0.001f || remappedT >= 0.999f)
                    continue;

                SplitResult sr = DeCasteljauSplit(
                    new BezierSeg(curP0, curC1, curC2, curP3), remappedT);

                // 新顶点
                CurveVertex newVert = new CurveVertex(sr.SplitPoint);
                newVert.Handle = HandleType.Asymmetric;
                newVert.HandleIn = new PointF(
                    sr.Left.C2.X - sr.SplitPoint.X,
                    sr.Left.C2.Y - sr.SplitPoint.Y);
                newVert.HandleOut = new PointF(
                    sr.Right.C1.X - sr.SplitPoint.X,
                    sr.Right.C1.Y - sr.SplitPoint.Y);
                newVerts.Add(newVert);

                // 更新起始顶点的 HandleOut（仅首次）
                if (!v1HandleUpdated && v1.Handle != HandleType.None)
                {
                    v1.SetHandleOutAbs(sr.Left.C1);
                    v1HandleUpdated = true;
                }

                // 前进到右子段
                curP0 = sr.SplitPoint;
                curC1 = sr.Right.C1;
                curC2 = sr.Right.C2;
                curP3 = bez.P3;
                tOffset = t;
            }

            // 更新终止顶点的 HandleIn
            if (v2.Handle != HandleType.None && newVerts.Count > 0)
            {
                v2.SetHandleInAbs(curC2);
            }

            // 从后向前插入（保持索引稳定）
            newVerts.Reverse();
            int inserted = 0;
            foreach (CurveVertex nv in newVerts)
            {
                path.Insert(edgeIdx + 1, nv);
                inserted++;
            }
            return inserted;
        }

        // ====================================================================
        // 交点查找
        // ====================================================================

        /// <summary>线段与垂直线 x=xLine 的交点参数 t（0~1），无交点返回 null</summary>
        private static float? LineVsVertical(PointF p1, PointF p2, float xLine)
        {
            float dx = p2.X - p1.X;
            if (Math.Abs(dx) < 1e-9f) return null; // 平行
            float t = (xLine - p1.X) / dx;
            if (t < 0f || t > 1f) return null;
            return t;
        }

        /// <summary>线段与水平线 y=yLine 的交点参数 t（0~1），无交点返回 null</summary>
        private static float? LineVsHorizontal(PointF p1, PointF p2, float yLine)
        {
            float dy = p2.Y - p1.Y;
            if (Math.Abs(dy) < 1e-9f) return null; // 平行
            float t = (yLine - p1.Y) / dy;
            if (t < 0f || t > 1f) return null;
            return t;
        }

        /// <summary>
        /// 查找贝塞尔曲线与区域边界线的所有交点。
        /// 使用扁平采样 + 二分细化。
        /// </summary>
        private static List<KeyValuePair<float, PointF>> FindBezierZoneIntersections(
            BezierSeg bez, float[] xLines, float[] yLines)
        {
            List<KeyValuePair<float, PointF>> result =
                new List<KeyValuePair<float, PointF>>();

            const int samples = 64;
            PointF prev = EvalBezier(bez, 0f);

            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / samples;
                PointF cur = EvalBezier(bez, t);

                // 垂直线
                foreach (float xLine in xLines)
                {
                    if ((prev.X - xLine) * (cur.X - xLine) <= 0 &&
                        Math.Abs(prev.X - cur.X) > 1e-9f)
                    {
                        float tHit = RefineVertical(bez, xLine, t - 1f / samples, t);
                        if (tHit > 0.001f && tHit < 0.999f)
                        {
                            PointF ip = EvalBezier(bez, tHit);
                            ip.X = xLine; // 精确对齐
                            result.Add(new KeyValuePair<float, PointF>(tHit, ip));
                        }
                    }
                }

                // 水平线
                foreach (float yLine in yLines)
                {
                    if ((prev.Y - yLine) * (cur.Y - yLine) <= 0 &&
                        Math.Abs(prev.Y - cur.Y) > 1e-9f)
                    {
                        float tHit = RefineHorizontal(bez, yLine, t - 1f / samples, t);
                        if (tHit > 0.001f && tHit < 0.999f)
                        {
                            PointF ip = EvalBezier(bez, tHit);
                            ip.Y = yLine; // 精确对齐
                            result.Add(new KeyValuePair<float, PointF>(tHit, ip));
                        }
                    }
                }

                prev = cur;
            }

            return result;
        }

        /// <summary>二分法细化贝塞尔与垂直线 x=xLine 的交点参数 t</summary>
        private static float RefineVertical(BezierSeg bez, float xLine, float tLo, float tHi)
        {
            for (int iter = 0; iter < 20; iter++)
            {
                float tMid = (tLo + tHi) * 0.5f;
                PointF p = EvalBezier(bez, tMid);
                if ((EvalBezier(bez, tLo).X - xLine) * (p.X - xLine) <= 0)
                    tHi = tMid;
                else
                    tLo = tMid;
            }
            return (tLo + tHi) * 0.5f;
        }

        /// <summary>二分法细化贝塞尔与水平线 y=yLine 的交点参数 t</summary>
        private static float RefineHorizontal(BezierSeg bez, float yLine, float tLo, float tHi)
        {
            for (int iter = 0; iter < 20; iter++)
            {
                float tMid = (tLo + tHi) * 0.5f;
                PointF p = EvalBezier(bez, tMid);
                if ((EvalBezier(bez, tLo).Y - yLine) * (p.Y - yLine) <= 0)
                    tHi = tMid;
                else
                    tLo = tMid;
            }
            return (tLo + tHi) * 0.5f;
        }

        // ====================================================================
        // 贝塞尔工具
        // ====================================================================

        private static PointF EvalBezier(BezierSeg bez, float t)
        {
            float u = 1f - t;
            float x = u * u * u * bez.P0.X
                    + 3 * u * u * t * bez.C1.X
                    + 3 * u * t * t * bez.C2.X
                    + t * t * t * bez.P3.X;
            float y = u * u * u * bez.P0.Y
                    + 3 * u * u * t * bez.C1.Y
                    + 3 * u * t * t * bez.C2.Y
                    + t * t * t * bez.P3.Y;
            return new PointF(x, y);
        }

        private static SplitResult DeCasteljauSplit(BezierSeg bez, float t)
        {
            PointF p0 = bez.P0, c1 = bez.C1, c2 = bez.C2, p3 = bez.P3;
            PointF m01 = Lerp(p0, c1, t);
            PointF m12 = Lerp(c1, c2, t);
            PointF m23 = Lerp(c2, p3, t);
            PointF m012 = Lerp(m01, m12, t);
            PointF m123 = Lerp(m12, m23, t);
            PointF m = Lerp(m012, m123, t);

            SplitResult sr = new SplitResult();
            sr.SplitPoint = m;
            sr.Left = new BezierSeg(p0, m01, m012, m);
            sr.Right = new BezierSeg(m, m123, m23, p3);
            return sr;
        }

        private static PointF Lerp(PointF a, PointF b, float t)
        {
            return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }
    }
}
