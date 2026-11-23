using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using DiagramDesigner.Core;
using DiagramDesigner.Serialization;
using DiagramDesigner.Shapes;

namespace DiagramDesigner.Tests
{
    /// <summary>
    /// 轻量测试运行器。无第三方框架依赖，通过控制台输出结果。
    /// 覆盖：RenderCommand 坐标映射、Connection 控制点计算、
    /// DrawingDocument ZOrder、XmlShapeSerializer 往返一致性、
    /// SpatialGrid 空间索引、ChangeHistory 撤销重做。
    /// </summary>
    class Tests
    {
        static int _pass = 0;
        static int _fail = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("=== DiagramDesigner 单元测试 ===\n");

            // 1. RenderCommand 坐标映射
            TestRenderCommandMapping();

            // 2. Connection 控制点计算
            TestConnectionControlPoints();

            // 3. DrawingDocument ZOrder
            TestZOrder();

            // 4. XmlShapeSerializer 往返一致性
            TestSerializerRoundTrip();

            // 5. SpatialGrid 空间索引
            TestSpatialGrid();

            // 6. ChangeHistory 撤销重做
            TestChangeHistory();

            // 7. ShapeBase.GetNearestConnectionPoint
            TestNearestConnectionPoint();

            Console.WriteLine("\n=== 结果 ===");
            Console.WriteLine("通过: " + _pass + "  失败: " + _fail);
            if (_fail > 0)
                Environment.Exit(1);
        }

        // ============================================================
        // 辅助方法
        // ============================================================

        static void Assert(bool condition, string message)
        {
            if (condition)
            {
                _pass++;
                Console.WriteLine("  [PASS] " + message);
            }
            else
            {
                _fail++;
                Console.WriteLine("  [FAIL] " + message);
            }
        }

        static float F(float v) { return (float)Math.Round(v, 2); }

        // ============================================================
        // 1. RenderCommand 坐标映射
        //    MapPoint(normX, normY, rect) = rect.Location + (normX*rect.W, normY*rect.H)
        //    通过反射调用私有 MapPoint 验证
        // ============================================================

        static void TestRenderCommandMapping()
        {
            Console.WriteLine("\n--- RenderCommand 坐标映射 ---");

            // MapPoint 是私有静态方法，通过反射调用
            System.Reflection.MethodInfo mi = typeof(RenderCommand).GetMethod(
                "MapPoint",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null,
                new Type[] { typeof(float), typeof(float), typeof(RectangleF) },
                null);

            Assert(mi != null, "MapPoint 方法存在");

            if (mi == null) return;

            // 确保没有激活的 Transformer，走默认线性映射
            RenderCommand.ClearVertexTransformer();
            RenderCommand.ClearScalableZoneBounds();

            // 测试1: 原点映射
            RectangleF rect = new RectangleF(100, 200, 400, 300);
            PointF r1 = (PointF)mi.Invoke(null, new object[] { 0f, 0f, rect });
            Assert(r1.X == 100f && r1.Y == 200f, "原点(0,0) → rect.Location");

            // 测试2: 对角点映射
            PointF r2 = (PointF)mi.Invoke(null, new object[] { 1f, 1f, rect });
            Assert(r2.X == 500f && r2.Y == 500f, "对角(1,1) → rect.RightBottom");

            // 测试3: 中心点映射
            PointF r3 = (PointF)mi.Invoke(null, new object[] { 0.5f, 0.5f, rect });
            Assert(r3.X == 300f && r3.Y == 350f, "中心(0.5,0.5) → rect.Center");

            // 测试4: 负坐标 rect
            RectangleF rect2 = new RectangleF(-200, -100, 100, 50);
            PointF r4 = (PointF)mi.Invoke(null, new object[] { 0.5f, 0.5f, rect2 });
            Assert(r4.X == -150f && r4.Y == -75f, "负坐标 rect 中心映射");
        }

        // ============================================================
        // 2. Connection 控制点计算
        //    GetDrawPoints 根据 Mode 生成不同路径点
        // ============================================================

