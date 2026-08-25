using System;
using System.Collections.Generic;
using System.Drawing;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 曲线感知的布尔合并工具（精确版）。
    /// 保留原始顶点的贝塞尔句柄；仅在交点处生成新句柄，
    /// 新句柄直接取自原始曲线在该参数处的子段控制点，从而无损保留曲率。
    /// 
    /// 边界判定使用精确的"点在路径内"测试（射线法 + 曲线扁平化），
    /// 按布尔运算语义直接计算，不依赖 Region 像素栅格化，
    /// 因此曲线脚点稳定、孔洞可检测、多区域全部保留。
    /// 
    /// 支持多外轮廓输出 + 孔洞。
    /// </summary>
    public static class CurveAwareMerge
    {
        /// <summary>诊断信息：记录上次 Merge 调用的执行状态，供调用方排查问题</summary>
        public static string LastDebugInfo = "";
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

        // 路径的扁平表示（用于 PIP 测试）
        private class FlatPath
        {
            public PointF[] Points; // 闭合多边形点序列（首尾不重复）
        }

        // ====================================================================
        // 公开方法
        // ====================================================================

        /// <summary>
        /// 执行曲线感知的布尔合并。
        /// 返回所有外轮廓列表（每个外轮廓为 CurveVertex 列表，含贝塞尔句柄）。
        /// 孔洞通过 holes 参数返回（与外轮廓一一对应：holes[i] 是 outers[i] 的孔洞列表）。
        /// 无法处理时返回 null（调用方回退到位图追踪）。
        /// </summary>
        public static List<List<CurveVertex>> Merge(
            List<List<CurveVertex>> paths,
            List<BooleanOperation> boolOps,
            object resultRegion, // 保留参数兼容性，内部不再使用
            float boundsMinX, float boundsMinY,
            float boundsMaxX, float boundsMaxY,
            out List<List<List<CurveVertex>>> holes)
        {
            holes = new List<List<List<CurveVertex>>>();
            LastDebugInfo = "Merge started. ";

            if (paths == null || paths.Count < 2) { LastDebugInfo += "FAIL: paths<2. "; return null; }
            LastDebugInfo += "paths=" + paths.Count + ". ";

            // Step 0: 预扁平化所有路径（用于精确 PIP 测试）
            FlatPath[] flatPaths = new FlatPath[paths.Count];
            for (int i = 0; i < paths.Count; i++)
            {
                if (paths[i] == null || paths[i].Count < 3) { LastDebugInfo += "FAIL: path[" + i + "] invalid. "; return null; }
                flatPaths[i] = FlattenPath(paths[i]);
            }
            LastDebugInfo += "flatPaths ok. ";

            // Step 1: 由原始顶点构建边（含扁平采样）
            List<Edge> allEdges = new List<Edge>();
            for (int i = 0; i < paths.Count; i++)
            {
                List<Edge> pe = BuildEdges(paths[i], i);
                if (pe != null) allEdges.AddRange(pe);
            }
            if (allEdges.Count < 3) { LastDebugInfo += "FAIL: edges<3. "; return null; }
            LastDebugInfo += "edges=" + allEdges.Count + ". ";

            // Step 2: 查找不同路径间的所有交点（映射回原始参数）
            List<Crossing> crossings = new List<Crossing>();
            for (int i = 0; i < allEdges.Count; i++)
                for (int j = i + 1; j < allEdges.Count; j++)
                    if (allEdges[i].PathIndex != allEdges[j].PathIndex)
                        FindIntersections(allEdges[i], i, allEdges[j], j, crossings);
            LastDebugInfo += "crossings=" + crossings.Count + ". ";

            // Step 3: 在交点处分割边，生成精确子段
            List<SubEdge> allSubEdges = new List<SubEdge>();
            SplitEdgesAtCrossings(allEdges, crossings, allSubEdges);
            if (allSubEdges.Count == 0) { LastDebugInfo += "FAIL: subEdges=0. "; return null; }
            LastDebugInfo += "subEdges=" + allSubEdges.Count + ". ";

            // Step 4: 边界测试 —— 用精确 PIP + 布尔运算语义
            for (int i = 0; i < allSubEdges.Count; i++)
                allSubEdges[i].IsBoundary = TestBoundaryByPIP(allSubEdges[i], flatPaths, boolOps);

            List<SubEdge> boundary = new List<SubEdge>();
            foreach (SubEdge se in allSubEdges) if (se.IsBoundary) boundary.Add(se);
            if (boundary.Count < 3) { LastDebugInfo += "FAIL: boundary<3 (count=" + boundary.Count + "). "; return null; }
            LastDebugInfo += "boundary=" + boundary.Count + ". ";

            // Step 5: 链接所有边界子边为闭合环
            List<List<SubEdge>> loops = ChainAllLoops(boundary);
            if (loops.Count == 0) { LastDebugInfo += "FAIL: loops=0. "; return null; }
            LastDebugInfo += "loops=" + loops.Count + ". ";

            // Step 6: 分类所有环 —— 区分外轮廓与孔洞，建立嵌套关系
            List<int> outerLoopIndices;
            List<List<int>> holeIndicesPerOuter;
            ClassifyLoops(loops, flatPaths, boolOps, out outerLoopIndices, out holeIndicesPerOuter);

            if (outerLoopIndices == null || outerLoopIndices.Count == 0) { LastDebugInfo += "FAIL: no outers. "; return null; }
            LastDebugInfo += "outers=" + outerLoopIndices.Count + ". ";

            // Step 7: 转换为 CurveVertex
            List<List<CurveVertex>> result = new List<List<CurveVertex>>();
            for (int o = 0; o < outerLoopIndices.Count; o++)
            {
                List<CurveVertex> outer = CreateVertices(loops[outerLoopIndices[o]]);
                if (outer == null || outer.Count < 3) continue;
                result.Add(outer);

                List<List<CurveVertex>> holeList = new List<List<CurveVertex>>();
                if (holeIndicesPerOuter != null && o < holeIndicesPerOuter.Count)
                {
                    foreach (int hi in holeIndicesPerOuter[o])
                    {
                        List<CurveVertex> holeVerts = CreateVertices(loops[hi]);
                        if (holeVerts != null && holeVerts.Count >= 3)
                            holeList.Add(holeVerts);
                    }
                }
                holes.Add(holeList);
            }

            if (result.Count == 0) { LastDebugInfo += "FAIL: result=0. "; return null; }
            LastDebugInfo += "SUCCESS outers=" + result.Count + " holes=" + holes.Count + ". ";
            return result;
        }

        // ====================================================================
        // 路径扁平化（用于 PIP 测试）
        // ====================================================================

        private static FlatPath FlattenPath(List<CurveVertex> verts)
        {
            FlatPath fp = new FlatPath();
            List<PointF> pts = new List<PointF>();
            int n = verts.Count;

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                CurveVertex v1 = verts[i];
                CurveVertex v2 = verts[next];
                bool isCurve = v1.Handle != HandleType.None || v2.Handle != HandleType.None;

                if (!isCurve)
                {
                    if (pts.Count == 0 || Dist(pts[pts.Count - 1], v1.Position) > 0.01f)
                        pts.Add(v1.Position);
                }
                else
                {
                    PointF c1 = (v1.Handle != HandleType.None) ? v1.GetHandleOutAbs() : v1.Position;
                    PointF c2 = (v2.Handle != HandleType.None) ? v2.GetHandleInAbs() : v2.Position;
                    BezierSeg bez = new BezierSeg(v1.Position, c1, c2, v2.Position);
                    List<PointF> segPts = new List<PointF>();
                    FlattenBezierSimple(bez, segPts, 0);
                    // 去掉重复的起点（与上一段终点重合）
                    for (int k = 0; k < segPts.Count; k++)
                    {
                        if (pts.Count > 0 && Dist(pts[pts.Count - 1], segPts[k]) < 0.01f) continue;
                        pts.Add(segPts[k]);
                    }
                }
            }

            // 去掉末尾与开头重复的点
            if (pts.Count > 1 && Dist(pts[0], pts[pts.Count - 1]) < 0.5f)
                pts.RemoveAt(pts.Count - 1);

            fp.Points = pts.ToArray();
            return fp;
        }

        private static void FlattenBezierSimple(BezierSeg bez, List<PointF> pts, int depth)
        {
            float d1 = PointToLineDist(bez.C1, bez.P0, bez.P3);
            float d2 = PointToLineDist(bez.C2, bez.P0, bez.P3);
            float chord = Dist(bez.P0, bez.P3);
            // 更高精度：0.1px 偏差
            bool flat = depth > 16 || (d1 < 0.1f && d2 < 0.1f) || chord < 0.5f;
            if (flat)
            {
                if (pts.Count == 0 || Dist(pts[pts.Count - 1], bez.P0) > 0.01f)
                    pts.Add(bez.P0);
                if (Dist(pts[pts.Count - 1], bez.P3) > 0.01f)
                    pts.Add(bez.P3);
                return;
            }
            SplitResult sr = DeCasteljauSplit(bez, 0.5f);
            FlattenBezierSimple(sr.Left, pts, depth + 1);
            FlattenBezierSimple(sr.Right, pts, depth + 1);
        }

        // ====================================================================
        // 点在路径内测试（射线法）
        // ====================================================================

        private static bool PointInPath(PointF p, FlatPath path)
        {
            PointF[] poly = path.Points;
            int n = poly.Length;
            if (n < 3) return false;

            // 射线法：从 p 向右发射水平射线，统计与多边形边的交点数
            // 奇数 = 在内，偶数 = 在外
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                PointF pi = poly[i];
                PointF pj = poly[j];

                // 检查边是否与水平射线相交
                // 边的两个端点必须在射线的上下两侧
                if ((pi.Y > p.Y) != (pj.Y > p.Y))
                {
                    // 计算交点的 x 坐标
                    float xIntersect = (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y) + pi.X;
                    if (p.X < xIntersect)
                        inside = !inside;
                }
            }
            return inside;
        }

        // ====================================================================
        // 按布尔运算语义计算点是否在结果内
        // ====================================================================

        /// <summary>
        /// 计算点 p 在布尔运算结果的内部还是外部。
        /// 运算链：从下往上（最后一个路径为基底，前面依次应用运算）。
        /// 对于两路径：result = path[0] op[0] path[1]
        ///   即：以 path[1] 为基础，path[0] 用 op[0] 组合上去
        /// 注意：根据调用方，path[0] 是上层，path[1] 是下层
        ///   Union: 上层 ∪ 下层 → in0 || in1
        ///   Subtract: 上层 - 下层 → in0 && !in1
        ///   Intersect: 上层 ∩ 下层 → in0 && in1
        ///   Xor: 上层 ⊕ 下层 → in0 != in1
        /// </summary>
        private static bool EvalBoolean(PointF p, FlatPath[] flatPaths, List<BooleanOperation> boolOps)
        {
            if (flatPaths == null || flatPaths.Length == 0) return false;

            // 先计算每个路径的包含状态
            bool[] inside = new bool[flatPaths.Length];
            for (int i = 0; i < flatPaths.Length; i++)
                inside[i] = PointInPath(p, flatPaths[i]);

            // 两路径情况（最常见）
            if (flatPaths.Length == 2)
            {
                bool in0 = inside[0]; // 上层
                bool in1 = inside[1]; // 下层
                BooleanOperation op = boolOps[0]; // 上层的运算

                switch (op)
                {
                    case BooleanOperation.Union:
                        return in0 || in1;
                    case BooleanOperation.Subtract:
                        return in0 && !in1;
                    case BooleanOperation.Intersect:
                        return in0 && in1;
                    case BooleanOperation.Xor:
                        return in0 != in1;
                    default:
                        return in0 || in1;
                }
            }

            // 多路径：从下往上累积
            bool result = inside[flatPaths.Length - 1]; // 最底层
            for (int i = flatPaths.Length - 2; i >= 0; i--)
            {
                BooleanOperation op = boolOps[i];
                bool cur = inside[i];
                switch (op)
                {
                    case BooleanOperation.Union:
                        result = result || cur;
                        break;
                    case BooleanOperation.Subtract:
                        // 注意：i 是上层，对下层做减法 → 上层 - 下层累积
                        // 但累积顺序是从下往上，所以这里需要重新考虑
                        // 对于链 A op1 B op2 C，实际是 (A op1 B) op2 C
                        // 即上面的对下面的结果做运算
                        result = ApplyOp(cur, result, op);
                        break;
                    case BooleanOperation.Intersect:
                        result = result && cur;
                        break;
                    case BooleanOperation.Xor:
                        result = result != cur;
                        break;
                }
            }
            return result;
        }

        // upperOp lower：上层对下层做运算
        private static bool ApplyOp(bool upper, bool lower, BooleanOperation op)
        {
            switch (op)
            {
                case BooleanOperation.Union: return upper || lower;
                case BooleanOperation.Subtract: return upper && !lower;
                case BooleanOperation.Intersect: return upper && lower;
                case BooleanOperation.Xor: return upper != lower;
                default: return upper || lower;
            }
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
        // Step 4: 边界测试（PIP + 布尔语义版）
        // ====================================================================

        /// <summary>
        /// 测试子边是否在结果边界上。
        /// 取子边中点两侧的点（法向偏移），用精确 PIP + 布尔语义计算内外状态。
        /// 如果两侧状态不同，则该边在边界上。
        /// </summary>
        private static bool TestBoundaryByPIP(SubEdge se, FlatPath[] flatPaths, List<BooleanOperation> boolOps)
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
            float px = -dy / len, py = dx / len; // 垂直方向（左手系法线）

            // 用多个偏移距离测试以提高可靠性
            // 使用较小的偏移（0.5px, 1px, 2px）以避免跨过附近的边
            for (int i = 0; i < 3; i++)
            {
                float off;
                switch (i)
                {
                    case 0: off = 0.5f; break;
                    case 1: off = 1.0f; break;
                    default: off = 2.0f; break;
                }
                PointF left = new PointF(mid.X + px * off, mid.Y + py * off);
                PointF right = new PointF(mid.X - px * off, mid.Y - py * off);
                bool inLeft = EvalBoolean(left, flatPaths, boolOps);
                bool inRight = EvalBoolean(right, flatPaths, boolOps);
                if (inLeft != inRight) return true;
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
        // Step 6: 环分类 —— 区分外轮廓与孔洞
        // ====================================================================

        /// <summary>
        /// 分类所有环：哪些是外轮廓，哪些是孔洞，以及孔洞属于哪个外轮廓。
        /// 算法：
        /// 1. 计算每个环的有向面积，正向（逆时针）= 外轮廓候选，负向 = 孔洞候选
        /// 2. 用点包含测试确定嵌套关系：孔洞在哪个外轮廓内
        /// 3. 处理多层嵌套（岛中洞）：奇数层为外，偶数层为洞
        /// </summary>
        private static void ClassifyLoops(List<List<SubEdge>> loops,
            FlatPath[] flatPaths, List<BooleanOperation> boolOps,
            out List<int> outerIndices, out List<List<int>> holeIndicesPerOuter)
        {
            int n = loops.Count;
            float[] areas = new float[n];
            PointF[] repPoints = new PointF[n]; // 环代表点（边界顶点，用于包含测试）

            for (int i = 0; i < n; i++)
            {
                areas[i] = SignedArea(loops[i]);
                repPoints[i] = GetLoopRepresentativePoint(loops[i]);
            }

            // 构建每个环的扁平多边形（用于环之间的包含测试）
            FlatPath[] loopPaths = new FlatPath[n];
            for (int i = 0; i < n; i++)
                loopPaths[i] = FlattenLoop(loops[i]);

            // 计算每个环被多少其他环包含（包含计数）
            // 用边界顶点测试：环 i 的边界顶点是否在环 j 内部
            int[] containmentLevel = new int[n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    if (PointInPath(repPoints[i], loopPaths[j]))
                        containmentLevel[i]++;
                }
            }

            // 偶数层（0, 2, 4...）= 外轮廓，奇数层（1, 3, 5...）= 孔洞
            // 边界顶点法可靠：大环边界点不在小环内 → 层级 0 → 外轮廓
            //                  小环边界点在大环内 → 层级 1 → 孔洞
            outerIndices = new List<int>();
            List<int> holeIndices = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (containmentLevel[i] % 2 == 0)
                    outerIndices.Add(i);
                else
                    holeIndices.Add(i);
            }

            // 为每个外轮廓分配它的孔洞
            holeIndicesPerOuter = new List<List<int>>();
            for (int o = 0; o < outerIndices.Count; o++)
            {
                List<int> holesForThis = new List<int>();
                int outerIdx = outerIndices[o];

                foreach (int hi in holeIndices)
                {
                    // 孔洞必须在此外轮廓内
                    if (!PointInPath(repPoints[hi], loopPaths[outerIdx]))
                        continue;

                    // 且中间没有其他外轮廓（即这个孔洞直接属于此外轮廓）
                    bool hasIntermediateOuter = false;
                    for (int k = 0; k < outerIndices.Count; k++)
                    {
                        if (k == o) continue;
                        int otherOuter = outerIndices[k];
                        if (PointInPath(repPoints[hi], loopPaths[otherOuter]) &&
                            PointInPath(repPoints[otherOuter], loopPaths[outerIdx]))
                        {
                            hasIntermediateOuter = true;
                            break;
                        }
                    }
                    if (!hasIntermediateOuter)
                        holesForThis.Add(hi);
                }
                holeIndicesPerOuter.Add(holesForThis);
            }
        }

        // 获取环的代表点（用于包含测试）
        // 关键：用边界顶点而非重心。对于同心圆（A 包含 B），重心都接近圆心，
        // 导致两者重心都落在对方内部 → 全被误判为孔洞 → 无外轮廓 → 返回 null。
        // 边界顶点只在自己所属环上：大环边界点不在小环内 → 外轮廓；
        // 小环边界点在大环内 → 孔洞。判断正确。
        private static PointF GetLoopRepresentativePoint(List<SubEdge> loop)
        {
            if (loop == null || loop.Count == 0) return PointF.Empty;
            // 用第一条边的起点（边界顶点）
            return loop[0].NodeStart;
        }

        // 将环扁平化为多边形
        private static FlatPath FlattenLoop(List<SubEdge> loop)
        {
            FlatPath fp = new FlatPath();
            List<PointF> pts = new List<PointF>();
            foreach (SubEdge se in loop)
            {
                if (!se.IsCurve)
                {
                    if (pts.Count == 0 || Dist(pts[pts.Count - 1], se.NodeStart) > 0.01f)
                        pts.Add(se.NodeStart);
                }
                else
                {
                    List<PointF> segPts = new List<PointF>();
                    FlattenBezierSimple(se.Seg, segPts, 0);
                    for (int k = 0; k < segPts.Count; k++)
                    {
                        if (pts.Count > 0 && Dist(pts[pts.Count - 1], segPts[k]) < 0.01f) continue;
                        pts.Add(segPts[k]);
                    }
                }
            }
            // 去重首尾
            if (pts.Count > 1 && Dist(pts[0], pts[pts.Count - 1]) < 0.5f)
                pts.RemoveAt(pts.Count - 1);
            fp.Points = pts.ToArray();
            return fp;
        }

        // ====================================================================
        // Step 7: 创建 CurveVertex 列表
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
        // 辅助：有向面积
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
