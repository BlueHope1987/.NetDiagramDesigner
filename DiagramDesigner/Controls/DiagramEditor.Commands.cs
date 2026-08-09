using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DiagramDesigner.Config;
using DiagramDesigner.Core;
using DiagramDesigner.Serialization;
using DiagramDesigner.Shapes;

namespace DiagramDesigner.Controls
{
    public partial class DiagramEditor : UserControl
    {
        #region 宿主回调

        /// <summary>
        /// 宿主注册的操作回调。键为 ShapeAction.CallbackName，值为处理函数。
        /// 处理函数签名：void(GraphicShape shape)
        /// </summary>
        private Dictionary<string, EventHandler<ShapeActionEventArgs>> _actionCallbacks =
            new Dictionary<string, EventHandler<ShapeActionEventArgs>>();

        /// <summary>
        /// 注册宿主回调，供 ShapeAction.ActionType = HostCallback 时使用
        /// </summary>
        public void RegisterActionCallback(string callbackName, EventHandler<ShapeActionEventArgs> handler)
        {
            if (string.IsNullOrEmpty(callbackName) || handler == null)
                return;
            _actionCallbacks[callbackName] = handler;
        }

        /// <summary>
        /// 从画布配置同步编辑器 UI 状态
        /// </summary>
        public void ApplyCanvasConfig(CanvasConfig config)
        {
            if (config == null)
                return;

            ShowToolbar = config.ShowToolbar;
            ShowToolboxPanel = config.ShowToolboxPanel;
            ShowMenuStrip = config.ShowMenuStrip;
            ShowStatusBar = config.ShowStatusBar;
            ShowContextMenu = config.ShowContextMenu;
            ShowToolbarText = config.ShowToolbarText;

            // 主题
            if (config.Theme == "Dark")
                Theme = EditorTheme.Dark;
            else
                Theme = EditorTheme.Light;

            // 连线模式
            SetConnectionMode(config.ConnectionMode);

            // 属性面板
            if (config.ShowPropertyPanel)
                ShowPropertyPanel = true;

            // 非设计模式下隐藏编辑时 UI，但保留工具箱（运行时工具可能用到）
            if (!config.DesignMode)
            {
                ShowToolbar = false;
                ShowMenuStrip = false;
                ShowStatusBar = false;
                ShowPropertyPanel = false;
                if (_propertyPanel != null && _propertyPanel.Visible)
                    _propertyPanel.Hide();
            }

            // 工具箱设计模式
            _toolbox.DesignMode = config.DesignMode;

            // 加载自定义图形类型到注册表和工具箱
            if (config.CustomShapeTypes != null)
            {
                foreach (ShapeType st in config.CustomShapeTypes)
                {
                    if (!ShapeTypeRegistry.Instance.Contains(st.Name))
                        ShapeTypeRegistry.Instance.Register(st);
                }
            }

            // 重新加载工具箱（包含新加载的自定义图形）
            _toolbox.ReloadFromRegistry();

            // 应用工具箱可见性过滤
            _toolbox.ApplyVisibilityFilter(config.VisibleToolNames);
        }

        /// <summary>
        /// 从编辑器当前状态构建画布配置
        /// </summary>
        public CanvasConfig BuildCanvasConfig()
        {
            CanvasConfig config = new CanvasConfig();
            config.ShowToolbar = ShowToolbar;
            config.ShowPropertyPanel = ShowPropertyPanel;
            config.ShowToolboxPanel = ShowToolboxPanel;
            config.ShowMenuStrip = ShowMenuStrip;
            config.ShowStatusBar = ShowStatusBar;
            config.ShowContextMenu = ShowContextMenu;
            config.ShowToolbarText = ShowToolbarText;
            config.Theme = (Theme == EditorTheme.Dark) ? "Dark" : "Light";
            config.ConnectionMode = GlobalConfig.Instance.DefaultConnectionMode;
            config.DesignMode = _canvas.Config.DesignMode;

            // 收集工具箱可见图形名
            config.VisibleToolNames = _toolbox.GetVisibleNames();

            // 收集已注册的图形类型名
            foreach (ShapeType st in ShapeTypeRegistry.Instance.GetAllTypes())
            {
                config.ShapeTypeNames.Add(st.Name);
            }

            // 收集自定义图形类型（Category == "自定义"）用于持久化
            config.CustomShapeTypes = new List<ShapeType>();
            foreach (ShapeType st in ShapeTypeRegistry.Instance.GetAllTypes())
            {
                if (st.Category == "自定义")
                    config.CustomShapeTypes.Add(st);
            }

            return config;
        }

