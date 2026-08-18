using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 曲线感知的布尔合并工具。
    /// 在路径相交点生成新的贝塞尔控制点以拟合原始曲率，
    /// 同时保留非相交段的原始曲线数据。
    /// </summary>
    public static class CurveAwareMerge
    {
        // ====================================================================
        // 数据结构
        // ====================================================================

        /// <summary>三次贝塞尔段（4 个控制点）</summary>
        private struct BezierSeg
        {
            public PointF P0, C1, C2, P3;

            public BezierSeg(PointF p0, PointF c1, PointF c2, PointF p3)
            {
                P0 = p0; C1 = c1; C2 = c2; P3 = p3;
            }
        }

        /// <summary>De Casteljau 分割结果（左右两段）</summary>
        private struct SplitResult
        {
            public BezierSeg Left, Right;
        }

        /// <summary>扁平化边（可能来自直线或贝塞尔曲线，附带原始曲线元数据）</summary>
        private class FlatEdge
        {
            public PointF P1, P2;
            public bool IsCurve;
            public BezierSeg Bezier;      // 曲线时的原始控制点
            public int PathIndex;
            public int EdgeIndex;         // 原始路径中的边索引
            public float OrigT1, OrigT2;  // 在原始贝塞尔边上的参数范围 [0,1]
        }

        /// <summary>两条边的交点</summary>
        private class Crossing
        {
            public PointF Position;
            public int PathIdx1, EdgeIdx1;
            public float T1;              // 在边1上的参数 [0,1]
            public int PathIdx2, EdgeIdx2;
            public float T2;              // 在边2上的参数 [0,1]
        }

        /// <summary>分割后的子边</summary>
        private class SubEdge
        {
            public BezierSeg Seg;         // Start=Seg.P0, End=Seg.P3
            public bool IsCurve;
            public int SourcePathIndex;
            public int SourceEdgeIndex;
            public float OrigT1, OrigT2;  // 在原始贝塞尔边上的参数范围
            public bool StartIsCrossing;
            public bool EndIsCrossing;
            public bool IsBoundary;
            public bool Used;             // 链接时标记已用

            public PointF Start { get { return Seg.P0; } }
            public PointF End { get { return Seg.P3; } }
        }

        // ====================================================================
        // 主方法
        // ====================================================================

        /// <summary>
        /// 执行曲线感知的布尔合并。
        /// 返回合并后的 CurveVertex 列表（含贝塞尔句柄），如果无法处理则返回 null。
        /// </summary>
        public static List<CurveVertex> Merge(
            List<List<CurveVertex>> paths,
            List<BooleanOperation> boolOps,
            Region resultRegion,
            float boundsMinX, float boundsMinY,
            float boundsMaxX, float boundsMaxY)
        {
            // Step 1: 扁平化所有路径
            List<List<FlatEdge>> allFlatEdges = new List<List<FlatEdge>>();
            for (int i = 0; i < paths.Count; i++)
            {
                if (paths[i] != null && paths[i].Count >= 3)
                    allFlatEdges.Add(FlattenPath(paths[i], i));
                else
                    allFlatEdges.Add(new List<FlatEdge>());
            }

            // Step 2: 查找不同路径之间的所有交点
            List<Crossing> allCrossings = new List<Crossing>();
            for (int i = 0; i < allFlatEdges.Count; i++)
            {
                for (int j = i + 1; j < allFlatEdges.Count; j++)
                {
                    if (allFlatEdges[i].Count == 0 || allFlatEdges[j].Count == 0)
                        continue;
                    FindIntersections(allFlatEdges[i], i, allFlatEdges[j], j, allCrossings);
                }
            }

            // 无交点时返回 null（调用方回退到位图追踪）
            if (allCrossings.Count == 0)
                return null;

            // Step 3: 在交点处分割边
            List<SubEdge> allSubEdges = new List<SubEdge>();
            SplitEdgesAtCrossings(allFlatEdges, allCrossings, allSubEdges);

            if (allSubEdges.Count == 0)
                return null;

            // Step 4: 测试哪些子边在结果边界上
            using (Bitmap testBmp = new Bitmap(
                Math.Max(1, (int)(boundsMaxX - boundsMinX + 20)),
                Math.Max(1, (int)(boundsMaxY - boundsMinY + 20))))
            using (Graphics testG = Graphics.FromImage(testBmp))
            {
                foreach (SubEdge se in allSubEdges)
                    se.IsBoundary = TestBoundary(se, resultRegion, testG);
            }

            // 收集边界子边
            List<SubEdge> boundaryEdges = new List<SubEdge>();
            foreach (SubEdge se in allSubEdges)
                if (se.IsBoundary) boundaryEdges.Add(se);

            if (boundaryEdges.Count == 0)
                return null;

            // Step 5: 链接边界子边形成闭合路径
            List<SubEdge> chained = ChainBoundaryEdges(boundaryEdges);
            if (chained == null || chained.Count < 3)
                return null;

            // Step 6: 创建 CurveVertex 列表（含贝塞尔句柄）
            return CreateVerticesFromChained(chained);
        }

        // ====================================================================
        // Step 1: 扁平化路径
        // ====================================================================

        private static List<FlatEdge> FlattenPath(List<CurveVertex> verts, int pathIndex)
        {
            List<FlatEdge> edges = new List<FlatEdge>();
            int n = verts.Count;
            if (n < 2) return edges;

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                CurveVertex v1 = verts[i];
                CurveVertex v2 = verts[next];

                bool hasCurve = v1.Handle != HandleType.None || v2.Handle != HandleType.None;

                if (!hasCurve)
                {
                    edges.Add(new FlatEdge
                    {
                        P1 = v1.Position,
                        P2 = v2.Position,
                        IsCurve = false,
                        Bezier = new BezierSeg(v1.Position, v1.Position, v2.Position, v2.Position),
                        PathIndex = pathIndex,
                        EdgeIndex = i,
                        OrigT1 = 0f,
                        OrigT2 = 1f
                    });
                }
                else
                {
                    PointF c1 = v1.Handle != HandleType.None ? v1.GetHandleOutAbs() : v1.Position;
                    PointF c2 = v2.Handle != HandleType.None ? v2.GetHandleInAbs() : v2.Position;
                    SubdivideBezier(new BezierSeg(v1.Position, c1, c2, v2.Position),
                        0f, 1f, pathIndex, i, edges, 0);
                }
            }
            return edges;
        }

        /// <summary>递归细分贝塞尔曲线直到足够平坦</summary>
        private static void SubdivideBezier(
            BezierSeg bez, float t1, float t2,
            int pathIndex, int edgeIndex,
            List<FlatEdge> edges, int depth)
        {
            float chordLen = Dist(bez.P0, bez.P3);
            float d1 = PointToLineDist(bez.C1, bez.P0, bez.P3);
            float d2 = PointToLineDist(bez.C2, bez.P0, bez.P3);

            if (depth > 12 || (d1 < 1.0f && d2 < 1.0f) || chordLen < 3.0f)
            {
                edges.Add(new FlatEdge
                {
                    P1 = bez.P0,
                    P2 = bez.P3,
                    IsCurve = true,
                    Bezier = bez,
                    PathIndex = pathIndex,
                    EdgeIndex = edgeIndex,
                    OrigT1 = t1,
                    OrigT2 = t2
                });
                return;
            }

            float midT = (t1 + t2) * 0.5f;
            SplitResult sr = DeCasteljauSplit(bez, 0.5f);
            SubdivideBezier(sr.Left, t1, midT, pathIndex, edgeIndex, edges, depth + 1);
            SubdivideBezier(sr.Right, midT, t2, pathIndex, edgeIndex, edges, depth + 1);
        }

        // ====================================================================
        // Step 2: 查找交点
        // ====================================================================

        private static void FindIntersections(
            List<FlatEdge> edges1, int pathIdx1,
            List<FlatEdge> edges2, int pathIdx2,
            List<Crossing> results)
        {
            for (int i1 = 0; i1 < edges1.Count; i1++)
            {
                FlatEdge e1 = edges1[i1];
                for (int i2 = 0; i2 < edges2.Count; i2++)
                {
                    FlatEdge e2 = edges2[i2];
                    float t, s;
                    PointF? ip = LineLineIntersect(e1.P1, e1.P2, e2.P1, e2.P2, out t, out s);
                    if (ip.HasValue && t > 0.001f && t < 0.999f && s > 0.001f && s < 0.999f)
                    {
                        results.Add(new Crossing
                        {
                            Position = ip.Value,
                            PathIdx1 = pathIdx1, EdgeIdx1 = i1, T1 = t,
                            PathIdx2 = pathIdx2, EdgeIdx2 = i2, T2 = s
                        });
                    }
                }
            }
        }

        /// <summary>线段-线段交点计算</summary>
        private static PointF? LineLineIntersect(PointF p1, PointF p2, PointF p3, PointF p4, out float t, out float s)
        {
            t = 0; s = 0;
            float dx1 = p2.X - p1.X, dy1 = p2.Y - p1.Y;
            float dx2 = p4.X - p3.X, dy2 = p4.Y - p3.Y;

            float denom = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(denom) < 1e-9f)
                return null;

            float dx3 = p3.X - p1.X, dy3 = p3.Y - p1.Y;
            t = (dx3 * dy2 - dy3 * dx2) / denom;
            s = (dx3 * dy1 - dy3 * dx1) / denom;

            if (t < -0.001f || t > 1.001f || s < -0.001f || s > 1.001f)
                return null;

            return new PointF(p1.X + t * dx1, p1.Y + t * dy1);
        }

        // ====================================================================
        // Step 3: 在交点处分割边
        // ====================================================================

        private static void SplitEdgesAtCrossings(
            List<List<FlatEdge>> allFlatEdges,
            List<Crossing> allCrossings,
            List<SubEdge> results)
        {
            // 为每条扁平边收集其上的交点：key = "pathIdx_edgeIdx"
            Dictionary<string, List<KeyValuePair<float, PointF>>> edgeSplits =
                new Dictionary<string, List<KeyValuePair<float, PointF>>>();

            foreach (Crossing c in allCrossings)
            {
                RegisterSplit(edgeSplits, c.PathIdx1, c.EdgeIdx1, c.T1, c.Position);
                RegisterSplit(edgeSplits, c.PathIdx2, c.EdgeIdx2, c.T2, c.Position);
            }

            for (int pi = 0; pi < allFlatEdges.Count; pi++)
            {
                for (int ei = 0; ei < allFlatEdges[pi].Count; ei++)
                {
                    FlatEdge fe = allFlatEdges[pi][ei];
                    string key = pi + "_" + ei;

                    List<KeyValuePair<float, PointF>> splits;
                    if (!edgeSplits.TryGetValue(key, out splits))
                    {
                        // 无分割点，整条边作为一个子边
                        results.Add(CreateSubEdge(fe, 0f, 1f, false, false));
                        continue;
                    }

                    // 按参数排序
                    splits.Sort((a, b) => a.Key.CompareTo(b.Key));

                    float prevT = 0f;
                    for (int si = 0; si < splits.Count; si++)
                    {
                        float splitT = splits[si].Key;
                        bool startCross = (si > 0);
                        results.Add(CreateSubEdge(fe, prevT, splitT, startCross, true));
                        prevT = splitT;
                    }
                    // 最后一段
                    results.Add(CreateSubEdge(fe, prevT, 1f, true, false));
                }
            }
        }

        private static void RegisterSplit(
            Dictionary<string, List<KeyValuePair<float, PointF>>> dict,
            int pathIdx, int edgeIdx, float t, PointF pos)
        {
            string key = pathIdx + "_" + edgeIdx;
            if (!dict.ContainsKey(key))
                dict[key] = new List<KeyValuePair<float, PointF>>();
            dict[key].Add(new KeyValuePair<float, PointF>(t, pos));
        }

        /// <summary>从扁平边的参数区间 [t1,t2] 创建子边</summary>
        private static SubEdge CreateSubEdge(FlatEdge fe, float t1, float t2, bool startCross, bool endCross)
        {
            SubEdge se = new SubEdge();
            se.IsCurve = fe.IsCurve;
            se.SourcePathIndex = fe.PathIndex;
            se.SourceEdgeIndex = fe.EdgeIndex;
            se.OrigT1 = fe.OrigT1 + (fe.OrigT2 - fe.OrigT1) * t1;
            se.OrigT2 = fe.OrigT1 + (fe.OrigT2 - fe.OrigT1) * t2;
            se.StartIsCrossing = startCross;
            se.EndIsCrossing = endCross;

            if (fe.IsCurve)
                se.Seg = CubicBezierSubsegment(fe.Bezier, t1, t2);
            else
                se.Seg = new BezierSeg(fe.P1, fe.P1, fe.P2, fe.P2);

            return se;
        }

        // ====================================================================
        // 贝塞尔分割核心
        // ====================================================================

        /// <summary>获取三次贝塞尔曲线在 [t1,t2] 子段的 BezierSeg</summary>
        private static BezierSeg CubicBezierSubsegment(BezierSeg bez, float t1, float t2)
        {
            if (t1 <= 0.001f && t2 >= 0.999f)
                return bez;

            // 先在 t1 处分割取右半，再在重映射后的 t2 处分割取左半
            SplitResult first = DeCasteljauSplit(bez, t1);
            float denom = 1f - t1;
            if (denom < 0.001f) denom = 0.001f;
            float rt = (t2 - t1) / denom;
            if (rt > 0.999f) rt = 0.999f;
            if (rt < 0.001f) rt = 0.001f;
            SplitResult second = DeCasteljauSplit(first.Right, rt);
            return second.Left;
        }

        /// <summary>De Casteljau 分割：在参数 t 处将贝塞尔段分为左右两段</summary>
        private static SplitResult DeCasteljauSplit(BezierSeg bez, float t)
        {
            PointF p0 = bez.P0, c1 = bez.C1, c2 = bez.C2, p3 = bez.P3;
            PointF m01 = Lerp(p0, c1, t);
            PointF m12 = Lerp(c1, c2, t);
            PointF m23 = Lerp(c2, p3, t);
            PointF m012 = Lerp(m01, m12, t);
            PointF m123 = Lerp(m12, m23, t);
            PointF m = Lerp(m012, m123, t);

            return new SplitResult
            {
                Left = new BezierSeg(p0, m01, m012, m),
                Right = new BezierSeg(m, m123, m23, p3)
            };
        }

        // ====================================================================
        // Step 4: 边界测试
        // ====================================================================

        private static bool TestBoundary(SubEdge se, Region resultRegion, Graphics g)
        {
            PointF mid = MidPoint(se.Seg.P0, se.Seg.P3);
            float dx = se.Seg.P3.X - se.Seg.P0.X;
            float dy = se.Seg.P3.Y - se.Seg.P0.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return false;

            // 垂直向量（左侧）
            float px = -dy / len, py = dx / len;

            // 先用 1.5px 偏移测试，再用 3px 测试
            for (int i = 0; i < 2; i++)
            {
                float offset = (i == 0) ? 1.5f : 3.0f;
                PointF left = new PointF(mid.X + px * offset, mid.Y + py * offset);
                PointF right = new PointF(mid.X - px * offset, mid.Y - py * offset);
                if (resultRegion.IsVisible(left, g) != resultRegion.IsVisible(right, g))
                    return true;
            }
            return false;
        }

        // ====================================================================
        // Step 5: 链接边界子边
        // ====================================================================

        private static List<SubEdge> ChainBoundaryEdges(List<SubEdge> boundaryEdges)
        {
            if (boundaryEdges.Count == 0) return null;

            List<SubEdge> chained = new List<SubEdge>();
            const float matchTol = 2.5f;

            SubEdge current = boundaryEdges[0];
            current.Used = true;
            chained.Add(current);
            PointF chainStart = current.Start;
            PointF currentEnd = current.End;

            int maxIter = boundaryEdges.Count * 2 + 10;
            while (maxIter-- > 0)
            {
                if (Dist(currentEnd, chainStart) < matchTol)
                    break;

                SubEdge bestEdge = null;
                float bestDist = float.MaxValue;
                bool bestReversed = false;

                foreach (SubEdge se in boundaryEdges)
                {
                    if (se.Used) continue;

                    float dFwd = Dist(se.Start, currentEnd);
                    if (dFwd < bestDist) { bestDist = dFwd; bestEdge = se; bestReversed = false; }

                    float dRev = Dist(se.End, currentEnd);
                    if (dRev < bestDist) { bestDist = dRev; bestEdge = se; bestReversed = true; }
                }

                if (bestEdge == null || bestDist > matchTol)
                    break;

                bestEdge.Used = true;
                if (bestReversed)
                {
                    SubEdge reversed = ReverseSubEdge(bestEdge);
                    chained.Add(reversed);
                    currentEnd = reversed.End;
                }
                else
                {
                    chained.Add(bestEdge);
                    currentEnd = bestEdge.End;
                }
            }

            return chained.Count >= 3 ? chained : null;
        }

        private static SubEdge ReverseSubEdge(SubEdge se)
        {
            SubEdge r = new SubEdge();
            r.IsCurve = se.IsCurve;
            r.Seg = new BezierSeg(se.Seg.P3, se.Seg.C2, se.Seg.C1, se.Seg.P0);
            r.SourcePathIndex = se.SourcePathIndex;
            r.SourceEdgeIndex = se.SourceEdgeIndex;
            r.OrigT1 = se.OrigT2;
            r.OrigT2 = se.OrigT1;
            r.StartIsCrossing = se.EndIsCrossing;
            r.EndIsCrossing = se.StartIsCrossing;
            r.IsBoundary = se.IsBoundary;
            r.Used = se.Used;
            return r;
        }

        // ====================================================================
        // Step 6: 创建 CurveVertex 列表（含贝塞尔句柄）
        // ====================================================================

        private static List<CurveVertex> CreateVerticesFromChained(List<SubEdge> chained)
        {
            List<CurveVertex> result = new List<CurveVertex>();

            for (int i = 0; i < chained.Count; i++)
            {
                SubEdge se = chained[i];
                SubEdge prev = chained[(i - 1 + chained.Count) % chained.Count];

                PointF pos = se.Start;
                bool isCrossing = se.StartIsCrossing;

                // 进入方向 = 前一条边在终点附近的切线
                PointF inDir = prev.IsCurve
                    ? BezierTangent(prev.Seg, 0.95f)
                    : Direction(prev.Start, prev.End);

                // 离开方向 = 当前边在起点附近的切线
                PointF outDir = se.IsCurve
                    ? BezierTangent(se.Seg, 0.05f)
                    : Direction(se.Start, se.End);

                // 句柄长度：基于相邻边长度的 1/3
                float avgLen = (Dist(prev.Start, prev.End) + Dist(se.Start, se.End)) * 0.5f;
                float handleLen = Clamp(avgLen / 3f, 5f, 60f);

                if (isCrossing)
                    CreateVertexWithHandles(result, pos, inDir, outDir, handleLen);
                else if (se.IsCurve)
                    CreateVertexWithTangent(result, pos, outDir, handleLen);
                else
                    result.Add(new CurveVertex(pos));
            }

            return result;
        }

        /// <summary>在交点处创建带贝塞尔句柄的顶点</summary>
        private static void CreateVertexWithHandles(
            List<CurveVertex> result, PointF pos,
            PointF inDir, PointF outDir, float handleLen)
        {
            PointF inNorm = Normalize(inDir);
            PointF outNorm = Normalize(outDir);

            float dot = inNorm.X * outNorm.X + inNorm.Y * outNorm.Y;
            float angle = (float)Math.Acos(Math.Max(-1f, Math.Min(1f, dot)));

            if (angle < 0.15f) // ~8.6°，几乎共线 → 对称句柄
            {
                PointF handle = Scale(outNorm, handleLen);
                result.Add(new CurveVertex(pos, HandleType.Symmetric, Negate(handle), handle));
            }
            else if (angle > 2.5f) // ~143°，接近锐角 → 尖角无句柄
            {
                result.Add(new CurveVertex(pos));
            }
            else // 中间角度 → 独立句柄
            {
                PointF handleOut = Scale(outNorm, handleLen);
                PointF handleIn = Negate(Scale(inNorm, handleLen));
                result.Add(new CurveVertex(pos, HandleType.Asymmetric, handleIn, handleOut));
            }
        }

        /// <summary>用切线方向创建对称句柄</summary>
        private static void CreateVertexWithTangent(
            List<CurveVertex> result, PointF pos, PointF tangent, float handleLen)
        {
            PointF tanNorm = Normalize(tangent);
            if (Math.Abs(tanNorm.X) < 0.001f && Math.Abs(tanNorm.Y) < 0.001f)
            {
                result.Add(new CurveVertex(pos));
                return;
            }
            PointF handle = Scale(tanNorm, handleLen);
            result.Add(new CurveVertex(pos, HandleType.Symmetric, Negate(handle), handle));
        }

        // ====================================================================
        // 贝塞尔切线计算
        // ====================================================================

        /// <summary>计算三次贝塞尔曲线在参数 t 处的切线方向</summary>
        private static PointF BezierTangent(BezierSeg bez, float t)
        {
            float u = 1f - t;
            // B'(t) = 3(1-t)²(C1-P0) + 6(1-t)t(C2-C1) + 3t²(P3-C2)
            float x = 3 * u * u * (bez.C1.X - bez.P0.X) + 6 * u * t * (bez.C2.X - bez.C1.X) + 3 * t * t * (bez.P3.X - bez.C2.X);
            float y = 3 * u * u * (bez.C1.Y - bez.P0.Y) + 6 * u * t * (bez.C2.Y - bez.C1.Y) + 3 * t * t * (bez.P3.Y - bez.C2.Y);
            return new PointF(x, y);
        }

        // ====================================================================
        // 数学工具方法
        // ====================================================================

        private static float Dist(PointF a, PointF b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static PointF Lerp(PointF a, PointF b, float t)
        {
            return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        private static PointF MidPoint(PointF a, PointF b)
        {
            return new PointF((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f);
        }

        private static PointF Direction(PointF from, PointF to)
        {
            return new PointF(to.X - from.X, to.Y - from.Y);
        }

        private static PointF Normalize(PointF v)
        {
            float len = (float)Math.Sqrt(v.X * v.X + v.Y * v.Y);
            if (len < 1e-9f) return new PointF(0, 0);
            return new PointF(v.X / len, v.Y / len);
        }

        private static PointF Scale(PointF v, float s)
        {
            return new PointF(v.X * s, v.Y * s);
        }

        private static PointF Negate(PointF v)
        {
            return new PointF(-v.X, -v.Y);
        }

        private static float Clamp(float val, float min, float max)
        {
            return val < min ? min : (val > max ? max : val);
        }

        private static float PointToLineDist(PointF p, PointF a, PointF b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-9f) return Dist(p, a);
            float t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq;
            t = Clamp(t, 0f, 1f);
            return Dist(p, new PointF(a.X + t * dx, a.Y + t * dy));
        }
    }
}