        static void TestConnectionControlPoints()
        {
            Console.WriteLine("\n--- Connection 控制点计算 ---");

            // GetDrawPoints 是私有方法，通过反射调用
            System.Reflection.MethodInfo mi = typeof(Connection).GetMethod(
                "GetDrawPoints",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert(mi != null, "GetDrawPoints 方法存在");
            if (mi == null) return;

            Connection conn = new Connection();
            conn.FromPoint = new PointF(0, 0);
            conn.ToPoint = new PointF(100, 200);

            // Straight: 2 个点
            conn.Mode = ConnectionMode.Straight;
            PointF[] straight = (PointF[])mi.Invoke(conn, null);
            Assert(straight.Length == 2, "Straight 模式返回 2 个点");
            Assert(straight[0] == conn.FromPoint && straight[1] == conn.ToPoint,
                "Straight 端点正确");

            // Curve: 4 个点，mid1/mid2 的 X 为两端中点
            conn.Mode = ConnectionMode.Curve;
            PointF[] curve = (PointF[])mi.Invoke(conn, null);
            Assert(curve.Length == 4, "Curve 模式返回 4 个点");
            Assert(curve[0] == conn.FromPoint && curve[3] == conn.ToPoint,
                "Curve 首尾端点正确");
            float midX = (conn.FromPoint.X + conn.ToPoint.X) / 2f;
            Assert(F(curve[1].X) == F(midX) && F(curve[2].X) == F(midX),
                "Curve 控制点 X 为两端 X 中点");
            Assert(F(curve[1].Y) == F(conn.FromPoint.Y) && F(curve[2].Y) == F(conn.ToPoint.Y),
                "Curve 控制点 Y 分别取 from/to 的 Y");

            // Orthogonal: 4 个点，中间两点 X 为 midX
            conn.Mode = ConnectionMode.Orthogonal;
            PointF[] ortho = (PointF[])mi.Invoke(conn, null);
            Assert(ortho.Length == 4, "Orthogonal 模式返回 4 个点");
            Assert(ortho[0] == conn.FromPoint && ortho[3] == conn.ToPoint,
                "Orthogonal 首尾端点正确");
            Assert(F(ortho[1].X) == F(midX) && F(ortho[2].X) == F(midX),
                "Orthogonal 中间点 X 为 midX");
            Assert(F(ortho[1].Y) == F(conn.FromPoint.Y) && F(ortho[2].Y) == F(conn.ToPoint.Y),
                "Orthogonal 中间点 Y 分别取 from/to 的 Y");
        }

        // ============================================================
        // 3. DrawingDocument ZOrder
        // ============================================================

        static void TestZOrder()
        {
            Console.WriteLine("\n--- DrawingDocument ZOrder ---");

            DrawingDocument doc = new DrawingDocument();

            TestShape s1 = new TestShape();
            TestShape s2 = new TestShape();
            TestShape s3 = new TestShape();

            doc.AddShape(s1);
            doc.AddShape(s2);
            doc.AddShape(s3);

            Assert(s1.ZOrder == 1, "第一个 Shape ZOrder=1");
            Assert(s2.ZOrder == 2, "第二个 Shape ZOrder=2");
            Assert(s3.ZOrder == 3, "第三个 Shape ZOrder=3");

            // BringToFront: s1 → 最前
            doc.BringToFront(s1);
            Assert(s1.ZOrder > s2.ZOrder && s1.ZOrder > s3.ZOrder,
                "BringToFront 后 ZOrder 最大");

            // SendToBack: s3 → 最后
            doc.SendToBack(s3);
            Assert(s3.ZOrder < s1.ZOrder && s3.ZOrder < s2.ZOrder,
                "SendToBack 后 ZOrder 最小");

            // HitTestShape 按 ZOrder 倒序（最前的优先命中）
            // 将 s1 移开，仅 s2 和 s3 重叠在 (0,0)-(100,100)
            s1.Bounds = new RectangleF(1000, 1000, 50, 50);
            s2.Bounds = new RectangleF(0, 0, 100, 100);
            s3.Bounds = new RectangleF(0, 0, 100, 100);
            doc.Reindex();
            ShapeBase hit = doc.HitTestShape(new PointF(50, 50));
            Assert(hit != null, "重叠区域命中非空");
            // SendToBack(s3) 后 s2 比 s3 更靠前
            bool s2BeforeS3 = s2.ZOrder > s3.ZOrder;
            Assert(s2BeforeS3 ? hit == s2 : hit == s3,
                "ZOrder 更大的 Shape 优先命中");
        }

        // ============================================================
        // 4. XmlShapeSerializer 往返一致性
        // ============================================================

        static void TestSerializerRoundTrip()
        {
            Console.WriteLine("\n--- XmlShapeSerializer 往返一致性 ---");

            DrawingDocument doc = new DrawingDocument();
            doc.Name = "测试文档";
            doc.PageSize = new SizeF(2000, 1500);

            TestShape s1 = new TestShape();
            s1.Name = "实体A";
            s1.Description = "测试描述";
            s1.Bounds = new RectangleF(100, 200, 300, 150);
            s1.FillColor = Color.Red;
            s1.BorderColor = Color.Blue;
            s1.X = 100;
            s1.Y = 200;

            TestShape s2 = new TestShape();
            s2.Name = "实体B";
            s2.Bounds = new RectangleF(500, 100, 200, 200);

            doc.AddShape(s1);
            doc.AddShape(s2);

            // 添加连线
            Connection conn = new Connection();
            conn.FromShape = s1;
            conn.ToShape = s2;
            conn.Mode = ConnectionMode.Curve;
            conn.LineColor = Color.Green;
            conn.LineWidth = 2.5f;
            conn.Label = "关联";
            doc.AddConnection(conn);

            string tempFile = Path.Combine(Path.GetTempPath(), "dd_test_roundtrip.xml");
            try
            {
                XmlShapeSerializer.Save(tempFile, doc);
                Assert(File.Exists(tempFile), "序列化生成文件");

                DrawingDocument loaded = XmlShapeSerializer.Load(tempFile);
                Assert(loaded != null, "反序列化返回非空文档");
                Assert(loaded.Name == "测试文档", "文档名称往返一致");
                Assert(loaded.Shapes.Count == 2, "Shape 数量往返一致");
                Assert(loaded.Connections.Count == 1, "Connection 数量往返一致");

                if (loaded.Shapes.Count >= 2)
                {
                    ShapeBase ls1 = loaded.Shapes[0];
                    Assert(ls1.Name == "实体A", "Shape1 名称往返一致");
                    Assert(F(ls1.Bounds.X) == 100f && F(ls1.Bounds.Y) == 200f,
                        "Shape1 位置往返一致");
                    Assert(F(ls1.Bounds.Width) == 300f && F(ls1.Bounds.Height) == 150f,
                        "Shape1 尺寸往返一致");
                    Assert(ls1.FillColor.ToArgb() == Color.Red.ToArgb(),
                        "Shape1 填充色往返一致");

                    Connection lc = loaded.Connections[0];
                    Assert(lc.Mode == ConnectionMode.Curve, "连线模式往返一致");
                    Assert(F(lc.LineWidth) == 2.5f, "线宽往返一致");
                    Assert(lc.Label == "关联", "标签往返一致");
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        // ============================================================
        // 5. SpatialGrid 空间索引
        // ============================================================

        static void TestSpatialGrid()
        {
            Console.WriteLine("\n--- SpatialGrid 空间索引 ---");

            SpatialGrid grid = new SpatialGrid(100f);
            List<ShapeBase> shapes = new List<ShapeBase>();

            TestShape s1 = new TestShape();
            s1.Bounds = new RectangleF(50, 50, 80, 80);    // cell (0,0)
            TestShape s2 = new TestShape();
            s2.Bounds = new RectangleF(350, 250, 50, 50);   // cell (3,2)
            TestShape s3 = new TestShape();
            s3.Bounds = new RectangleF(50, 50, 400, 400);   // 跨多格

            shapes.Add(s1);
            shapes.Add(s2);
            shapes.Add(s3);

            grid.Rebuild(shapes);
            Assert(!grid.IsDirty, "Rebuild 后 IsDirty=false");

            // 点查询：落在 s1 所在单元格
            List<ShapeBase> hit1 = grid.Query(new PointF(60, 60));
            Assert(hit1 != null && hit1.Contains(s1), "点(60,60) 命中 s1");
            Assert(hit1 != null && hit1.Contains(s3), "点(60,60) 也命中跨格 s3");

            // 点查询：落在 s2 所在单元格
            List<ShapeBase> hit2 = grid.Query(new PointF(360, 260));
            Assert(hit2 != null && hit2.Contains(s2), "点(360,260) 命中 s2");
            Assert(hit2 != null && hit2.Contains(s3), "点(360,260) 也命中跨格 s3");

            // 点查询：空区域
            List<ShapeBase> hit3 = grid.Query(new PointF(900, 900));
            Assert(hit3 == null || hit3.Count == 0, "点(900,900) 无命中");

            // 矩形查询
            List<ShapeBase> rectHits = grid.Query(new RectangleF(0, 0, 200, 200));
            Assert(rectHits.Contains(s1) && rectHits.Contains(s3), "矩形(0,0,200,200) 命中 s1 和 s3");
            Assert(!rectHits.Contains(s2), "矩形(0,0,200,200) 不命中 s2");

            // Invalidate 后 IsDirty=true
            grid.Invalidate();
            Assert(grid.IsDirty, "Invalidate 后 IsDirty=true");
        }

        // ============================================================
        // 6. ChangeHistory 撤销重做
        // ============================================================

        static void TestChangeHistory()
        {
            Console.WriteLine("\n--- ChangeHistory 撤销重做 ---");

            DrawingDocument doc = new DrawingDocument();
            ChangeHistory history = new ChangeHistory();

            TestShape shape = new TestShape();
            shape.Name = "原始名称";
            doc.AddShape(shape);

            // 模拟属性变更
            ChangeSet cs = new ChangeSet();
            cs.Kind = ChangeKind.ShapePropertyChanged;
            cs.ShapeId = shape.Id;
            cs.PropertyName = "Name";
            cs.OldValue = "原始名称";
            cs.NewValue = "修改后名称";
            history.Push(cs);

            // 执行正向变更（模拟画布侧应用）
            shape.Name = "修改后名称";

            Assert(history.CanUndo, "Push 后 CanUndo=true");
            Assert(!history.CanRedo, "Push 后 CanRedo=false");

            // Undo
            history.Undo(doc);
            Assert(shape.Name == "原始名称", "Undo 恢复属性旧值");
            Assert(history.CanRedo, "Undo 后 CanRedo=true");
            Assert(!history.CanUndo, "Undo 后 CanUndo=false（单步）");

            // Redo
            history.Redo(doc);
            Assert(shape.Name == "修改后名称", "Redo 恢复属性新值");
            Assert(history.CanUndo, "Redo 后 CanUndo=true");
            Assert(!history.CanRedo, "Redo 后 CanRedo=false");

            // 新变更清空 redo 栈
            ChangeSet cs2 = new ChangeSet();
            cs2.Kind = ChangeKind.ShapePropertyChanged;
            cs2.ShapeId = shape.Id;
            cs2.PropertyName = "Name";
            cs2.OldValue = "修改后名称";
            cs2.NewValue = "再次修改";
            history.Push(cs2);
            shape.Name = "再次修改";
            Assert(!history.CanRedo, "新 Push 后 CanRedo=false（redo 栈已清空）");

            // 测试 ShapeAdded 撤销
            ChangeHistory history2 = new ChangeHistory();
            DrawingDocument doc2 = new DrawingDocument();
            TestShape newShape = new TestShape();

            ChangeSet addCs = new ChangeSet();
            addCs.Kind = ChangeKind.ShapeAdded;
            addCs.ShapeId = newShape.Id;
            addCs.NewValue = newShape;
            history2.Push(addCs);
            doc2.AddShape(newShape);
            Assert(doc2.Shapes.Count == 1, "ShapeAdded 后文档有 1 个 Shape");

            history2.Undo(doc2);
            Assert(doc2.Shapes.Count == 0, "Undo ShapeAdded 后文档为空");

            history2.Redo(doc2);
            Assert(doc2.Shapes.Count == 1, "Redo ShapeAdded 后文档恢复 1 个 Shape");
        }

        // ============================================================
        // 7. ShapeBase.GetNearestConnectionPoint
        // ============================================================

        static void TestNearestConnectionPoint()
        {
            Console.WriteLine("\n--- ShapeBase.GetNearestConnectionPoint ---");

            TestShape shape = new TestShape();
            shape.Bounds = new RectangleF(100, 100, 200, 100);
            // Center = (200, 150), halfW=100, halfH=50

            // 从右侧水平方向 → 命中右边中点
            PointF fromRight = new PointF(500, 150);
            PointF p1 = shape.GetNearestConnectionPoint(fromRight);
            Assert(F(p1.X) == 300f && F(p1.Y) == 150f, "右侧方向 → 右边中点(300,150)");

            // 从下方垂直方向 → 命中底边中点
            PointF fromBottom = new PointF(200, 500);
            PointF p2 = shape.GetNearestConnectionPoint(fromBottom);
            Assert(F(p2.X) == 200f && F(p2.Y) == 200f, "下方方向 → 底边中点(200,200)");

            // 从左上对角方向 → 命中角点
            PointF fromCorner = new PointF(0, 0);
            PointF p3 = shape.GetNearestConnectionPoint(fromCorner);
            // dx=-200, dy=-150, absDx*halfH=10000, absDy*halfW=15000 → 走 Y 方向
            // scale=halfH/absDy=50/150=0.333 → point=(200-200*0.333, 150-150*0.333)=(133.33,100)
            Assert(F(p3.Y) == 100f, "左上对角方向 → Y=100（顶边）");

            // from=center 特殊情况
            PointF p4 = shape.GetNearestConnectionPoint(new PointF(200, 150));
            Assert(F(p4.X) == 300f && F(p4.Y) == 150f, "from=center → 右边中点（默认）");
        }
    }

    /// <summary>
    /// 最小可实例化 ShapeBase 子类，用于测试。
    /// </summary>
    class TestShape : ShapeBase
    {
        public TestShape()
        {
            Resizable = true;
        }

        public override void Draw(Graphics g, float scale)
        {
            // 测试用空实现
        }

        public override ShapeBase Clone()
        {
            TestShape clone = new TestShape();
            clone.Bounds = this.Bounds;
            clone.Name = this.Name;
            clone.Description = this.Description;
            clone.FillColor = this.FillColor;
            clone.BorderColor = this.BorderColor;
            clone.Id = this.Id;
            return clone;
        }
    }
}