        /// <summary>
        /// 注册控件内置的图形操作回调。控件自身负责处理内置图形的默认行为。
        /// </summary>
        private void RegisterBuiltinActions()
        {
            RegisterActionCallback("add_member", new EventHandler<ShapeActionEventArgs>(OnBuiltinAddMember));
        }

        private void OnBuiltinAddMember(object sender, ShapeActionEventArgs e)
        {
            GenericShape shape = e.Shape as GenericShape;
            if (shape == null)
                return;

            ShapeMember m = new ShapeMember();
            m.Name = "NewMember";
            m.TypeName = "string";
            shape.Members.Add(m);
            _canvas.Invalidate();
        }

        #endregion

        #region 菜单事件处理

        private void OnEditDelete(object sender, EventArgs e)
        {
            DeleteSelected();
        }

        private void OnEditSelectAll(object sender, EventArgs e)
        {
            SelectAll();
        }

        private void OnViewGrid(object sender, EventArgs e)
        {
            GlobalConfig.Instance.ShowGrid = !GlobalConfig.Instance.ShowGrid;
            _menuViewGrid.Checked = GlobalConfig.Instance.ShowGrid;
        }

        private void OnViewSnap(object sender, EventArgs e)
        {
            GlobalConfig.Instance.SnapToGrid = !GlobalConfig.Instance.SnapToGrid;
            _menuViewSnap.Checked = GlobalConfig.Instance.SnapToGrid;
        }

        private void OnViewZoomIn(object sender, EventArgs e)
        {
            ZoomIn();
        }

        private void OnViewZoomOut(object sender, EventArgs e)
        {
            ZoomOut();
        }

        private void OnViewReset(object sender, EventArgs e)
        {
            ZoomReset();
        }

        private void OnViewToolbar(object sender, EventArgs e)
        {
            ShowToolbar = !ShowToolbar;
        }

        private void OnViewProperty(object sender, EventArgs e)
        {
            ShowPropertyPanel = !ShowPropertyPanel;
        }

        private void OnViewToolbox(object sender, EventArgs e)
        {
            ShowToolboxPanel = !ShowToolboxPanel;
        }

        private void OnViewContextMenu(object sender, EventArgs e)
        {
            ShowContextMenu = !ShowContextMenu;
        }

        private void OnViewStatusBar(object sender, EventArgs e)
        {
            ShowStatusBar = !ShowStatusBar;
        }

        private void OnViewToolbarText(object sender, EventArgs e)
        {
            ShowToolbarText = !ShowToolbarText;
        }

        private void OnThemeLight(object sender, EventArgs e)
        {
            Theme = EditorTheme.Light;
            OnThemeChanged(EventArgs.Empty);
        }

        private void OnThemeDark(object sender, EventArgs e)
        {
            Theme = EditorTheme.Dark;
            OnThemeChanged(EventArgs.Empty);
        }

        private void OnToolSelect(object sender, EventArgs e)
        {
            SetTool(CanvasTool.Select);
        }

        private void OnToolConnect(object sender, EventArgs e)
        {
            SetTool(CanvasTool.Connect);
        }

        private void OnToolStraight(object sender, EventArgs e)
        {
            SetConnectionMode(ConnectionMode.Straight);
        }

        private void OnToolCurve(object sender, EventArgs e)
        {
            SetConnectionMode(ConnectionMode.Curve);
        }

