using System;
using System.Collections.Generic;
using System.Drawing;

namespace DiagramDesigner.Core
{
    /// <summary>
    /// 内置图形库。预定义大量常用图形类型，宿主可通过一行代码注册全部或部分图形。
    /// </summary>
    public static class ShapeLibrary
    {
        private static readonly Dictionary<string, ShapeType> _library = new Dictionary<string, ShapeType>();
        private static bool _initialized = false;

        // .NET 2.0 兼容：自定义委托替代 Func<T>
        private delegate ShapeType ShapeFactory();

        #region 基本图形

        public static ShapeType Rectangle
        {
            get { return GetOrCreate("矩形", "基本图形", CreateRectangle); }
        }

        public static ShapeType RoundedRectangle
        {
            get { return GetOrCreate("圆角矩形", "基本图形", CreateRoundedRectangle); }
        }

        public static ShapeType Ellipse
        {
            get { return GetOrCreate("椭圆", "基本图形", CreateEllipse); }
        }

        public static ShapeType Diamond
        {
            get { return GetOrCreate("菱形", "基本图形", CreateDiamond); }
        }

        public static ShapeType Triangle
        {
            get { return GetOrCreate("三角形", "基本图形", CreateTriangle); }
        }

        public static ShapeType RightTriangle
        {
            get { return GetOrCreate("直角三角形", "基本图形", CreateRightTriangle); }
        }

        public static ShapeType Pentagon
        {
            get { return GetOrCreate("五边形", "基本图形", CreatePentagon); }
        }

        public static ShapeType Hexagon
        {
            get { return GetOrCreate("六边形", "基本图形", CreateHexagon); }
        }

        public static ShapeType Octagon
        {
            get { return GetOrCreate("八边形", "基本图形", CreateOctagon); }
        }

        public static ShapeType Star
        {
            get { return GetOrCreate("星形", "基本图形", CreateStar); }
        }

        public static ShapeType Cross
        {
            get { return GetOrCreate("十字形", "基本图形", CreateCross); }
        }

        public static ShapeType Trapezoid
        {
            get { return GetOrCreate("梯形", "基本图形", CreateTrapezoid); }
        }

        public static ShapeType Parallelogram
        {
            get { return GetOrCreate("平行四边形", "基本图形", CreateParallelogram); }
        }

        public static ShapeType Teardrop
        {
            get { return GetOrCreate("水滴", "基本图形", CreateTeardrop); }
        }

        public static ShapeType Ring
        {
            get { return GetOrCreate("圆环", "基本图形", CreateRing); }
        }

        #endregion

        #region 流程图

        public static ShapeType FlowStartEnd
        {
            get { return GetOrCreate("起止", "流程图", CreateFlowStartEnd); }
        }

        public static ShapeType FlowProcess
        {
            get { return GetOrCreate("处理", "流程图", CreateFlowProcess); }
        }

        public static ShapeType FlowDecision
        {
            get { return GetOrCreate("判断", "流程图", CreateFlowDecision); }
        }

        public static ShapeType FlowDocument
        {
            get { return GetOrCreate("文档", "流程图", CreateFlowDocument); }
        }

        public static ShapeType FlowData
        {
            get { return GetOrCreate("数据", "流程图", CreateFlowData); }
        }

        public static ShapeType FlowPredefined
        {
            get { return GetOrCreate("预定义处理", "流程图", CreateFlowPredefined); }
        }

        public static ShapeType FlowDelay
        {
            get { return GetOrCreate("延迟", "流程图", CreateFlowDelay); }
        }

        public static ShapeType FlowMerge
        {
            get { return GetOrCreate("合并", "流程图", CreateFlowMerge); }
        }

        public static ShapeType FlowOffPage
        {
            get { return GetOrCreate("离页连接", "流程图", CreateFlowOffPage); }
        }

        public static ShapeType FlowPreparation
        {
            get { return GetOrCreate("准备", "流程图", CreateFlowPreparation); }
        }

        public static ShapeType FlowParallel
        {
            get { return GetOrCreate("并行", "流程图", CreateFlowParallel); }
        }

        public static ShapeType FlowStorage
        {
            get { return GetOrCreate("存储", "流程图", CreateFlowStorage); }
        }

        public static ShapeType FlowDisplay
        {
            get { return GetOrCreate("显示", "流程图", CreateFlowDisplay); }
        }

        public static ShapeType FlowManual
        {
            get { return GetOrCreate("人工操作", "流程图", CreateFlowManual); }
        }

        #endregion

        #region 云原生/架构

        public static ShapeType Cloud
        {
            get { return GetOrCreate("云", "云原生", CreateCloud); }
        }

        public static ShapeType Database
        {
            get { return GetOrCreate("数据库", "云原生", CreateDatabase); }
        }

        public static ShapeType Server
        {
            get { return GetOrCreate("服务器", "云原生", CreateServer); }
        }

        public static ShapeType Container
        {
            get { return GetOrCreate("容器", "云原生", CreateContainer); }
        }

        public static ShapeType Microservice
        {
            get { return GetOrCreate("微服务", "云原生", CreateMicroservice); }
        }

        public static ShapeType LoadBalancer
        {
            get { return GetOrCreate("负载均衡", "云原生", CreateLoadBalancer); }
        }

        public static ShapeType Cache
        {
            get { return GetOrCreate("缓存", "云原生", CreateCache); }
        }

