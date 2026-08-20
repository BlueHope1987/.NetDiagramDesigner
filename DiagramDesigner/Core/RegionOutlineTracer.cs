using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 从 Region 提取轮廓 GraphicsPath 的工具。
    /// 使用 GetRegionScans 获取非重叠矩形，提取唯一边界边，链接成闭合轮廓。
    /// 支持超采样以提高曲线/斜边质量，自动处理孔洞。
    /// </summary>
    public static class RegionOutlineTracer
    {
        // ====================================================================
        // 公开方法
        // ====================================================================

        /// <summary>
        /// 追踪 Region 的轮廓，返回 GraphicsPath（可能含多个子路径：外轮廓 + 孔洞）。
        /// supersample 控制超采样倍数，值越大轮廓越平滑。
        /// </summary>
        public static GraphicsPath TraceOutline(Region region, float supersample)
        {
            GraphicsPath result = new GraphicsPath();
            if (region == null)
                return result;

            // 克隆并缩放 Region 以提高分辨率
            // 注意：不调用 region.IsEmpty(null)，因为 Mono 下需要 Graphics 上下文，
            // 传入 null 会抛出 ArgumentNullException；GetRegionScans 返回空数组即可处理空 Region
            Region scaled = region.Clone();
            try
            {
                using (Matrix scaleMatrix = new Matrix())
                {
                    scaleMatrix.Scale(supersample, supersample);
                    scaled.Transform(scaleMatrix);
                }

                RectangleF[] scans;
                using (Matrix identity = new Matrix())
                    scans = scaled.GetRegionScans(identity);

                if (scans.Length == 0)
                    return result;

                // 收集所有有向边，统计出现次数
                Dictionary<string, int> edgeCount = new Dictionary<string, int>();
                Dictionary<string, float[]> edgeDir = new Dictionary<string, float[]>();

                foreach (RectangleF r in scans)
                {
                    // 4 条顺时针有向边
                    AddEdge(edgeCount, edgeDir, r.X, r.Y, r.Right, r.Y);           // 上 左→右
                    AddEdge(edgeCount, edgeDir, r.Right, r.Y, r.Right, r.Bottom);  // 右 上→下
                    AddEdge(edgeCount, edgeDir, r.Right, r.Bottom, r.X, r.Bottom); // 下 右→左
                    AddEdge(edgeCount, edgeDir, r.X, r.Bottom, r.X, r.Y);          // 左 下→上
                }

                // 收集边界边（仅出现一次）
                float invScale = 1f / supersample;
                Dictionary<string, List<float[]>> adjacency = new Dictionary<string, List<float[]>>();

                foreach (KeyValuePair<string, int> kv in edgeCount)
                {
                    if (kv.Value != 1)
                        continue;
                    float[] dir = edgeDir[kv.Key];
                    // 缩放回原始坐标
                    float x1 = dir[0] * invScale, y1 = dir[1] * invScale;
                    float x2 = dir[2] * invScale, y2 = dir[3] * invScale;
                    string startKey = KeyFor(x1, y1);

                    if (!adjacency.ContainsKey(startKey))
                        adjacency[startKey] = new List<float[]>();
                    adjacency[startKey].Add(new float[] { x1, y1, x2, y2 });
                }

                // 链接边界边为闭合轮廓
                Dictionary<string, bool> used = new Dictionary<string, bool>();

                foreach (string startKey in new List<string>(adjacency.Keys))
                {
                    if (used.ContainsKey(startKey))
                        continue;

                    List<PointF> contour = ChainContour(adjacency, startKey, used);
                    if (contour.Count >= 3)
                    {
                        // RDP 简化
                        List<PointF> simplified = SimplifyClosed(contour, invScale);
                        if (simplified.Count >= 3)
                        {
                            result.AddLines(simplified.ToArray());
                            result.CloseFigure();
                        }
                    }
                }
            }
            finally
            {
                scaled.Dispose();
            }

            return result;
        }

        /// <summary>
        /// 创建 Region 边界附近的环形裁剪区域（用于布尔运算结果的描边）。
        /// 原理：ring = dilate(R, d) \ erode(R, d)，只保留边界附近宽度约 2d 的环形区域。
        /// 配合 SetClip(ring) + DrawPath(原始路径)：外轮廓附近的路径段落入环内被绘制，
        /// 内部共享边远离边界落入 erode 区域被裁掉，从而实现统一描边。
        /// 使用 8 连通（4 正交 + 4 对角）平移以改善角落覆盖。
        /// </summary>
        public static Region CreateStrokeClipRing(Region region, float distance)
        {
            // 8 方向偏移：4 正交 + 4 对角
            float[] offsets = {
                distance, 0f,           // 右
                -distance, 0f,         // 左
                0f, distance,          // 下
                0f, -distance,         // 上
                distance, distance,    // 右下
                distance, -distance,   // 右上
                -distance, distance,    // 左下
                -distance, -distance   // 左上
            };

            // 膨胀：dilated = R ∪ translate(R, d0) ∪ ... ∪ translate(R, d7)
            Region dilated = region.Clone();
            // 侵蚀：eroded = R ∩ translate(R, d0) ∩ ... ∩ translate(R, d7)
            Region eroded = region.Clone();

            for (int i = 0; i < offsets.Length; i += 2)
            {
                using (Matrix m = new Matrix(1f, 0f, 0f, 1f, offsets[i], offsets[i + 1]))
                {
                    // 膨胀
                    Region tempD = region.Clone();
                    tempD.Transform(m);
                    dilated.Union(tempD);
                    tempD.Dispose();

                    // 侵蚀
                    Region tempE = region.Clone();
                    tempE.Transform(m);
                    eroded.Intersect(tempE);
                    tempE.Dispose();
                }
            }

            // 环 = 膨胀区域 减去 侵蚀区域
            dilated.Exclude(eroded);
            eroded.Dispose();
            return dilated;
        }

        /// <summary>
        /// 创建 Region 内部边界附近的裁剪区域（用于混合运算组的描边）。
        /// 原理：innerClip = R \ erode(R, d)，只保留 R 内部靠近边界的环形区域。
        /// 与 CreateStrokeClipRing 的区别：不包含 R 外部的环带，避免画出外部边。
        /// 适用于混合运算组（如 Subtract+Union、Xor+Union 等），
        /// 直接用最终 Region 确定边界，不依赖运算类型推断。
        /// </summary>
        public static Region CreateInnerStrokeClip(Region region, float distance)
        {
            float[] offsets = {
                distance, 0f,           // 右
                -distance, 0f,         // 左
                0f, distance,          // 下
                0f, -distance,         // 上
                distance, distance,    // 右下
                distance, -distance,   // 右上
                -distance, distance,    // 左下
                -distance, -distance   // 左上
            };

            // 侵蚀：eroded = R ∩ translate(R, d0) ∩ ... ∩ translate(R, d7)
            Region eroded = region.Clone();

            for (int i = 0; i < offsets.Length; i += 2)
            {
                using (Matrix m = new Matrix(1f, 0f, 0f, 1f, offsets[i], offsets[i + 1]))
                {
                    Region temp = region.Clone();
                    temp.Transform(m);
                    eroded.Intersect(temp);
                    temp.Dispose();
                }
            }

            // 内裁剪环 = R \ erode(R, d)
            Region result = region.Clone();
            result.Exclude(eroded);
            eroded.Dispose();
            return result;
        }

        // ====================================================================
        // 方案B：精确逐路径裁剪描边（纯角色驱动）
        // ====================================================================

        /// <summary>
        /// 对已修改组进行精确逐路径裁剪描边。
        /// 纯运算组（所有运算类型相同）用精确裁剪，裁剪边界恰好经过路径交汇点，不留线头。
        /// 混合运算组（如 Subtract+Union、Xor+Union）用内裁剪环 R\erode(R,d)，
        /// 直接用最终 Region 确定边界，不依赖运算类型推断。
        /// </summary>
        public static void StrokeModifiedGroup(
            Graphics g, Pen pen, float erodeDist,
            List<GraphicsPath> allPaths,
            List<int> groupPathIndices,
            List<int> groupRoles,
            Region groupRegion,
            bool allUnion,
            bool allXor)
        {
            int count = groupPathIndices.Count;
            if (count == 0) return;

            // 单路径直接描边
            if (count == 1)
            {
                DrawPathSafe(g, pen, allPaths, groupPathIndices[0]);
                return;
            }

            // 统计角色类型
            bool hasRole0 = false, hasRole1 = false, hasRole2 = false;
            for (int k = 0; k < groupRoles.Count; k++)
            {
                if (groupRoles[k] == 0) hasRole0 = true;
                else if (groupRoles[k] == 1) hasRole1 = true;
                else if (groupRoles[k] == 2) hasRole2 = true;
            }

            // 检查是否只有首条路径为 role 0（纯 Subtract/Intersect 的必要条件）
            bool onlyFirstIsRole0 = groupRoles.Count > 0 && groupRoles[0] == 0;
            for (int k = 1; k < groupRoles.Count; k++)
                if (groupRoles[k] == 0) onlyFirstIsRole0 = false;

            // === Xor：全部路径完整描边（外轮廓+孔洞边界） ===
            if (allXor && hasRole0 && !hasRole1 && !hasRole2)
            {
                for (int k = 0; k < count; k++)
                    DrawPathSafe(g, pen, allPaths, groupPathIndices[k]);
                return;
            }

            // === 纯 Union：每条路径描边在"其余路径并集"之外的部分 ===
            if (allUnion && hasRole0 && !hasRole1 && !hasRole2)
            {
                for (int k = 0; k < count; k++)
                {
                    GraphicsState state = g.Save();
                    Region others = BuildUnionExcept(allPaths, groupPathIndices, k);
                    if (others != null)
                    {
                        g.SetClip(others, CombineMode.Exclude);
                        others.Dispose();
                    }
                    DrawPathSafe(g, pen, allPaths, groupPathIndices[k]);
                    g.Restore(state);
                }
                return;
            }

            // === 纯 Subtract (role 0 + role 1，只有首条为 role 0)：基底并集被减去路径切割 ===
            if (hasRole0 && hasRole1 && !hasRole2 && onlyFirstIsRole0)
            {
                Region baseUnion = BuildUnionByRole(allPaths, groupPathIndices, groupRoles, 0);

                for (int k = 0; k < count; k++)
                {
                    int idx = groupPathIndices[k];
                    if (idx < 0 || idx >= allPaths.Count || allPaths[idx] == null) continue;

                    GraphicsState state = g.Save();

                    if (groupRoles[k] == 0)
                    {
                        // 基底路径：描边在"所有其余路径并集"之外
                        // （其余基底=并集重叠，减去路径=切割孔）
                        Region others = BuildUnionExcept(allPaths, groupPathIndices, k);
                        if (others != null)
                        {
                            g.SetClip(others, CombineMode.Exclude);
                            others.Dispose();
                        }
                    }
                    else
                    {
                        // 减去路径：描边在"基底并集"内、"其余减去路径并集"外
                        // （形成切割孔的边界）
                        if (baseUnion != null)
                            g.SetClip(baseUnion, CombineMode.Replace);
                        Region otherNegs = BuildUnionExceptByRole(allPaths, groupPathIndices, groupRoles, 1, k);
                        if (otherNegs != null)
                        {
                            g.SetClip(otherNegs, CombineMode.Exclude);
                            otherNegs.Dispose();
                        }
                    }

                    g.DrawPath(pen, allPaths[idx]);
                    g.Restore(state);
                }

                if (baseUnion != null) baseUnion.Dispose();
                return;
            }

            // === 纯 Intersect (role 0 + role 2，只有首条为 role 0)：基底并集与约束路径求交 ===
            if (hasRole0 && !hasRole1 && hasRole2 && onlyFirstIsRole0)
            {
                Region baseUnion = BuildUnionByRole(allPaths, groupPathIndices, groupRoles, 0);
                Region constrIntersect = BuildIntersectByRole(allPaths, groupPathIndices, groupRoles, 2);

                for (int k = 0; k < count; k++)
                {
                    int idx = groupPathIndices[k];
                    if (idx < 0 || idx >= allPaths.Count || allPaths[idx] == null) continue;

                    GraphicsState state = g.Save();

                    if (groupRoles[k] == 0)
                    {
                        // 基底路径：描边在"约束交集"内、"其余基底并集"外
                        if (constrIntersect != null)
                            g.SetClip(constrIntersect, CombineMode.Replace);
                        Region otherBases = BuildUnionExceptByRole(allPaths, groupPathIndices, groupRoles, 0, k);
                        if (otherBases != null)
                        {
                            g.SetClip(otherBases, CombineMode.Exclude);
                            otherBases.Dispose();
                        }
                    }
                    else
                    {
                        // 约束路径：描边在"基底并集"内、"其余约束交集"内
                        if (baseUnion != null)
                            g.SetClip(baseUnion, CombineMode.Replace);
                        Region otherConstr = BuildIntersectExceptByRole(allPaths, groupPathIndices, groupRoles, 2, k);
                        if (otherConstr != null)
                        {
                            g.SetClip(otherConstr, CombineMode.Intersect);
                            otherConstr.Dispose();
                        }
                    }

                    g.DrawPath(pen, allPaths[idx]);
                    g.Restore(state);
                }

                if (baseUnion != null) baseUnion.Dispose();
                if (constrIntersect != null) constrIntersect.Dispose();
                return;
            }

            // === 混合运算组：用内裁剪环 R\erode(R,d) 统一描边 ===
            // 直接用最终 Region 确定边界，不依赖运算类型推断
            using (Region innerClip = CreateInnerStrokeClip(groupRegion, erodeDist))
            {
                GraphicsState state = g.Save();
                g.SetClip(innerClip, CombineMode.Replace);
                for (int k = 0; k < count; k++)
                    DrawPathSafe(g, pen, allPaths, groupPathIndices[k]);
                g.Restore(state);
            }
        }

        // ====================================================================
        // 方案B 辅助方法
        // ====================================================================

        private static void DrawPathSafe(Graphics g, Pen pen, List<GraphicsPath> allPaths, int idx)
        {
            if (idx >= 0 && idx < allPaths.Count && allPaths[idx] != null)
                g.DrawPath(pen, allPaths[idx]);
        }

        /// <summary>构建组内除第 exceptK 条外所有路径的并集 Region</summary>
        private static Region BuildUnionExcept(List<GraphicsPath> allPaths, List<int> indices, int exceptK)
        {
            Region result = null;
            for (int m = 0; m < indices.Count; m++)
            {
                if (m == exceptK) continue;
                int idx = indices[m];
                if (idx < 0 || idx >= allPaths.Count || allPaths[idx] == null) continue;
                if (result == null)
                    result = new Region(allPaths[idx]);
                else
                    result.Union(allPaths[idx]);
            }
            return result;
        }

        /// <summary>构建组内指定角色的所有路径的并集 Region</summary>
        private static Region BuildUnionByRole(List<GraphicsPath> allPaths, List<int> indices, List<int> roles, int role)
        {
            Region result = null;
            for (int m = 0; m < indices.Count; m++)
            {
                if (roles[m] != role) continue;
                int idx = indices[m];
                if (idx < 0 || idx >= allPaths.Count || allPaths[idx] == null) continue;
                if (result == null)
                    result = new Region(allPaths[idx]);
                else
                    result.Union(allPaths[idx]);
            }
            return result;
        }

        /// <summary>构建组内指定角色、除第 exceptK 条外所有路径的并集 Region</summary>
        private static Region BuildUnionExceptByRole(List<GraphicsPath> allPaths, List<int> indices, List<int> roles, int role, int exceptK)
        {
            Region result = null;
            for (int m = 0; m < indices.Count; m++)
            {
                if (m == exceptK) continue;
                if (roles[m] != role) continue;
                int idx = indices[m];
                if (idx < 0 || idx >= allPaths.Count || allPaths[idx] == null) continue;
                if (result == null)
                    result = new Region(allPaths[idx]);
                else
                    result.Union(allPaths[idx]);
            }
            return result;
        }

        /// <summary>构建组内指定角色的所有路径的交集 Region</summary>
        private static Region BuildIntersectByRole(List<GraphicsPath> allPaths, List<int> indices, List<int> roles, int role)
        {
            Region result = null;
            for (int m = 0; m < indices.Count; m++)
            {
                if (roles[m] != role) continue;
                int idx = indices[m];
                if (idx < 0 || idx >= allPaths.Count || allPaths[idx] == null) continue;
                if (result == null)
                    result = new Region(allPaths[idx]);
                else
                    result.Intersect(allPaths[idx]);
            }
            return result;
        }

        /// <summary>构建组内指定角色、除第 exceptK 条外所有路径的交集 Region</summary>
        private static Region BuildIntersectExceptByRole(List<GraphicsPath> allPaths, List<int> indices, List<int> roles, int role, int exceptK)
        {
            Region result = null;
            for (int m = 0; m < indices.Count; m++)
            {
                if (m == exceptK) continue;
                if (roles[m] != role) continue;
                int idx = indices[m];
                if (idx < 0 || idx >= allPaths.Count || allPaths[idx] == null) continue;
                if (result == null)
                    result = new Region(allPaths[idx]);
                else
                    result.Intersect(allPaths[idx]);
            }
            return result;
        }

        // ====================================================================
        // 边收集
        // ====================================================================

        private static void AddEdge(
            Dictionary<string, int> edgeCount,
            Dictionary<string, float[]> edgeDir,
            float x1, float y1, float x2, float y2)
        {
            string normKey = NormalizedKey(x1, y1, x2, y2);

            if (edgeCount.ContainsKey(normKey))
            {
                edgeCount[normKey]++;
            }
            else
            {
                edgeCount[normKey] = 1;
                edgeDir[normKey] = new float[] { x1, y1, x2, y2 };
            }
        }

        /// <summary>标准化边 key：始终让较小的端点在前</summary>
        private static string NormalizedKey(float x1, float y1, float x2, float y2)
        {
            if (x1 < x2 || (x1 == x2 && y1 <= y2))
                return KeyFor(x1, y1) + ">" + KeyFor(x2, y2);
            return KeyFor(x2, y2) + ">" + KeyFor(x1, y1);
        }

        /// <summary>点 key：四舍五入到 3 位小数</summary>
        private static string KeyFor(float x, float y)
        {
            return (Math.Round(x, 3)).ToString("F3") + "," + (Math.Round(y, 3)).ToString("F3");
        }

        // ====================================================================
        // 轮廓链接
        // ====================================================================

        private static List<PointF> ChainContour(
            Dictionary<string, List<float[]>> adjacency,
            string startKey,
            Dictionary<string, bool> used)
        {
            List<PointF> contour = new List<PointF>();
            string current = startKey;
            int maxIter = adjacency.Count + 10;

            while (maxIter-- > 0)
            {
                if (!adjacency.ContainsKey(current))
                    break;

                // 找一条从 current 出发的未使用边
                float[] edge = null;
                foreach (float[] e in adjacency[current])
                {
                    string endKey = KeyFor(e[2], e[3]);
                    if (!used.ContainsKey(current + ">" + endKey))
                    {
                        edge = e;
                        break;
                    }
                }

                if (edge == null)
                    break;

                // 标记使用
                used[current + ">" + KeyFor(edge[2], edge[3])] = true;

                if (contour.Count == 0)
                    contour.Add(new PointF(edge[0], edge[1]));
                contour.Add(new PointF(edge[2], edge[3]));

                current = KeyFor(edge[2], edge[3]);

                if (current == startKey)
                    break;
            }

            // 移除闭合时的重复末尾点
            if (contour.Count >= 2)
            {
                PointF first = contour[0];
                PointF last = contour[contour.Count - 1];
                if (Math.Abs(first.X - last.X) < 0.01f && Math.Abs(first.Y - last.Y) < 0.01f)
                    contour.RemoveAt(contour.Count - 1);
            }

            return contour;
        }

        // ====================================================================
        // RDP 简化
        // ====================================================================

        private static List<PointF> SimplifyClosed(List<PointF> points, float tolerance)
        {
            if (points.Count <= 3)
                return points;

            // 先移除连续重复点
            List<PointF> cleaned = new List<PointF>();
            foreach (PointF p in points)
            {
                if (cleaned.Count == 0)
                    cleaned.Add(p);
                else
                {
                    PointF last = cleaned[cleaned.Count - 1];
                    if ((p.X - last.X) * (p.X - last.X) + (p.Y - last.Y) * (p.Y - last.Y) > 0.01f)
                        cleaned.Add(p);
                }
            }
            if (cleaned.Count <= 3)
                return cleaned;

            // 对闭环在首尾处断开，分两段简化
            int mid = cleaned.Count / 2;
            List<PointF> firstHalf = new List<PointF>();
            for (int i = 0; i <= mid; i++)
                firstHalf.Add(cleaned[i]);
            List<PointF> secondHalf = new List<PointF>();
            for (int i = mid; i < cleaned.Count; i++)
                secondHalf.Add(cleaned[i]);
            secondHalf.Add(cleaned[0]);

            List<PointF> simplifiedFirst = RdpSimplify(firstHalf, tolerance);
            List<PointF> simplifiedSecond = RdpSimplify(secondHalf, tolerance);

            List<PointF> result = new List<PointF>();
            for (int i = 0; i < simplifiedFirst.Count - 1; i++)
                result.Add(simplifiedFirst[i]);
            for (int i = 0; i < simplifiedSecond.Count - 1; i++)
                result.Add(simplifiedSecond[i]);

            return result.Count >= 3 ? result : cleaned;
        }

        private static List<PointF> RdpSimplify(List<PointF> points, float tolerance)
        {
            if (points.Count <= 2)
                return new List<PointF>(points);

            float tolSq = tolerance * tolerance;
            PointF first = points[0];
            PointF last = points[points.Count - 1];
            float maxDistSq = 0;
            int maxIdx = 0;

            for (int i = 1; i < points.Count - 1; i++)
            {
                float dSq = PointLineDistSq(points[i], first, last);
                if (dSq > maxDistSq)
                {
                    maxDistSq = dSq;
                    maxIdx = i;
                }
            }

            List<PointF> result = new List<PointF>();
            if (maxDistSq > tolSq)
            {
                List<PointF> left = new List<PointF>();
                for (int i = 0; i <= maxIdx; i++)
                    left.Add(points[i]);
                List<PointF> simplifiedLeft = RdpSimplify(left, tolerance);

                List<PointF> right = new List<PointF>();
                for (int i = maxIdx; i < points.Count; i++)
                    right.Add(points[i]);
                List<PointF> simplifiedRight = RdpSimplify(right, tolerance);

                for (int i = 0; i < simplifiedLeft.Count - 1; i++)
                    result.Add(simplifiedLeft[i]);
                for (int i = 0; i < simplifiedRight.Count; i++)
                    result.Add(simplifiedRight[i]);
            }
            else
            {
                result.Add(first);
                result.Add(last);
            }

            return result;
        }

        private static float PointLineDistSq(PointF p, PointF a, PointF b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float lenSq = dx * dx + dy * dy;
            if (lenSq < 0.0001f)
            {
                float ddx = p.X - a.X, ddy = p.Y - a.Y;
                return ddx * ddx + ddy * ddy;
            }
            float t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq;
            t = Math.Max(0f, Math.Min(1f, t));
            float projX = a.X + t * dx, projY = a.Y + t * dy;
            float pdx = p.X - projX, pdy = p.Y - projY;
            return pdx * pdx + pdy * pdy;
        }
    }
}