        private void OnToolOrtho(object sender, EventArgs e)
        {
            SetConnectionMode(ConnectionMode.Orthogonal);
        }

        #endregion

        #region 工具栏事件处理

        private void OnSelectTool(object sender, EventArgs e)
        {
            SetTool(CanvasTool.Select);
        }

        private void OnConnectTool(object sender, EventArgs e)
        {
            SetTool(CanvasTool.Connect);
        }

        private void OnStraightMode(object sender, EventArgs e)
        {
            SetConnectionMode(ConnectionMode.Straight);
        }

        private void OnCurveMode(object sender, EventArgs e)
        {
            SetConnectionMode(ConnectionMode.Curve);
        }

        private void OnOrthoMode(object sender, EventArgs e)
        {
            SetConnectionMode(ConnectionMode.Orthogonal);
        }

        private void OnZoomIn(object sender, EventArgs e)
        {
            ZoomIn();
        }

        private void OnZoomOut(object sender, EventArgs e)
        {
            ZoomOut();
        }

        private void OnZoomFit(object sender, EventArgs e)
        {
            ZoomReset();
        }

        private void OnBringToFront(object sender, EventArgs e)
        {
            BringToFront();
        }

        private void OnSendToBack(object sender, EventArgs e)
        {
            SendToBack();
        }

        private void OnDeleteSelected(object sender, EventArgs e)
        {
            DeleteSelected();
        }

        #endregion

        #region 画布事件处理

        private void OnCanvasSelectionChanged(object sender, EventArgs e)
        {
            List<ShapeBase> shapes = _canvas.Document.GetSelectedShapes();
            List<Connection> conns = _canvas.Document.GetSelectedConnections();

            if (shapes.Count == 1 && conns.Count == 0)
            {
                _propertyGrid.SelectedObject = shapes[0];
            }
            else if (shapes.Count == 0 && conns.Count == 1)
            {
                _propertyGrid.SelectedObject = conns[0];
            }
            else if (shapes.Count > 1)
            {
                object[] arr = new object[shapes.Count];
                for (int i = 0; i < shapes.Count; i++)
                    arr[i] = shapes[i];
                _propertyGrid.SelectedObjects = arr;
            }
            else
            {
                // 无选中时显示画布配置，可在属性面板直接编辑
                _propertyGrid.SelectedObject = _canvas.Config;
            }

            _statusLabel.Text = string.Format("选中: {0} 个实体, {1} 条连线",
                shapes.Count, conns.Count);

            UpdateMenuAvailability();
        }

        private void OnDocumentModified(object sender, EventArgs e)
        {
            _statusLabel.Text = "文档已修改";
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            PointF world = _canvas.ScreenToWorld(e.Location);
            _zoomLabel.Text = string.Format("缩放: {0:0}% | 坐标: ({1:0}, {2:0})",
                _canvas.Zoom * 100, world.X, world.Y);
        }

