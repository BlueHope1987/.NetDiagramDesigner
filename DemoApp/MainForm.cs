using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DiagramDesigner.Config;
using DiagramDesigner.Controls;
using DiagramDesigner.Core;
using DiagramDesigner.Serialization;
using DiagramDesigner.Shapes;

namespace DemoApp
{
    public class MainForm : Form
    {
        private DiagramEditor _editor;
        private MenuStrip _menuStrip;
        private string _currentFilePath = "";

        public MainForm()
        {
            // 1. 先创建编辑器控件（Dock.Fill，Z序最低）
            _editor = new DiagramEditor();
            _editor.Dock = DockStyle.Fill;
            this.Controls.Add(_editor);

            // 2. 后创建宿主菜单栏（Dock.Top，Z序更高，抢占顶部）
            _menuStrip = new MenuStrip();
            this.MainMenuStrip = _menuStrip;
            this.Controls.Add(_menuStrip);

            // 3. 宿主自行构建文件菜单
            BuildFileMenu();

            // 4. 控件会自动注入其余菜单（编辑/视图/工具/图形）

            this.Text = "Marvin's DiagramDesigner - Demo";
            this.Size = new Size(1400, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = SystemIcons.Application;

            InitializeShapeTypes();
            WireEditorEvents();

            // ===== 示范：两种自定义图形定义方式 =====

            // 方式1：构建画布文档并传入（适用于从文件/网络加载完整配置）
            // CanvasConfig runtimeConfig = new CanvasConfig();
            // runtimeConfig.ShowToolbar = false;
            // runtimeConfig.ShowToolboxPanel = false;
            // runtimeConfig.ShowStatusBar = true;
            // runtimeConfig.ShowPropertyPanel = false;
            // runtimeConfig.Theme = "Dark";
            // DrawingDocument doc = new DrawingDocument();
            // doc.AddShape(ShapeLibrary.Cloud.CreateInstance());
            // _editor.SetDocument(doc, runtimeConfig);

            // 方式2：方法调用（适用于运行时动态配置）
            // _editor.Canvas.Config.ShowToolbar = true;
            // _editor.Canvas.Config.ShowToolboxPanel = true;
            // _editor.Canvas.Config.ShowStatusBar = true;
            // _editor.Canvas.Config.Theme = "Light";
            // _editor.ApplyCanvasConfig(_editor.Canvas.Config);

            InitializeSampleData();
        }

        #region File Menu

        private void BuildFileMenu()
        {
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("文件(&F)");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("新建", null, new EventHandler(OnFileNew)));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("打开...", null, new EventHandler(OnFileOpen), Keys.Control | Keys.O));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("保存", null, new EventHandler(OnFileSave), Keys.Control | Keys.S));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("另存为...", null, new EventHandler(OnFileSaveAs)));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("退出", null, new EventHandler(OnFileExit)));
            _menuStrip.Items.Add(fileMenu);
        }

        private void OnFileNew(object sender, EventArgs e)
        {
            _editor.ClearDocument();
            _currentFilePath = "";
            // 新建文档时复位为编辑时默认配置
            ResetToEditMode();
            UpdateTitle();
        }

        /// <summary>
        /// 复位编辑器到编辑时默认配置
        /// </summary>
        private void ResetToEditMode()
        {
            CanvasConfig editConfig = new CanvasConfig();
            editConfig.ShowToolbar = true;
            editConfig.ShowToolboxPanel = true;
            editConfig.ShowStatusBar = true;
            editConfig.ShowPropertyPanel = false;
            editConfig.ShowMenuStrip = true;
            editConfig.ShowContextMenu = true;
            editConfig.ShowToolbarText = false;
            editConfig.Theme = "Light";
            editConfig.DesignMode = true;
            _editor.ApplyCanvasConfig(editConfig);
            _editor.Canvas.Config = editConfig;
        }

        private void OnFileOpen(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "XML 文件 (*.xml)|*.xml|所有文件 (*.*)|*.*";
                dlg.DefaultExt = "xml";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    DrawingDocument doc = XmlShapeSerializer.Load(dlg.FileName);
                    // 加载文档，配置随文档自动应用
                    _editor.SetDocument(doc);
                    _currentFilePath = dlg.FileName;
                    UpdateTitle();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("打开失败: " + ex.Message, "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnFileSave(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                OnFileSaveAs(sender, e);
                return;
            }
            try
            {
                // 保存前将当前编辑器配置同步到文档
                _editor.GetDocument().Config = _editor.BuildCanvasConfig();
                XmlShapeSerializer.Save(_currentFilePath, _editor.GetDocument());
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败: " + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnFileSaveAs(object sender, EventArgs e)
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "XML 文件 (*.xml)|*.xml|所有文件 (*.*)|*.*";
                dlg.DefaultExt = "xml";
                if (!string.IsNullOrEmpty(_currentFilePath))
                    dlg.FileName = _currentFilePath;

                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    // 保存前将当前编辑器配置同步到文档
                    _editor.GetDocument().Config = _editor.BuildCanvasConfig();
                    XmlShapeSerializer.Save(dlg.FileName, _editor.GetDocument());
                    _currentFilePath = dlg.FileName;
                    UpdateTitle();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("保存失败: " + ex.Message, "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnFileExit(object sender, EventArgs e)
        {
            this.Close();
        }

        private void UpdateTitle()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
                this.Text = "Marvin's DiagramDesigner - Demo";
            else
                this.Text = "Marvin's DiagramDesigner - " + Path.GetFileName(_currentFilePath);
        }

        #endregion

        #region Shape Type Registration

        private void InitializeShapeTypes()
        {
            ShapeTypeRegistry.Instance.Clear();

            // 方式1：一键注册全部内置图形（44+ 图形）
            ShapeLibrary.RegisterAll();

            // 方式2：也可按需选择性注册
            // ShapeLibrary.RegisterCategory("基本图形");
            // ShapeLibrary.RegisterCategory("流程图");
            // ShapeLibrary.RegisterCategory("云原生");
            // ShapeLibrary.RegisterCategory("UML");
            // ShapeLibrary.RegisterCategory("网络");
            // ShapeLibrary.Register(
            //     ShapeLibrary.Rectangle,
            //     ShapeLibrary.Ellipse,
            //     ShapeLibrary.Cloud,
            //     ShapeLibrary.Database
            // );

            // 方式3：组合自定义图形（视觉叠加）
            // ShapeType custom = ShapeComposer.Union("集群", "自定义",
            //     ShapeLibrary.Cloud, ShapeLibrary.Server);
            // ShapeTypeRegistry.Instance.Register(custom);

            _editor.Toolbox.ReloadFromRegistry();
        }

        #endregion

        #region Editor Events

        /// <summary>
        /// 订阅编辑器事件，示范宿主如何响应图形/连线的增删与状态切换。
        /// 实际应用中宿主可据此同步外部数据源、记录操作日志或触发业务逻辑。
        /// </summary>
        private void WireEditorEvents()
        {
            _editor.ShapeAdded += new EventHandler<ShapeEventArgs>(OnEditorShapeAdded);
            _editor.ShapeDeleted += new EventHandler<ShapeEventArgs>(OnEditorShapeDeleted);
            _editor.ConnectionAdded += new EventHandler<ConnectionEventArgs>(OnEditorConnectionAdded);
            _editor.ConnectionDeleted += new EventHandler<ConnectionEventArgs>(OnEditorConnectionDeleted);
            _editor.ShapeStateChanged += new EventHandler<ShapeStateChangedEventArgs>(OnEditorShapeStateChanged);
        }

        private void OnEditorShapeAdded(object sender, ShapeEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(
                "[Event] 图形添加: " + e.ShapeName + " (类型: " + e.ShapeTypeName + ")");
        }

        private void OnEditorShapeDeleted(object sender, ShapeEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(
                "[Event] 图形删除: " + e.ShapeName);
        }

        private void OnEditorConnectionAdded(object sender, ConnectionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(
                "[Event] 连线创建: " + e.FromShapeName + " -> " + e.ToShapeName);
        }

        private void OnEditorConnectionDeleted(object sender, ConnectionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(
                "[Event] 连线删除: " + e.FromShapeName + " -> " + e.ToShapeName);
        }

        private void OnEditorShapeStateChanged(object sender, ShapeStateChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(
                "[Event] 状态切换: " + e.Shape.Name +
                " (" + e.OldStateName + " -> " + e.NewStateName + ")");
        }

        #endregion

        #region Sample Data

        private void InitializeSampleData()
        {
            ContainerShape container = new ContainerShape();
            container.Name = "部署环境";
            container.HeaderText = "生产环境集群";
            container.X = 100;
            container.Y = 80;
            container.Width = 500;
            container.Height = 350;
            container.FillColor = Color.FromArgb(250, 250, 252);
            container.BorderColor = Color.FromArgb(100, 100, 130);

            GenericShape rect1 = new GenericShape();
            rect1.ShapeTypeName = "矩形";
            rect1.Name = "API网关";
            rect1.X = 140;
            rect1.Y = 140;
            rect1.FillColor = Color.FromArgb(230, 245, 255);
            rect1.BorderColor = Color.FromArgb(60, 130, 200);

            GenericShape rect2 = new GenericShape();
            rect2.ShapeTypeName = "矩形";
            rect2.Name = "用户服务";
            rect2.X = 340;
            rect2.Y = 140;
            rect2.FillColor = Color.FromArgb(230, 255, 240);
            rect2.BorderColor = Color.FromArgb(60, 180, 100);

            GenericShape ellipse1 = new GenericShape();
            ellipse1.ShapeTypeName = "椭圆";
            ellipse1.Name = "数据库";
            ellipse1.X = 340;
            ellipse1.Y = 260;
            ellipse1.FillColor = Color.FromArgb(255, 240, 230);
            ellipse1.BorderColor = Color.FromArgb(200, 130, 60);

            GenericShape diamond1 = new GenericShape();
            diamond1.ShapeTypeName = "菱形";
            diamond1.Name = "负载均衡";
            diamond1.X = 160;
            diamond1.Y = 260;
            diamond1.FillColor = Color.FromArgb(255, 240, 255);
            diamond1.BorderColor = Color.FromArgb(180, 60, 180);

            _editor.Document.AddShape(container);
            _editor.Document.AddShape(rect1);
            _editor.Document.AddShape(rect2);
            _editor.Document.AddShape(ellipse1);
            _editor.Document.AddShape(diamond1);

            container.AddChild(rect1);
            container.AddChild(rect2);
            container.AddChild(ellipse1);
            container.AddChild(diamond1);

            GenericShape classShape = new GenericShape();
            classShape.ShapeTypeName = "类";
            classShape.Name = "UserService";
            classShape.X = 600;
            classShape.Y = 120;
            classShape.Width = 180;
            classShape.Height = 160;

            ShapeMember m1 = new ShapeMember();
            m1.MemberType = MemberType.Property;
            m1.Name = "Id";
            m1.TypeName = "Guid";
            m1.Visibility = Visibility.Public;
            classShape.Members.Add(m1);

            ShapeMember m2 = new ShapeMember();
            m2.MemberType = MemberType.Property;
            m2.Name = "Name";
            m2.TypeName = "string";
            m2.Visibility = Visibility.Public;
            classShape.Members.Add(m2);

            ShapeMember m3 = new ShapeMember();
            m3.MemberType = MemberType.Method;
            m3.Name = "GetUser";
            m3.TypeName = "User";
            m3.Visibility = Visibility.Public;
            ShapeMemberParameter p1 = new ShapeMemberParameter();
            p1.Name = "id";
            p1.TypeName = "Guid";
            m3.Parameters.Add(p1);
            classShape.Members.Add(m3);

            ShapeMember m4 = new ShapeMember();
            m4.MemberType = MemberType.Method;
            m4.Name = "Save";
            m4.TypeName = "bool";
            m4.Visibility = Visibility.Public;
            classShape.Members.Add(m4);

            ShapeState normalState = new ShapeState();
            normalState.Name = "Normal";
            normalState.FillColor = new XmlColor(Color.FromArgb(250, 248, 240));
            normalState.BorderColor = new XmlColor(Color.FromArgb(100, 100, 100));
            normalState.TextColor = new XmlColor(Color.FromArgb(40, 40, 40));
            classShape.AddState(normalState);

            ShapeState warningState = new ShapeState();
            warningState.Name = "Warning";
            warningState.FillColor = new XmlColor(Color.FromArgb(255, 248, 220));
            warningState.BorderColor = new XmlColor(Color.FromArgb(255, 140, 0));
            warningState.TextColor = new XmlColor(Color.FromArgb(180, 90, 0));
            classShape.AddState(warningState);

            ShapeState errorState = new ShapeState();
            errorState.Name = "Error";
            errorState.FillColor = new XmlColor(Color.FromArgb(255, 235, 235));
            errorState.BorderColor = new XmlColor(Color.FromArgb(220, 50, 50));
            errorState.TextColor = new XmlColor(Color.FromArgb(180, 30, 30));
            classShape.AddState(errorState);

            _editor.Document.AddShape(classShape);

            Connection conn1 = new Connection();
            conn1.FromShape = rect1;
            conn1.ToShape = rect2;
            conn1.Mode = ConnectionMode.Straight;
            conn1.Label = "REST";

            Connection conn2 = new Connection();
            conn2.FromShape = rect2;
            conn2.ToShape = ellipse1;
            conn2.Mode = ConnectionMode.Orthogonal;
            conn2.Label = "SQL";

            Connection conn3 = new Connection();
            conn3.FromShape = diamond1;
            conn3.ToShape = rect1;
            conn3.Mode = ConnectionMode.Curve;

            Connection conn4 = new Connection();
            conn4.FromShape = rect2;
            conn4.ToShape = classShape;
            conn4.Mode = ConnectionMode.Straight;
            conn4.Label = "gRPC";

            _editor.Document.AddConnection(conn1);
            _editor.Document.AddConnection(conn2);
            _editor.Document.AddConnection(conn3);
            _editor.Document.AddConnection(conn4);

            _editor.Canvas.Invalidate();
        }

        #endregion
    }
}
