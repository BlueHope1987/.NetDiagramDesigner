using System;
using System.Collections.Generic;
using System.Drawing;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 曲线感知的布尔合并工具（轻量可靠版）。
    /// 保留原始顶点的贝塞尔句柄；仅在交点处生成新句柄，
    /// 新句柄直接取自原始曲线在该参数处的子段控制点，从而无损保留曲率。
    /// 边界判定直接使用 Region.IsVisible(PointF)，不分配位图。
    /// 支持多环结果（外轮廓 + 孔洞）。
    /// </summary>
    public static class CurveAwareMerge
    {
        private struct BezierSeg
        {
            public PointF P0, C1, C2, P3;
            public BezierSeg(PointF p0, PointF c1, PointF c2, PointF p3) { P0 = p0; C1 = c1; C2 = c2; P3 = p3; }
        }

        private struct SplitResult { public BezierSeg Left, Right; }

        // 原始路径的一条边（直线或三次贝塞尔），附带交点检测用的扁平采样与参数映射
        private class Edge
        {
            public int PathIndex;
            public int VertIndex;
            public bool IsCurve;
            public BezierSeg Bezier;
            public CurveVertex StartVertex;   // 起始原始顶点（用于保留句柄）
            public PointF EndPos;             // 终点位置（用于精确节点匹配）
            public PointF[] Samples;          // 扁平采样点
            public float[] SampleParams;      // 每个采样点在原始贝塞尔上的参数 t
        }

        // 两条边的交点（已映射回各自原始贝塞尔参数）
        private class Crossing
        {
            public PointF Position;
            public int Edge1;
            public float T1;
            public int Edge2;
            public float T2;
        }

        // 分割后的子边
        private class SubEdge
        {
            public BezierSeg Seg;              // 原始贝塞尔的精确子段
            public bool IsCurve;
            public int SourcePathIndex;
            public int SourceVertIndex;
            public CurveVertex OrigStartVertex; // 非空：起点是原始顶点(t1≈0)，保留原始句柄
            public float OrigT1, OrigT2;
            public PointF NodeStart;          // 链接/顶点用位置（交点=Crossing.Position，原始=顶点位置）
            public PointF NodeEnd;
            public bool IsBoundary;
            public bool Used;
        }

        // 用于精确端点匹配的网格键（0.5px 网格）
        private struct PointKey
        {
            public int X, Y;
            public PointKey(PointF p) { X = (int)Math.Round(p.X * 2f); Y = (int)Math.Round(p.Y * 2f); }
            public override int GetHashCode() { return X * 7349 ^ Y; }
            public override bool Equals(object o) { PointKey k = (PointKey)o; return X == k.X && Y == k.Y; }
        }

        // ====================================================================
        // 公开方法
        // ====================================================================

        /// <summary>
        /// 执行曲线感知的布尔合并。返回外轮廓 CurveVertex 列表（含贝塞尔句柄）；
        /// 孔洞通过 holes 参数返回。无法处理时返回 null（调用方回退到位图追踪）。
        /// </summary>
        public static List<CurveVertex> Merge(
            List<List<CurveVertex>> paths,
            List<BooleanOperation> boolOps,
            Region resultRegion,
            float boundsMinX, float boundsMinY,
            float boundsMaxX, float boundsMaxY,
            out List<List<CurveVertex>> holes)
        {
            holes = new List<List<CurveVertex>>();

            // Step 1: 由原始顶点构建边（含扁平采样）
            List<Edge> allEdges = new List<Edge>();
            for (int i = 0; i < paths.Count; i++)
            {
                if (paths[i] == null || paths[i].Count < 3) continue;
                List<Edge> pe = BuildEdges(paths[i], i);
                if (pe != null) allEdges.AddRange(pe);
            }
            if (allEdges.Count < 3) return null;

            // Step 2: 查找不同路径间的所有交点（映射回原始参数）
            List<Crossing> crossings = new List<Crossing>();
            for (int i = 0; i < allEdges.Count; i++)
                for (int j = i + 1; j < allEdges.Count; j++)
                    if (allEdges[i].PathIndex != allEdges[j].PathIndex)
                        FindIntersections(allEdges[i], i, allEdges[j], j, crossings);
            if (crossings.Count == 0) return null; // 无交点 → 回退位图追踪

            // Step 3: 在交点处分割边，生成精确子段
            List<SubEdge> allSubEdges = new List<SubEdge>();
            SplitEdgesAtCrossings(allEdges, crossings, allSubEdges);
            if (allSubEdges.Count == 0) return null;

            // Step 4: 边界测试（用曲线切线方向计算垂直测试点）
            for (int i = 0; i < allSubEdges.Count; i++)
                allSubEdges[i].IsBoundary = TestBoundary(allSubEdges[i], resultRegion);

            List<SubEdge> boundary = new List<SubEdge>();
            foreach (SubEdge se in allSubEdges) if (se.IsBoundary) boundary.Add(se);
            if (boundary.Count < 3) return null;

            // Step 5: 链接所有边界子边为闭合环（外轮廓 + 孔洞）
            List<List<SubEdge>> loops = ChainAllLoops(boundary);
            if (loops.Count == 0) return null;

            // Step 6: 按面积区分外轮廓与孔洞
            int outerIdx = 0;
            float maxArea = 0;
            for (int i = 0; i < loops.Count; i++)
            {
                float area = Math.Abs(SignedArea(loops[i]));
                if (area > maxArea) { maxArea = area; outerIdx = i; }
            }

            List<CurveVertex> outer = CreateVertices(loops[outerIdx]);
            if (outer == null || outer.Count < 3) return null;

            for (int i = 0; i < loops.Count; i++)
            {
                if (i == outerIdx) continue;
                List<CurveVertex> holeVerts = CreateVertices(loops[i]);
                if (holeVerts != null && holeVerts.Count >= 3)
                    holes.Add(holeVerts);
            }

            return outer;
        }

        // ====================================================================
        // Step 1: 构建边
        // ====================================================================

        private static List<Edge> BuildEdges(List<CurveVertex> verts, int pathIndex)
        {
            int n = verts.Count;
            if (n < 2) return null;
            List<Edge> edges = new List<Edge>();
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                CurveVertex v1 = verts[i];
                CurveVertex v2 = verts[next];
                Edge e = new Edge();
                e.PathIndex = pathIndex;
                e.VertIndex = i;
                e.StartVertex = v1;
                e.EndPos = v2.Position;
                e.IsCurve = v1.Handle != HandleType.None || v2.Handle != HandleType.None;
                PointF c1 = (e.IsCurve && v1.Handle != HandleType.None) ? v1.GetHandleOutAbs() : v1.Position;
                PointF c2 = (e.IsCurve && v2.Handle != HandleType.None) ? v2.GetHandleInAbs() : v2.Position;
                e.Bezier = new BezierSeg(v1.Position, c1, c2, v2.Position);

                if (!e.IsCurve)
                {
                    e.Samples = new PointF[] { v1.Position, v2.Position };
                    e.SampleParams = new float[] { 0f, 1f };
                }
                else
                {
                    List<PointF> pts = new List<PointF>();
                    List<float> pars = new List<float>();
                    FlattenCurve(e.Bezier, 0f, 1f, pts, pars, 0);
                    e.Samples = pts.ToArray();
                    e.SampleParams = pars.ToArray();
                }
                edges.Add(e);
            }
            return edges;
        }

        // 递归扁平化贝塞尔，收集采样点与对应原始参数（去重端点）
        private static void FlattenCurve(BezierSeg bez, float t1, float t2,
            List<PointF> pts, List<float> pars, int depth)
        {
            float d1 = PointToLineDist(bez.C1, bez.P0, bez.P3);
            float d2 = PointToLineDist(bez.C2, bez.P0, bez.P3);
            float chord = Dist(bez.P0, bez.P3);
            bool flat = depth > 14 || (d1 < 0.5f && d2 < 0.5f) || chord < 2f;
            if (flat)
            {
                if (pts.Count == 0 || Dist(pts[pts.Count - 1], bez.P0) > 0.01f) { pts.Add(bez.P0); pars.Add(t1); }
                if (Dist(pts[pts.Count - 1], bez.P3) > 0.01f) { pts.Add(bez.P3); pars.Add(t2); }
                return;
            }
            float mid = (t1 + t2) * 0.5f;
            SplitResult sr = DeCasteljauSplit(bez, 0.5f);
            FlattenCurve(sr.Left, t1, mid, pts, pars, depth + 1);
            FlattenCurve(sr.Right, mid, t2, pts, pars, depth + 1);
        }

        // ====================================================================
        // Step 2: 交点查找
        // ====================================================================

        private static void FindIntersections(Edge e1, int idx1, Edge e2, int idx2, List<Crossing> results)
        {
            PointF[] s1 = e1.Samples; float[] p1 = e1.SampleParams;
            PointF[] s2 = e2.Samples; float[] p2 = e2.SampleParams;
            for (int i = 0; i < s1.Length - 1; i++)
            {
                for (int j = 0; j < s2.Length - 1; j++)
                {
                    float t, s;
                    PointF? ip = LineLineIntersect(s1[i], s1[i + 1], s2[j], s2[j + 1], out t, out s);
                    if (!ip.HasValue) continue;
                    if (t <= 0.0001f || t >= 0.9999f || s <= 0.0001f || s >= 0.9999f) continue;
                    // 映射回原始贝塞尔参数（线性插值）
                    float ot1 = p1[i] + (p1[i + 1] - p1[i]) * t;
                    float ot2 = p2[j] + (p2[j + 1] - p2[j]) * s;
                    if (ot1 <= 0.001f || ot1 >= 0.999f || ot2 <= 0.001f || ot2 >= 0.999f) continue;
                    if (IsNearExisting(ip.Value, results)) continue; // 去重
                    results.Add(new Crossing { Position = ip.Value, Edge1 = idx1, T1 = ot1, Edge2 = idx2, T2 = ot2 });
                }
            }
        }

        private static bool IsNearExisting(PointF p, List<Crossing> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
                if (Dist(p, list[i].Position) < 0.5f) return true;
            return false;
        }

        // 线段-线段交点计算
        private static PointF? LineLineIntersect(PointF p1, PointF p2, PointF p3, PointF p4, out float t, out float s)
        {
            t = 0; s = 0;
            float dx1 = p2.X - p1.X, dy1 = p2.Y - p1.Y;
            float dx2 = p4.X - p3.X, dy2 = p4.Y - p3.Y;
            float denom = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(denom) < 1e-9f) return null;
            float dx3 = p3.X - p1.X, dy3 = p3.Y - p1.Y;
            t = (dx3 * dy2 - dy3 * dx2) / denom;
            s = (dx3 * dy1 - dy3 * dx1) / denom;
            if (t < -0.001f || t > 1.001f || s < -0.001f || s > 1.001f) return null;
            return new PointF(p1.X + t * dx1, p1.Y + t * dy1);
        }

        // ====================================================================
        // Step 3: 分割边
        // ====================================================================

        private static void SplitEdgesAtCrossings(List<Edge> allEdges, List<Crossing> crossings, List<SubEdge> results)
        {
            // 每条边收集其上的交点：(参数, 位置)
            Dictionary<int, List<KeyValuePair<float, PointF>>> splits =
                new Dictionary<int, List<KeyValuePair<float, PointF>>>();
            foreach (Crossing c in crossings)
            {
                RegisterSplit(splits, c.Edge1, c.T1, c.Position);
                RegisterSplit(splits, c.Edge2, c.T2, c.Position);
            }

            for (int ei = 0; ei < allEdges.Count; ei++)
            {
                Edge e = allEdges[ei];
                List<KeyValuePair<float, PointF>> list;
                if (!splits.TryGetValue(ei, out list) || list.Count == 0)
                {
                    results.Add(MakeSubEdge(e, 0f, 1f, e.StartVertex.Position, e.EndPos));
                    continue;
                }
                list.Sort(CompareParam);
                // 清理：去重参数、剔除端点
                List<KeyValuePair<float, PointF>> pts = new List<KeyValuePair<float, PointF>>();
                float last = -1f;
                foreach (KeyValuePair<float, PointF> kv in list)
                {
                    float tp = kv.Key;
                    if (tp <= 0.002f || tp >= 0.998f) continue;
                    if (Math.Abs(tp - last) < 0.002f) continue;
                    pts.Add(kv); last = tp;
                }
                if (pts.Count == 0)
                {
                    results.Add(MakeSubEdge(e, 0f, 1f, e.StartVertex.Position, e.EndPos));
                    continue;
                }
                results.Add(MakeSubEdge(e, 0f, pts[0].Key, e.StartVertex.Position, pts[0].Value));
                for (int k = 1; k < pts.Count; k++)
                    results.Add(MakeSubEdge(e, pts[k - 1].Key, pts[k].Key, pts[k - 1].Value, pts[k].Value));
                results.Add(MakeSubEdge(e, pts[pts.Count - 1].Key, 1f, pts[pts.Count - 1].Value, e.EndPos));
            }
        }

        private static void RegisterSplit(Dictionary<int, List<KeyValuePair<float, PointF>>> dict, int edgeIdx, float t, PointF pos)
        {
            List<KeyValuePair<float, PointF>> list;
            if (!dict.TryGetValue(edgeIdx, out list)) { list = new List<KeyValuePair<float, PointF>>(); dict[edgeIdx] = list; }
            list.Add(new KeyValuePair<float, PointF>(t, pos));
        }

        private static int CompareParam(KeyValuePair<float, PointF> a, KeyValuePair<float, PointF> b) { return a.Key.CompareTo(b.Key); }

        // 由原始边与参数区间 [t1,t2] 创建子边
        private static SubEdge MakeSubEdge(Edge e, float t1, float t2, PointF nodeStart, PointF nodeEnd)
        {
            SubEdge se = new SubEdge();
            se.IsCurve = e.IsCurve;
            se.SourcePathIndex = e.PathIndex;
            se.SourceVertIndex = e.VertIndex;
            se.OrigT1 = t1; se.OrigT2 = t2;
            se.NodeStart = nodeStart; se.NodeEnd = nodeEnd;
            se.OrigStartVertex = (t1 <= 0.001f) ? e.StartVertex : null;
            if (e.IsCurve) se.Seg = CubicBezierSubsegment(e.Bezier, t1, t2);
            else
            {
                PointF a = Lerp(e.Bezier.P0, e.Bezier.P3, t1);
                PointF b = Lerp(e.Bezier.P0, e.Bezier.P3, t2);
                se.Seg = new BezierSeg(a, a, b, b);
            }
            return se;
        }

        // ====================================================================
        // 贝塞尔核心
        // ====================================================================

        private static BezierSeg CubicBezierSubsegment(BezierSeg bez, float t1, float t2)
        {
            if (t1 <= 0.001f && t2 >= 0.999f) return bez;
            SplitResult first = DeCasteljauSplit(bez, t1);
            float denom = 1f - t1;
            if (denom < 0.001f) denom = 0.001f;
            float rt = (t2 - t1) / denom;
            if (rt > 0.999f) rt = 0.999f;
            if (rt < 0.001f) rt = 0.001f;
            SplitResult second = DeCasteljauSplit(first.Right, rt);
            return second.Left;
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
            return new SplitResult { Left = new BezierSeg(p0, m01, m012, m), Right = new BezierSeg(m, m123, m23, p3) };
        }

        /// <summary>计算三次贝塞尔曲线在参数 t 处的点</summary>
        private static PointF EvalBezier(BezierSeg bez, float t)
        {
            float u = 1f - t;
            float x = u * u * u * bez.P0.X + 3 * u * u * t * bez.C1.X + 3 * u * t * t * bez.C2.X + t * t * t * bez.P3.X;
            float y = u * u * u * bez.P0.Y + 3 * u * u * t * bez.C1.Y + 3 * u * t * t * bez.C2.Y + t * t * t * bez.P3.Y;
            return new PointF(x, y);
        }

        // ====================================================================
        // Step 4: 边界测试
        // ====================================================================

        /// <summary>
        /// 测试子边是否在结果边界上。
        /// 关键修复：曲线边用中点(t=0.5)处的切线方向计算垂直测试点，
        /// 而非弦方向（P0→P3），避免曲率较大时误判。
        /// </summary>
        private static bool TestBoundary(SubEdge se, Region resultRegion)
        {
            PointF mid;
            float dx, dy;

            if (se.IsCurve)
            {
                // 用贝塞尔中点位置和中点切线方向
                mid = EvalBezier(se.Seg, 0.5f);
                PointF tan = BezierTangent(se.Seg, 0.5f);
                dx = tan.X;
                dy = tan.Y;
            }
            else
            {
                mid = MidPoint(se.Seg.P0, se.Seg.P3);
                dx = se.Seg.P3.X - se.Seg.P0.X;
                dy = se.Seg.P3.Y - se.Seg.P0.Y;
            }

            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return false;
            float px = -dy / len, py = dx / len; // 垂直方向

            // 用多个偏移距离测试以提高可靠性
            for (int i = 0; i < 3; i++)
            {
                float off;
                switch (i)
                {
                    case 0: off = 1.0f; break;
                    case 1: off = 2.0f; break;
                    default: off = 4.0f; break;
                }
                PointF left = new PointF(mid.X + px * off, mid.Y + py * off);
                PointF right = new PointF(mid.X - px * off, mid.Y - py * off);
                if (resultRegion.IsVisible(left) != resultRegion.IsVisible(right)) return true;
            }
            return false;
        }

        // ====================================================================
        // Step 5: 链接所有边界子边为闭合环
        // ====================================================================

        /// <summary>
        /// 查找所有闭合环。每次从未使用的边界子边开始，链接成一个闭合环，
        /// 直到所有边界子边都被使用或无法继续链接。
        /// </summary>
        private static List<List<SubEdge>> ChainAllLoops(List<SubEdge> boundary)
        {
            List<List<SubEdge>> loops = new List<List<SubEdge>>();

            // 按起点/终点建索引
            Dictionary<PointKey, List<int>> startMap = new Dictionary<PointKey, List<int>>();
            Dictionary<PointKey, List<int>> endMap = new Dictionary<PointKey, List<int>>();
            for (int i = 0; i < boundary.Count; i++)
            {
                IndexAdd(startMap, new PointKey(boundary[i].NodeStart), i);
                IndexAdd(endMap, new PointKey(boundary[i].NodeEnd), i);
            }

            int maxLoops = boundary.Count;
            while (maxLoops-- > 0)
            {
                // 找到第一条未使用的边界子边作为起点
                int startIdx = -1;
                for (int i = 0; i < boundary.Count; i++)
                    if (!boundary[i].Used) { startIdx = i; break; }
                if (startIdx < 0) break; // 全部已用

                List<SubEdge> loop = ChainOneLoop(boundary, startMap, endMap, startIdx);
                if (loop != null && loop.Count >= 3)
                    loops.Add(loop);
                else
                {
                    // 无法形成闭合环，标记为已用避免死循环
                    if (startIdx >= 0) boundary[startIdx].Used = true;
                }
            }

            return loops;
        }

        /// <summary>从指定起点链接一个闭合环</summary>
        private static List<SubEdge> ChainOneLoop(List<SubEdge> boundary,
            Dictionary<PointKey, List<int>> startMap, Dictionary<PointKey, List<int>> endMap,
            int startIdx)
        {
            List<SubEdge> chained = new List<SubEdge>();
            boundary[startIdx].Used = true;
            chained.Add(boundary[startIdx]);
            PointF chainStart = boundary[startIdx].NodeStart;
            PointF curEnd = boundary[startIdx].NodeEnd;
            int maxIter = boundary.Count * 2 + 4;
            while (maxIter-- > 0)
            {
                if (Dist(curEnd, chainStart) < 0.5f) break; // 闭合
                int nextIdx; bool reversed;
                if (!FindNext(boundary, startMap, endMap, curEnd, out nextIdx, out reversed)) break;
                boundary[nextIdx].Used = true;
                if (reversed) { SubEdge rev = ReverseSubEdge(boundary[nextIdx]); chained.Add(rev); curEnd = rev.NodeEnd; }
                else { chained.Add(boundary[nextIdx]); curEnd = boundary[nextIdx].NodeEnd; }
            }
            if (chained.Count < 3) return null;
            if (Dist(chained[0].NodeStart, chained[chained.Count - 1].NodeEnd) > 1.0f) return null; // 必须闭合
            return chained;
        }

        private static void IndexAdd(Dictionary<PointKey, List<int>> dict, PointKey k, int idx)
        {
            List<int> list;
            if (!dict.TryGetValue(k, out list)) { list = new List<int>(); dict[k] = list; }
            list.Add(idx);
        }

        private static bool FindNext(List<SubEdge> boundary,
            Dictionary<PointKey, List<int>> startMap, Dictionary<PointKey, List<int>> endMap,
            PointF curEnd, out int bestIdx, out bool bestReversed)
        {
            bestIdx = -1; bestReversed = false;
            float bestDist = float.MaxValue;
            PointKey k = new PointKey(curEnd);
            List<int> fwd;
            if (startMap.TryGetValue(k, out fwd))
                foreach (int i in fwd) { if (boundary[i].Used) continue; float d = Dist(boundary[i].NodeStart, curEnd); if (d < bestDist) { bestDist = d; bestIdx = i; bestReversed = false; } }
            List<int> rev;
            if (endMap.TryGetValue(k, out rev))
                foreach (int i in rev) { if (boundary[i].Used) continue; float d = Dist(boundary[i].NodeEnd, curEnd); if (d < bestDist) { bestDist = d; bestIdx = i; bestReversed = true; } }
            return bestIdx >= 0 && bestDist <= 1.0f;
        }

        private static SubEdge ReverseSubEdge(SubEdge se)
        {
            SubEdge r = new SubEdge();
            r.IsCurve = se.IsCurve;
            r.Seg = new BezierSeg(se.Seg.P3, se.Seg.C2, se.Seg.C1, se.Seg.P0);
            r.SourcePathIndex = se.SourcePathIndex;
            r.SourceVertIndex = se.SourceVertIndex;
            r.OrigT1 = se.OrigT2; r.OrigT2 = se.OrigT1;
            r.NodeStart = se.NodeEnd; r.NodeEnd = se.NodeStart;
            r.OrigStartVertex = null;
            r.IsBoundary = se.IsBoundary; r.Used = se.Used;
            return r;
        }

        // ====================================================================
        // Step 6: 创建 CurveVertex 列表
        // ====================================================================

        private static List<CurveVertex> CreateVertices(List<SubEdge> chained)
        {
            List<CurveVertex> result = new List<CurveVertex>();
            int n = chained.Count;
            for (int i = 0; i < n; i++)
            {
                SubEdge cur = chained[i];
                SubEdge prev = chained[(i - 1 + n) % n];
                if (cur.OrigStartVertex != null) { result.Add(cur.OrigStartVertex.Clone()); continue; }
                // 交点：绝对控制点 = Seg.C1 / prev.Seg.C2
                PointF pos = cur.NodeStart;
                PointF outAbs = cur.Seg.C1;
                PointF inAbs = prev.Seg.C2;
                if (Dist(outAbs, pos) < 0.01f) { PointF tan = BezierTangent(cur.Seg, 0f); outAbs = new PointF(pos.X + tan.X, pos.Y + tan.Y); }
                if (Dist(inAbs, pos) < 0.01f) { PointF tan = BezierTangent(prev.Seg, 1f); inAbs = new PointF(pos.X - tan.X, pos.Y - tan.Y); }
                PointF hOut = new PointF(outAbs.X - pos.X, outAbs.Y - pos.Y);
                PointF hIn = new PointF(inAbs.X - pos.X, inAbs.Y - pos.Y);
                result.Add(new CurveVertex(pos, HandleType.Asymmetric, hIn, hOut));
            }
            return result;
        }

        // ====================================================================
        // 辅助：有向面积（用于区分外轮廓与孔洞）
        // ====================================================================

        private static float SignedArea(List<SubEdge> loop)
        {
            float area = 0f;
            foreach (SubEdge se in loop)
            {
                area += (se.NodeStart.X * se.NodeEnd.Y) - (se.NodeEnd.X * se.NodeStart.Y);
            }
            return area * 0.5f;
        }

        // ====================================================================
        // 贝塞尔切线
        // ====================================================================

        private static PointF BezierTangent(BezierSeg bez, float t)
        {
            float u = 1f - t;
            float x = 3 * u * u * (bez.C1.X - bez.P0.X) + 6 * u * t * (bez.C2.X - bez.C1.X) + 3 * t * t * (bez.P3.X - bez.C2.X);
            float y = 3 * u * u * (bez.C1.Y - bez.P0.Y) + 6 * u * t * (bez.C2.Y - bez.C1.Y) + 3 * t * t * (bez.P3.Y - bez.C2.Y);
            return new PointF(x, y);
        }

        // ====================================================================
        // 数学工具方法
        // ====================================================================

        private static float Dist(PointF a, PointF b) { float dx = a.X - b.X, dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy); }
        private static PointF Lerp(PointF a, PointF b, float t) { return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }
        private static PointF MidPoint(PointF a, PointF b) { return new PointF((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f); }
        private static PointF Normalize(PointF v) { float len = (float)Math.Sqrt(v.X * v.X + v.Y * v.Y); return (len < 1e-9f) ? new PointF(0, 0) : new PointF(v.X / len, v.Y / len); }
        private static float Clamp(float val, float min, float max) { return val < min ? min : (val > max ? max : val); }
        private static float PointToLineDist(PointF p, PointF a, PointF b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-9f) return Dist(p, a);
            float t = Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0f, 1f);
            return Dist(p, new PointF(a.X + t * dx, a.Y + t * dy));
        }
    }
}