        private void OnCanvasMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _contextMenuEnabled)
            {
                PointF world = _canvas.ScreenToWorld(e.Location);
                ShapeBase hit = _canvas.Document.HitTestShape(world);

                if (hit != null)
                {
                    // 点击已选中的图形：保持多选状态
                    // 点击未选中的图形：切换选中（Ctrl 按下时追加）
                    if (!hit.Selected)
                    {
                        if ((Control.ModifierKeys & Keys.Control) != Keys.Control)
                            _canvas.Document.ClearSelection();
                        hit.Selected = true;
                        _canvas.OnSelectionChanged();
                        _canvas.Invalidate();
                    }
                }
                else
                {
                    // 右键空白处：清除选中
                    _canvas.Document.ClearSelection();
                    _canvas.OnSelectionChanged();
                    _canvas.Invalidate();
                }

                List<ShapeBase> selectedShapes = _canvas.Document.GetSelectedShapes();

                if (selectedShapes.Count > 0)
                {
                    ShowShapeContextMenu(e.Location);
                }
                else
                {
                    // 非设计模式下不显示空画布配置菜单
                    if (_canvas.Config.DesignMode)
                        ShowCanvasConfigMenu(e.Location);
                }
            }
        }

        #endregion

        private void UpdateMenuAvailability()
        {
            List<ShapeBase> selectedShapes = _canvas.Document.GetSelectedShapes();
            bool hasSelection = (selectedShapes.Count > 0);

            if (_menuEditDelete != null)
                _menuEditDelete.Enabled = hasSelection;
            if (_menuShapeToFront != null)
                _menuShapeToFront.Enabled = hasSelection;
            if (_menuShapeToBack != null)
                _menuShapeToBack.Enabled = hasSelection;
        }

        #region 右键菜单

        private void ShowShapeContextMenu(Point location)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            List<ShapeBase> selectedShapes = _canvas.Document.GetSelectedShapes();
            bool designMode = _canvas.Config.DesignMode;

            // 动态生成：基于 ShapeType.CustomActions
            // 系统行为按 DesignMode 过滤：InlineEditTitle 仅设计时，AddMember/DeleteMember 始终显示
            if (selectedShapes.Count == 1)
            {
                GenericShape gs = selectedShapes[0] as GenericShape;
                if (gs != null)
                {
                    ShapeType st = ShapeTypeRegistry.Instance.GetShapeType(gs.ShapeTypeName);
                    if (st != null && st.CustomActions != null)
                    {
                        int addedCount = 0;
                        foreach (ShapeAction action in st.CustomActions)
                        {
                            // 系统行为过滤：InlineEditTitle 仅在设计时显示
                            if (action.IsSystemBehavior
                                && action.ActionType == ShapeActionType.InlineEditTitle
                                && !designMode)
                                continue;

                            // ZoneConnect 不在菜单中显示（由画布直接处理）
                            if (action.ActionType == ShapeActionType.ZoneConnect)
                                continue;

                            Image icon = LoadIcon(action.IconName);
                            ToolStripMenuItem item = new ToolStripMenuItem(action.Name, icon,
                                new EventHandler(OnShapeAction));
                            item.Tag = action;
                            menu.Items.Add(item);
                            addedCount++;
                        }
                        if (addedCount > 0)
                            menu.Items.Add(new ToolStripSeparator());
                    }
                }
            }

            // 设计模式下才显示删除、置顶/置底、属性等编辑项
            if (designMode)
            {
                ToolStripMenuItem itemDelete = new ToolStripMenuItem("删除",
                    LoadIcon("delete.png"), new EventHandler(OnCtxDelete));
                itemDelete.ShortcutKeyDisplayString = "Delete";
                menu.Items.Add(itemDelete);

                if (selectedShapes.Count > 1)
                {
                    ToolStripMenuItem itemFront = new ToolStripMenuItem("置顶",
                        LoadIcon("to_front.png"), new EventHandler(OnCtxFront));
                    ToolStripMenuItem itemBack = new ToolStripMenuItem("置底",
                        LoadIcon("to_back.png"), new EventHandler(OnCtxBack));
                    menu.Items.Add(itemFront);
                    menu.Items.Add(itemBack);
                }

                menu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem itemProps = new ToolStripMenuItem("属性...",
                    LoadIcon("properties.png"), new EventHandler(OnCtxProps));
                menu.Items.Add(itemProps);
            }

            menu.Show(_canvas, location);
        }

        private void ShowCanvasConfigMenu(Point location)
        {
            // 空画布右键只保留"画布属性"入口，所有配置项已移至属性面板
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem itemConfig = new ToolStripMenuItem("画布属性",
                LoadIcon("properties.png"), new EventHandler(OnCfgShowPropertyPanel));
            menu.Items.Add(itemConfig);
            menu.Show(_canvas, location);
        }

        private void OnCfgShowPropertyPanel(object sender, EventArgs e)
        {
            ShowPropertyPanel = true;
            _propertyGrid.SelectedObject = _canvas.Config;
            _propertyGrid.Focus();
        }

        private void OnShapeAction(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || item.Tag == null)
                return;

            ShapeAction action = item.Tag as ShapeAction;
            if (action == null)
                return;

            List<ShapeBase> shapes = _canvas.Document.GetSelectedShapes();
            if (shapes.Count != 1)
                return;

            GenericShape gs = shapes[0] as GenericShape;
            if (gs == null)
                return;

            // 行为序列：若有子操作则依次执行
            if (action.HasSubActions)
            {
                foreach (ShapeAction sub in action.SubActions)
                    ExecuteSingleAction(gs, sub);
                _canvas.Invalidate();
                return;
            }

            ExecuteSingleAction(gs, action);
            _canvas.Invalidate();
        }

        /// <summary>执行单个行为（非序列）</summary>
        private void ExecuteSingleAction(GenericShape gs, ShapeAction action)
        {
            switch (action.ActionType)
            {
                case ShapeActionType.StateChange:
                    if (!string.IsNullOrEmpty(action.TargetState))
                    {
                        string oldStateName = gs.CurrentStateName;
                        gs.CurrentStateName = action.TargetState;
                        _canvas.Invalidate();
                        RaiseShapeStateChanged(gs, oldStateName, action.TargetState);
                    }
                    break;

                case ShapeActionType.HostCallback:
                    if (_actionCallbacks.ContainsKey(action.CallbackName))
                    {
                        ShapeActionEventArgs args = new ShapeActionEventArgs(gs, action.Name);
                        _actionCallbacks[action.CallbackName](this, args);
                    }
                    break;

                case ShapeActionType.InlineEditTitle:
                    _canvas.StartInlineEditTitle(gs);
                    break;

                case ShapeActionType.AddMember:
                    {
                        ShapeMember m = new ShapeMember();
                        m.Name = "NewMember" + (gs.Members.Count + 1);
                        m.TypeName = "string";
                        gs.Members.Add(m);
                        _canvas.Invalidate();
                        OnDocumentModified(null, null);
                    }
                    break;

                case ShapeActionType.DeleteMember:
                    {
                        if (gs.Members.Count > 0)
                        {
                            gs.Members.RemoveAt(gs.Members.Count - 1);
                            _canvas.Invalidate();
                            OnDocumentModified(null, null);
                        }
                    }
                    break;

                case ShapeActionType.ZoneClick:
                    // 点击行为由画布 ZoneClicked 事件处理，此处不重复触发
                    break;

                case ShapeActionType.ZoneConnect:
                    // 连接行为由画布鼠标事件直接处理
                    break;
            }
        }

        /// <summary>画布 ZoneClicked 事件处理：执行点击区域的系统行为序列</summary>
        private void OnCanvasZoneClicked(object sender, ZoneClickEventArgs e)
        {
            GenericShape gs = e.Shape;
            if (gs == null)
                return;

            // 在系统行为中查找匹配的 ZoneClick 行为
            ShapeType st = ShapeTypeRegistry.Instance.GetShapeType(gs.ShapeTypeName);
            if (st == null || st.CustomActions == null)
                return;

            foreach (ShapeAction action in st.CustomActions)
            {
                if (action.ActionType == ShapeActionType.ZoneClick
                    && action.ZoneName == e.Zone.Name)
                {
                    // 若有子操作则依次执行
                    if (action.HasSubActions)
                    {
                        foreach (ShapeAction sub in action.SubActions)
                            ExecuteSingleAction(gs, sub);
                    }
                    _canvas.Invalidate();
                    break;
                }
            }
        }

        private void OnCtxDelete(object sender, EventArgs e)
        {
            DeleteSelected();
        }

        private void OnCtxFront(object sender, EventArgs e)
        {
            BringToFront();
        }

        private void OnCtxBack(object sender, EventArgs e)
        {
            SendToBack();
        }

        private void OnCtxProps(object sender, EventArgs e)
        {
            ShowPropertyPanel = true;
            _propertyGrid.Focus();
        }

        #endregion
    }
}