        public static ShapeType MessageQueue
        {
            get { return GetOrCreate("消息队列", "云原生", CreateMessageQueue); }
        }

        public static ShapeType Gateway
        {
            get { return GetOrCreate("网关", "云原生", CreateGateway); }
        }

        public static ShapeType Volume
        {
            get { return GetOrCreate("存储卷", "云原生", CreateVolume); }
        }

        #endregion

        #region UML

        public static ShapeType UmlClass
        {
            get { return GetOrCreate("类", "UML", CreateUmlClass); }
        }

        public static ShapeType UmlActor
        {
            get { return GetOrCreate("参与者", "UML", CreateUmlActor); }
        }

        public static ShapeType UmlInterface
        {
            get { return GetOrCreate("接口", "UML", CreateUmlInterface); }
        }

        public static ShapeType UmlPackage
        {
            get { return GetOrCreate("包", "UML", CreateUmlPackage); }
        }

        public static ShapeType UmlNote
        {
            get { return GetOrCreate("注释", "UML", CreateUmlNote); }
        }

        public static ShapeType UmlUseCase
        {
            get { return GetOrCreate("用例", "UML", CreateUmlUseCase); }
        }

        #endregion

        #region 网络

        public static ShapeType NetFirewall
        {
            get { return GetOrCreate("防火墙", "网络", CreateNetFirewall); }
        }

        public static ShapeType NetRouter
        {
            get { return GetOrCreate("路由器", "网络", CreateNetRouter); }
        }

        public static ShapeType NetSwitch
        {
            get { return GetOrCreate("交换机", "网络", CreateNetSwitch); }
        }

        #endregion

        #region 集合访问

        /// <summary>
        /// 所有内置图形
        /// </summary>
        public static IEnumerable<ShapeType> All
        {
            get
            {
                EnsureInitialized();
                return _library.Values;
            }
        }

        /// <summary>
        /// 所有类别名称
        /// </summary>
        public static IEnumerable<string> Categories
        {
            get
            {
                List<string> cats = new List<string>();
                foreach (ShapeType t in All)
                {
                    if (!cats.Contains(t.Category))
                        cats.Add(t.Category);
                }
                return cats;
            }
        }

        /// <summary>
        /// 按类别获取图形
        /// </summary>
        public static IEnumerable<ShapeType> GetByCategory(string category)
        {
            foreach (ShapeType t in All)
            {
                if (t.Category == category)
                    yield return t;
            }
        }

        #endregion

        #region 注册方法

        /// <summary>
        /// 注册全部内置图形到 ShapeTypeRegistry
        /// </summary>
        public static void RegisterAll()
        {
            EnsureInitialized();
            foreach (ShapeType type in _library.Values)
            {
                ShapeTypeRegistry.Instance.Register(type);
            }
        }

        /// <summary>
        /// 注册指定类别的全部图形
        /// </summary>
        public static void RegisterCategory(string category)
        {
            foreach (ShapeType type in GetByCategory(category))
            {
                ShapeTypeRegistry.Instance.Register(type);
            }
        }

        /// <summary>
        /// 注册指定的一个或多个图形
        /// </summary>
        public static void Register(params ShapeType[] types)
        {
            foreach (ShapeType type in types)
            {
                if (type != null)
                    ShapeTypeRegistry.Instance.Register(type);
            }
        }

        #endregion

        #region 内部辅助

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            // 触发所有静态属性的初始化
            ShapeType _ = Rectangle; _ = RoundedRectangle; _ = Ellipse; _ = Diamond;
            _ = Triangle; _ = RightTriangle; _ = Pentagon; _ = Hexagon;
            _ = Octagon; _ = Star; _ = Cross; _ = Trapezoid;
            _ = Parallelogram; _ = Teardrop; _ = Ring;
            _ = FlowStartEnd; _ = FlowProcess; _ = FlowDecision; _ = FlowDocument;
            _ = FlowData; _ = FlowPredefined; _ = FlowDelay; _ = FlowMerge;
            _ = FlowOffPage; _ = FlowPreparation; _ = FlowParallel; _ = FlowStorage;
            _ = FlowDisplay; _ = FlowManual;
            _ = Cloud; _ = Database; _ = Server; _ = Container;
            _ = Microservice; _ = LoadBalancer; _ = Cache; _ = MessageQueue;
            _ = Gateway; _ = Volume;
            _ = UmlClass; _ = UmlActor; _ = UmlInterface; _ = UmlPackage;
            _ = UmlNote; _ = UmlUseCase;
            _ = NetFirewall; _ = NetRouter; _ = NetSwitch;
            _initialized = true;
        }

        private static ShapeType GetOrCreate(string name, string category, ShapeFactory factory)
        {
            if (_library.ContainsKey(name))
                return _library[name];
            ShapeType type = factory();
            _library[name] = type;
            return type;
        }

        private static ShapeType CreateBase(string name, string category, Color fill, Color border, float w, float h)
        {
            ShapeType t = new ShapeType();
            t.Name = name;
            t.Category = category;
            t.Description = name;
            t.DefaultWidth = w;
            t.DefaultHeight = h;
            t.DefaultFillColor = new XmlColor(fill);
            t.DefaultBorderColor = new XmlColor(border);
            return t;
        }

        private static RenderCommand RoundedRectCmd(float x, float y, float w, float h, float r, bool useColors)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.RoundedRect;
            c.X = x; c.Y = y; c.Width = w; c.Height = h;
            c.CornerRadius = r;
            c.UseShapeColors = useColors;
            c.Fill = true; c.Stroke = true;
            return c;
        }

        private static RenderCommand RectCmd(float x, float y, float w, float h, bool useColors)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.Rectangle;
            c.X = x; c.Y = y; c.Width = w; c.Height = h;
            c.UseShapeColors = useColors;
            c.Fill = true; c.Stroke = true;
            return c;
        }

        private static RenderCommand EllipseCmd(float x, float y, float w, float h, bool useColors)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.Ellipse;
            c.X = x; c.Y = y; c.Width = w; c.Height = h;
            c.UseShapeColors = useColors;
            c.Fill = true; c.Stroke = true;
            return c;
        }

        private static RenderCommand PolygonCmd(PointF[] pts, bool useColors)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.Polygon;
            c.X = 0; c.Y = 0; c.Width = 1; c.Height = 1;
            c.PolygonPoints = pts;
            c.UseShapeColors = useColors;
            c.Fill = true; c.Stroke = true;
            return c;
        }

        private static RenderCommand LineCmd(float x1, float y1, float x2, float y2, Color color)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.Line;
            c.X = x1; c.Y = y1; c.Width = x2 - x1; c.Height = y2 - y1;
            c.StrokeColor = new XmlColor(color);
            c.UseShapeColors = false;
            c.Fill = false; c.Stroke = true;
            return c;
        }

        private static RenderCommand TextCmd(float x, float y, float w, float h, string text, string align, float fontSize, bool bold)
        {
            RenderCommand c = new RenderCommand();
            c.CommandType = RenderCommandType.Text;
            c.X = x; c.Y = y; c.Width = w; c.Height = h;
            c.Text = text;
            c.TextAlign = align;
            c.FontSize = fontSize;
            c.IsBold = bold;
            c.UseShapeColors = true;
            return c;
        }

        #endregion

        #region 基本图形工厂

        private static ShapeType CreateRectangle()
        {
            ShapeType t = CreateBase("矩形", "基本图形", Color.FromArgb(230, 245, 255), Color.FromArgb(60, 130, 200), 140, 90);
            t.SupportsMembers = true;
            t.RenderCommands.Add(RectCmd(0, 0, 1, 1, true));
            t.RenderCommands.Add(TextCmd(0, 0, 1, 0.35f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateRoundedRectangle()
        {
            ShapeType t = CreateBase("圆角矩形", "基本图形", Color.FromArgb(240, 245, 250), Color.FromArgb(80, 130, 180), 140, 90);
            t.SupportsMembers = true;
            t.RenderCommands.Add(RoundedRectCmd(0, 0, 1, 1, 10f, true));
            t.RenderCommands.Add(TextCmd(0, 0, 1, 0.35f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateEllipse()
        {
            ShapeType t = CreateBase("椭圆", "基本图形", Color.FromArgb(255, 240, 230), Color.FromArgb(200, 130, 60), 120, 100);
            t.SupportsMembers = true;
            t.RenderCommands.Add(EllipseCmd(0, 0, 1, 1, true));
            t.RenderCommands.Add(TextCmd(0, 0, 1, 0.35f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateDiamond()
        {
            ShapeType t = CreateBase("菱形", "基本图形", Color.FromArgb(230, 255, 240), Color.FromArgb(60, 180, 100), 120, 100);
            t.SupportsMembers = true;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.5f, 0f), new PointF(1f, 0.5f),
                new PointF(0.5f, 1f), new PointF(0f, 0.5f)
            }, true));
            t.RenderCommands.Add(TextCmd(0, 0, 1, 0.35f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateTriangle()
        {
            ShapeType t = CreateBase("三角形", "基本图形", Color.FromArgb(255, 245, 230), Color.FromArgb(200, 140, 60), 120, 100);
            t.SupportsMembers = true;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.5f, 0f), new PointF(1f, 1f), new PointF(0f, 1f)
            }, true));
            t.RenderCommands.Add(TextCmd(0.15f, 0.15f, 0.7f, 0.3f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateRightTriangle()
        {
            ShapeType t = CreateBase("直角三角形", "基本图形", Color.FromArgb(245, 230, 255), Color.FromArgb(140, 60, 200), 120, 100);
            t.SupportsMembers = true;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0f, 0f), new PointF(1f, 1f), new PointF(0f, 1f)
            }, true));
            t.RenderCommands.Add(TextCmd(0.05f, 0.1f, 0.5f, 0.3f, "", "center", 9f, false));
            return t;
        }

        private static ShapeType CreatePentagon()
        {
            ShapeType t = CreateBase("五边形", "基本图形", Color.FromArgb(255, 250, 230), Color.FromArgb(180, 150, 50), 120, 110);
            t.SupportsMembers = true;
            float[] angles = { -90, -18, 54, 126, 198 };
            PointF[] pts = new PointF[5];
            for (int i = 0; i < 5; i++)
            {
                float rad = angles[i] * (float)Math.PI / 180f;
                pts[i] = new PointF(0.5f + 0.5f * (float)Math.Cos(rad), 0.5f + 0.5f * (float)Math.Sin(rad));
            }
            t.RenderCommands.Add(PolygonCmd(pts, true));
            t.RenderCommands.Add(TextCmd(0.2f, 0.2f, 0.6f, 0.3f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateHexagon()
        {
            ShapeType t = CreateBase("六边形", "基本图形", Color.FromArgb(240, 230, 255), Color.FromArgb(130, 60, 200), 130, 110);
            t.SupportsMembers = true;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.25f, 0f), new PointF(0.75f, 0f),
                new PointF(1f, 0.5f), new PointF(0.75f, 1f),
                new PointF(0.25f, 1f), new PointF(0f, 0.5f)
            }, true));
            t.RenderCommands.Add(TextCmd(0.1f, 0.1f, 0.8f, 0.3f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateOctagon()
        {
            ShapeType t = CreateBase("八边形", "基本图形", Color.FromArgb(230, 255, 250), Color.FromArgb(60, 160, 140), 120, 120);
            t.SupportsMembers = true;
            PointF[] pts = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                float angle = (i * 45f - 90f) * (float)Math.PI / 180f;
                float r = 0.5f;
                float offset = 0.15f;
                float x = 0.5f, y = 0.5f;
                if (Math.Abs(Math.Cos(angle)) > 0.7) x += (float)Math.Cos(angle) * (r - offset);
                else x += (float)Math.Cos(angle) * r;
                if (Math.Abs(Math.Sin(angle)) > 0.7) y += (float)Math.Sin(angle) * (r - offset);
                else y += (float)Math.Sin(angle) * r;
                pts[i] = new PointF(x, y);
            }
            t.RenderCommands.Add(PolygonCmd(pts, true));
            t.RenderCommands.Add(TextCmd(0.15f, 0.15f, 0.7f, 0.3f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateStar()
        {
            ShapeType t = CreateBase("星形", "基本图形", Color.FromArgb(255, 245, 200), Color.FromArgb(200, 160, 40), 120, 120);
            t.SupportsMembers = true;
            PointF[] pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = (i * 36f - 90f) * (float)Math.PI / 180f;
                float r = (i % 2 == 0) ? 0.5f : 0.22f;
                pts[i] = new PointF(0.5f + r * (float)Math.Cos(angle), 0.5f + r * (float)Math.Sin(angle));
            }
            t.RenderCommands.Add(PolygonCmd(pts, true));
            t.RenderCommands.Add(TextCmd(0.25f, 0.35f, 0.5f, 0.3f, "", "center", 9f, false));
            return t;
        }

        private static ShapeType CreateCross()
        {
            ShapeType t = CreateBase("十字形", "基本图形", Color.FromArgb(255, 230, 230), Color.FromArgb(180, 80, 80), 100, 100);
            t.SupportsMembers = false;
            float w = 0.25f;
            float hw = w / 2f;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.5f - hw, 0f), new PointF(0.5f + hw, 0f),
                new PointF(0.5f + hw, 0.5f - hw), new PointF(1f, 0.5f - hw),
                new PointF(1f, 0.5f + hw), new PointF(0.5f + hw, 0.5f + hw),
                new PointF(0.5f + hw, 1f), new PointF(0.5f - hw, 1f),
                new PointF(0.5f - hw, 0.5f + hw), new PointF(0f, 0.5f + hw),
                new PointF(0f, 0.5f - hw), new PointF(0.5f - hw, 0.5f - hw)
            }, true));
            return t;
        }

        private static ShapeType CreateTrapezoid()
        {
            ShapeType t = CreateBase("梯形", "基本图形", Color.FromArgb(240, 255, 240), Color.FromArgb(80, 160, 80), 140, 90);
            t.SupportsMembers = true;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.15f, 0f), new PointF(0.85f, 0f),
                new PointF(1f, 1f), new PointF(0f, 1f)
            }, true));
            t.RenderCommands.Add(TextCmd(0.1f, 0.1f, 0.8f, 0.3f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateParallelogram()
        {
            ShapeType t = CreateBase("平行四边形", "基本图形", Color.FromArgb(255, 240, 245), Color.FromArgb(180, 80, 120), 140, 90);
            t.SupportsMembers = true;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.15f, 0f), new PointF(1f, 0f),
                new PointF(0.85f, 1f), new PointF(0f, 1f)
            }, true));
            t.RenderCommands.Add(TextCmd(0.1f, 0.1f, 0.8f, 0.3f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateTeardrop()
        {
            ShapeType t = CreateBase("水滴", "基本图形", Color.FromArgb(220, 240, 255), Color.FromArgb(60, 120, 200), 100, 130);
            t.SupportsMembers = false;
            // 上半圆 + 下尖三角
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.2f, 0.3f), new PointF(0.5f, 0f), new PointF(0.8f, 0.3f),
                new PointF(0.85f, 0.55f), new PointF(0.5f, 1f), new PointF(0.15f, 0.55f)
            }, true));
            return t;
        }

        private static ShapeType CreateRing()
        {
            ShapeType t = CreateBase("圆环", "基本图形", Color.FromArgb(240, 240, 240), Color.FromArgb(100, 100, 100), 100, 100);
            t.SupportsMembers = false;
            t.RenderCommands.Add(EllipseCmd(0, 0, 1, 1, true));
            // 内部镂空椭圆（白色覆盖）
            RenderCommand inner = EllipseCmd(0.25f, 0.25f, 0.5f, 0.5f, false);
            inner.FillColor = new XmlColor(Color.White);
            inner.Stroke = false;
            t.RenderCommands.Add(inner);
            return t;
        }

        #endregion

        #region 流程图工厂

        private static ShapeType CreateFlowStartEnd()
        {
            ShapeType t = CreateBase("起止", "流程图", Color.FromArgb(255, 235, 235), Color.FromArgb(200, 80, 80), 140, 70);
            t.SupportsMembers = false;
            t.RenderCommands.Add(RoundedRectCmd(0, 0, 1, 1, 20f, true));
            return t;
        }

        private static ShapeType CreateFlowProcess()
        {
            ShapeType t = CreateBase("处理", "流程图", Color.FromArgb(230, 245, 255), Color.FromArgb(60, 130, 200), 140, 80);
            t.SupportsMembers = true;
            t.RenderCommands.Add(RectCmd(0, 0, 1, 1, true));
            t.RenderCommands.Add(TextCmd(0, 0, 1, 0.35f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateFlowDecision()
        {
            ShapeType t = CreateBase("判断", "流程图", Color.FromArgb(230, 255, 240), Color.FromArgb(60, 180, 100), 120, 100);
            t.SupportsMembers = false;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.5f, 0f), new PointF(1f, 0.5f),
                new PointF(0.5f, 1f), new PointF(0f, 0.5f)
            }, true));
            return t;
        }

        private static ShapeType CreateFlowDocument()
        {
            ShapeType t = CreateBase("文档", "流程图", Color.FromArgb(255, 250, 240), Color.FromArgb(180, 140, 80), 130, 110);
            t.SupportsMembers = true;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0f, 0f), new PointF(0.8f, 0f),
                new PointF(1f, 0.2f), new PointF(1f, 1f), new PointF(0f, 1f)
            }, true));
            t.RenderCommands.Add(LineCmd(0.8f, 0f, 0.8f, 0.2f, Color.FromArgb(180, 140, 80)));
            t.RenderCommands.Add(LineCmd(0.8f, 0.2f, 1f, 0.2f, Color.FromArgb(180, 140, 80)));
            return t;
        }

        private static ShapeType CreateFlowData()
        {
            ShapeType t = CreateBase("数据", "流程图", Color.FromArgb(240, 255, 250), Color.FromArgb(60, 160, 140), 140, 90);
            t.SupportsMembers = true;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.15f, 0f), new PointF(1f, 0f),
                new PointF(0.85f, 1f), new PointF(0f, 1f)
            }, true));
            return t;
        }

        private static ShapeType CreateFlowPredefined()
        {
            ShapeType t = CreateBase("预定义处理", "流程图", Color.FromArgb(245, 245, 255), Color.FromArgb(100, 100, 180), 140, 80);
            t.SupportsMembers = true;
            t.RenderCommands.Add(RectCmd(0, 0, 1, 1, true));
            t.RenderCommands.Add(RectCmd(0.03f, 0.03f, 0.94f, 0.94f, true));
            t.RenderCommands[1].Fill = false;
            t.RenderCommands.Add(TextCmd(0, 0, 1, 0.35f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateFlowDelay()
        {
            ShapeType t = CreateBase("延迟", "流程图", Color.FromArgb(255, 248, 230), Color.FromArgb(180, 140, 60), 140, 80);
            t.SupportsMembers = false;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0f, 0f), new PointF(0.7f, 0f),
                new PointF(1f, 0.5f), new PointF(0.7f, 1f), new PointF(0f, 1f)
            }, true));
            return t;
        }

        private static ShapeType CreateFlowMerge()
        {
            ShapeType t = CreateBase("合并", "流程图", Color.FromArgb(240, 230, 255), Color.FromArgb(130, 60, 200), 120, 60);
            t.SupportsMembers = false;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0f, 0f), new PointF(1f, 0f), new PointF(0.5f, 1f)
            }, true));
            return t;
        }

        private static ShapeType CreateFlowOffPage()
        {
            ShapeType t = CreateBase("离页连接", "流程图", Color.FromArgb(230, 255, 245), Color.FromArgb(60, 150, 120), 100, 80);
            t.SupportsMembers = false;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0f, 0f), new PointF(1f, 0f),
                new PointF(1f, 0.7f), new PointF(0.5f, 1f), new PointF(0f, 0.7f)
            }, true));
            return t;
        }

        private static ShapeType CreateFlowPreparation()
        {
            ShapeType t = CreateBase("准备", "流程图", Color.FromArgb(255, 240, 250), Color.FromArgb(180, 80, 150), 130, 90);
            t.SupportsMembers = false;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.15f, 0f), new PointF(0.85f, 0f),
                new PointF(1f, 0.5f), new PointF(0.85f, 1f),
                new PointF(0.15f, 1f), new PointF(0f, 0.5f)
            }, true));
            return t;
        }

        private static ShapeType CreateFlowParallel()
        {
            ShapeType t = CreateBase("并行", "流程图", Color.FromArgb(245, 250, 255), Color.FromArgb(80, 130, 180), 120, 90);
            t.SupportsMembers = false;
            t.RenderCommands.Add(RectCmd(0, 0, 1, 1, true));
            t.RenderCommands.Add(LineCmd(0.25f, 0.1f, 0.25f, 0.9f, Color.FromArgb(80, 130, 180)));
            t.RenderCommands.Add(LineCmd(0.75f, 0.1f, 0.75f, 0.9f, Color.FromArgb(80, 130, 180)));
            return t;
        }

        private static ShapeType CreateFlowStorage()
        {
            ShapeType t = CreateBase("存储", "流程图", Color.FromArgb(255, 250, 240), Color.FromArgb(160, 130, 80), 120, 100);
            t.SupportsMembers = false;
            t.RenderCommands.Add(EllipseCmd(0, 0, 1, 0.25f, true));
            t.RenderCommands.Add(RectCmd(0, 0.125f, 1, 0.75f, true));
            t.RenderCommands.Add(EllipseCmd(0, 0.75f, 1, 0.25f, true));
            t.RenderCommands[2].Fill = false;
            return t;
        }

        private static ShapeType CreateFlowDisplay()
        {
            ShapeType t = CreateBase("显示", "流程图", Color.FromArgb(240, 245, 255), Color.FromArgb(80, 120, 180), 140, 100);
            t.SupportsMembers = false;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0f, 0f), new PointF(0.85f, 0f),
                new PointF(1f, 0.15f), new PointF(1f, 0.85f),
                new PointF(0.85f, 1f), new PointF(0f, 1f)
            }, true));
            return t;
        }

        private static ShapeType CreateFlowManual()
        {
            ShapeType t = CreateBase("人工操作", "流程图", Color.FromArgb(255, 245, 235), Color.FromArgb(180, 110, 60), 140, 90);
            t.SupportsMembers = false;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.1f, 0f), new PointF(0.9f, 0f),
                new PointF(1f, 1f), new PointF(0f, 1f)
            }, true));
            return t;
        }

        #endregion

        #region 云原生工厂

        private static ShapeType CreateCloud()
        {
            ShapeType t = CreateBase("云", "云原生", Color.FromArgb(240, 248, 255), Color.FromArgb(0, 120, 215), 160, 100);
            t.SupportsMembers = true;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.20f, 0.65f), new PointF(0.08f, 0.55f),
                new PointF(0.12f, 0.35f), new PointF(0.30f, 0.25f),
                new PointF(0.45f, 0.15f), new PointF(0.65f, 0.15f),
                new PointF(0.80f, 0.25f), new PointF(0.92f, 0.35f),
                new PointF(0.90f, 0.55f), new PointF(0.78f, 0.65f),
                new PointF(0.65f, 0.70f), new PointF(0.35f, 0.70f)
            }, true));
            t.RenderCommands.Add(TextCmd(0.1f, 0.25f, 0.8f, 0.3f, "", "center", 10f, false));
            return t;
        }

        private static ShapeType CreateDatabase()
        {
            ShapeType t = CreateBase("数据库", "云原生", Color.FromArgb(230, 245, 255), Color.FromArgb(60, 100, 180), 130, 120);
            t.SupportsMembers = true;
            float topY = 0.08f;
            float botY = 0.92f;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.10f, topY), new PointF(0.90f, topY),
                new PointF(0.90f, botY), new PointF(0.10f, botY)
            }, true));
            t.RenderCommands[0].Fill = true; t.RenderCommands[0].Stroke = false;
            t.RenderCommands.Add(EllipseCmd(0.10f, 0f, 0.80f, topY * 2, true));
            t.RenderCommands.Add(EllipseCmd(0.10f, botY - topY, 0.80f, topY * 2, true));
            t.RenderCommands[2].Fill = false;
            return t;
        }

        private static ShapeType CreateServer()
        {
            ShapeType t = CreateBase("服务器", "云原生", Color.FromArgb(245, 245, 250), Color.FromArgb(80, 80, 120), 100, 140);
            t.SupportsMembers = false;
            t.RenderCommands.Add(RoundedRectCmd(0, 0, 1, 1, 4f, true));
            // 服务器指示灯条
            for (int i = 0; i < 4; i++)
            {
                float y = 0.15f + i * 0.18f;
                RenderCommand led = RectCmd(0.15f, y, 0.7f, 0.06f, false);
                led.FillColor = new XmlColor(Color.FromArgb(100 + i * 30, 100 + i * 30, 120 + i * 20));
                t.RenderCommands.Add(led);
            }
            return t;
        }

        private static ShapeType CreateContainer()
        {
            ShapeType t = CreateBase("容器", "容器", Color.FromArgb(240, 255, 240), Color.FromArgb(60, 160, 80), 160, 110);
            t.IsContainer = true;
            t.SupportsMembers = true;
            t.RenderCommands.Add(RoundedRectCmd(0.10f, 0.15f, 0.80f, 0.70f, 4f, true));
            t.RenderCommands.Add(RoundedRectCmd(0f, 0.30f, 0.15f, 0.12f, 2f, true));
            t.RenderCommands.Add(RoundedRectCmd(0.85f, 0.55f, 0.15f, 0.12f, 2f, true));
            return t;
        }

        private static ShapeType CreateMicroservice()
        {
            ShapeType t = CreateBase("微服务", "云原生", Color.FromArgb(255, 250, 230), Color.FromArgb(180, 150, 60), 140, 100);
            t.SupportsMembers = false;
            // 四个小方块 + 连线
            t.RenderCommands.Add(RectCmd(0.1f, 0.1f, 0.3f, 0.3f, true));
            t.RenderCommands.Add(RectCmd(0.6f, 0.1f, 0.3f, 0.3f, true));
            t.RenderCommands.Add(RectCmd(0.1f, 0.6f, 0.3f, 0.3f, true));
            t.RenderCommands.Add(RectCmd(0.6f, 0.6f, 0.3f, 0.3f, true));
            t.RenderCommands.Add(LineCmd(0.4f, 0.25f, 0.6f, 0.25f, Color.FromArgb(180, 150, 60)));
            t.RenderCommands.Add(LineCmd(0.25f, 0.4f, 0.25f, 0.6f, Color.FromArgb(180, 150, 60)));
            t.RenderCommands.Add(LineCmd(0.75f, 0.4f, 0.75f, 0.6f, Color.FromArgb(180, 150, 60)));
            t.RenderCommands.Add(LineCmd(0.4f, 0.75f, 0.6f, 0.75f, Color.FromArgb(180, 150, 60)));
            return t;
        }

        private static ShapeType CreateLoadBalancer()
        {
            ShapeType t = CreateBase("负载均衡", "云原生", Color.FromArgb(255, 240, 240), Color.FromArgb(180, 80, 80), 140, 100);
            t.SupportsMembers = false;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.5f, 0f), new PointF(1f, 0.5f), new PointF(0.5f, 1f), new PointF(0f, 0.5f)
            }, true));
            // 叠加横线表示分发
            t.RenderCommands.Add(LineCmd(0.1f, 0.5f, 0.9f, 0.5f, Color.FromArgb(180, 80, 80)));
            t.RenderCommands.Add(LineCmd(0.3f, 0.5f, 0.3f, 0.75f, Color.FromArgb(180, 80, 80)));
            t.RenderCommands.Add(LineCmd(0.7f, 0.5f, 0.7f, 0.75f, Color.FromArgb(180, 80, 80)));
            return t;
        }

        private static ShapeType CreateCache()
        {
            ShapeType t = CreateBase("缓存", "云原生", Color.FromArgb(255, 252, 230), Color.FromArgb(200, 170, 40), 100, 120);
            t.SupportsMembers = false;
            // 闪电形
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.55f, 0f), new PointF(0.35f, 0.4f),
                new PointF(0.55f, 0.4f), new PointF(0.35f, 1f),
                new PointF(0.65f, 0.5f), new PointF(0.45f, 0.5f)
            }, true));
            return t;
        }

        private static ShapeType CreateMessageQueue()
        {
            ShapeType t = CreateBase("消息队列", "云原生", Color.FromArgb(245, 240, 255), Color.FromArgb(130, 80, 180), 120, 100);
            t.SupportsMembers = false;
            // 三个横条表示队列
            for (int i = 0; i < 3; i++)
            {
                float y = 0.15f + i * 0.28f;
                t.RenderCommands.Add(RoundedRectCmd(0.1f, y, 0.8f, 0.18f, 3f, true));
            }
            return t;
        }

        private static ShapeType CreateGateway()
        {
            ShapeType t = CreateBase("网关", "云原生", Color.FromArgb(230, 250, 255), Color.FromArgb(40, 130, 160), 140, 90);
            t.SupportsMembers = false;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0f, 0f), new PointF(1f, 0f),
                new PointF(0.85f, 1f), new PointF(0.15f, 1f)
            }, true));
            t.RenderCommands.Add(LineCmd(0.3f, 0.3f, 0.7f, 0.3f, Color.FromArgb(40, 130, 160)));
            t.RenderCommands.Add(LineCmd(0.3f, 0.55f, 0.7f, 0.55f, Color.FromArgb(40, 130, 160)));
            return t;
        }

        private static ShapeType CreateVolume()
        {
            ShapeType t = CreateBase("存储卷", "云原生", Color.FromArgb(240, 250, 240), Color.FromArgb(80, 140, 80), 120, 100);
            t.SupportsMembers = false;
            t.RenderCommands.Add(RoundedRectCmd(0, 0, 1, 1, 6f, true));
            // 内部折线表示卷
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.15f, 0.6f), new PointF(0.3f, 0.4f),
                new PointF(0.45f, 0.55f), new PointF(0.6f, 0.35f),
                new PointF(0.75f, 0.5f), new PointF(0.85f, 0.4f)
            }, false));
            t.RenderCommands[1].StrokeWidth = 2f;
            return t;
        }

        #endregion

        #region UML 工厂

        private static ShapeType CreateUmlClass()
        {
            ShapeType t = CreateBase("类", "UML", Color.FromArgb(250, 248, 240), Color.FromArgb(100, 100, 100), 180, 160);
            t.SupportsMembers = true;
            t.NameAlignment = NameAlignment.TopCenter;
            t.NameAreaTop = 0.22f;
            t.Resizable = true;

            // 自定义操作：添加成员
            t.CustomActions.Add(ShapeAction.CreateCallback("添加成员", "add_member", "add_member.png"));

            t.RenderCommands.Add(RoundedRectCmd(0, 0, 1, 1, 4f, true));
            t.RenderCommands.Add(TextCmd(0, 0, 1, 0.22f, "", "center", 11f, true));
            t.RenderCommands.Add(LineCmd(0f, 0.22f, 1f, 0.22f, Color.FromArgb(180, 180, 180)));
            return t;
        }

        private static ShapeType CreateUmlActor()
        {
            ShapeType t = CreateBase("参与者", "UML", Color.FromArgb(255, 240, 230), Color.FromArgb(160, 100, 60), 100, 130);
            t.SupportsMembers = false;
            t.RenderCommands.Add(EllipseCmd(0.25f, 0f, 0.50f, 0.28f, true));
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.50f, 0.28f), new PointF(0.85f, 0.48f),
                new PointF(0.95f, 1f), new PointF(0.05f, 1f),
                new PointF(0.15f, 0.48f)
            }, true));
            return t;
        }

        private static ShapeType CreateUmlInterface()
        {
            ShapeType t = CreateBase("接口", "UML", Color.FromArgb(240, 245, 255), Color.FromArgb(80, 120, 180), 100, 80);
            t.SupportsMembers = false;
            t.RenderCommands.Add(EllipseCmd(0.35f, 0f, 0.3f, 0.4f, true));
            t.RenderCommands.Add(LineCmd(0.5f, 0.4f, 0.5f, 1f, Color.FromArgb(80, 120, 180)));
            return t;
        }

        private static ShapeType CreateUmlPackage()
        {
            ShapeType t = CreateBase("包", "UML", Color.FromArgb(250, 250, 245), Color.FromArgb(140, 140, 100), 180, 140);
            t.SupportsMembers = false;
            t.NameAlignment = NameAlignment.TopLeft;
            t.NameAreaTop = 0.18f;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0f, 0f), new PointF(0.4f, 0f),
                new PointF(0.4f, 0.18f), new PointF(1f, 0.18f),
                new PointF(1f, 1f), new PointF(0f, 1f)
            }, true));
            return t;
        }

        private static ShapeType CreateUmlNote()
        {
            ShapeType t = CreateBase("注释", "UML", Color.FromArgb(255, 255, 200), Color.FromArgb(200, 180, 60), 150, 110);
            t.SupportsMembers = false;
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0f, 0f), new PointF(0.85f, 0f),
                new PointF(1f, 0.15f), new PointF(1f, 1f), new PointF(0f, 1f)
            }, true));
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.85f, 0f), new PointF(0.85f, 0.15f), new PointF(1f, 0.15f)
            }, false));
            return t;
        }

        private static ShapeType CreateUmlUseCase()
        {
            ShapeType t = CreateBase("用例", "UML", Color.FromArgb(245, 250, 255), Color.FromArgb(80, 130, 180), 160, 90);
            t.SupportsMembers = false;
            t.RenderCommands.Add(EllipseCmd(0, 0, 1, 1, true));
            return t;
        }

        #endregion

        #region 网络工厂

        private static ShapeType CreateNetFirewall()
        {
            ShapeType t = CreateBase("防火墙", "网络", Color.FromArgb(255, 240, 240), Color.FromArgb(180, 60, 60), 100, 120);
            t.SupportsMembers = false;
            t.RenderCommands.Add(RectCmd(0, 0, 1, 1, true));
            // 砖墙纹理线
            for (int row = 0; row < 4; row++)
            {
                float y = 0.1f + row * 0.22f;
                bool offset = (row % 2 == 1);
                for (int col = 0; col < 3; col++)
                {
                    float x = offset ? 0.15f + col * 0.35f : col * 0.35f;
                    if (x + 0.3f > 1f) continue;
                    RenderCommand brick = RectCmd(x, y, 0.3f, 0.18f, false);
                    brick.FillColor = new XmlColor(Color.FromArgb(200 + row * 10, 60 + row * 5, 60 + row * 5));
                    brick.Stroke = true;
                    brick.StrokeWidth = 0.5f;
                    t.RenderCommands.Add(brick);
                }
            }
            return t;
        }

        private static ShapeType CreateNetRouter()
        {
            ShapeType t = CreateBase("路由器", "网络", Color.FromArgb(240, 250, 255), Color.FromArgb(60, 120, 160), 120, 100);
            t.SupportsMembers = false;
            t.RenderCommands.Add(EllipseCmd(0, 0, 1, 1, true));
            // 箭头
            t.RenderCommands.Add(PolygonCmd(new PointF[] {
                new PointF(0.35f, 0.45f), new PointF(0.55f, 0.45f),
                new PointF(0.55f, 0.35f), new PointF(0.75f, 0.55f),
                new PointF(0.55f, 0.75f), new PointF(0.55f, 0.65f),
                new PointF(0.35f, 0.65f)
            }, false));
            return t;
        }

        private static ShapeType CreateNetSwitch()
        {
            ShapeType t = CreateBase("交换机", "网络", Color.FromArgb(245, 255, 245), Color.FromArgb(60, 140, 80), 140, 80);
            t.SupportsMembers = false;
            t.RenderCommands.Add(RoundedRectCmd(0, 0, 1, 1, 6f, true));
            // 端口小方块
            for (int i = 0; i < 5; i++)
            {
                float x = 0.1f + i * 0.16f;
                RenderCommand port = RectCmd(x, 0.7f, 0.1f, 0.2f, false);
                port.FillColor = new XmlColor(Color.FromArgb(80 + i * 20, 160 + i * 10, 80 + i * 10));
                t.RenderCommands.Add(port);
            }
            return t;
        }

        #endregion
    }
}
