using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DiagramDesigner.Core;

namespace DiagramDesigner.Controls
{
    public delegate ShapeBase CreateShapeHandler();

    public class ToolboxItem
    {
        private string _name;
        private string _category;
        private Bitmap _icon;
        private CreateShapeHandler _createShape;
        private bool _visible = true;

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public string Category
        {
            get { return _category; }
            set { _category = value; }
        }

        public Bitmap Icon
        {
            get { return _icon; }
            set { _icon = value; }
        }

        public CreateShapeHandler CreateShape
        {
            get { return _createShape; }
            set { _createShape = value; }
        }

        /// <summary>是否在工具箱中可见</summary>
        public bool Visible
        {
            get { return _visible; }
            set { _visible = value; }
        }
    }

    public class ToolboxPanel : Panel
    {
        private List<ToolboxItem> _items = new List<ToolboxItem>();
        private ToolboxItem _selectedItem = null;
        private int _itemHeight = 36;
        private int _iconSize = 28;
        private int _padding = 4;
        private ContextMenuStrip _contextMenu;
        private bool _designMode = true;

        /// <summary>
        /// 设计模式。false 时禁用工具箱编辑相关功能。
        /// 注意：使用 new 关键字显式隐藏 Component.DesignMode。
        /// </summary>
        public new bool DesignMode
        {
            get { return _designMode; }
            set { _designMode = value; }
        }

        public ToolboxPanel()
        {
            this.BackColor = Color.FromArgb(250, 250, 250);
            this.BorderStyle = BorderStyle.FixedSingle;
            this.AutoScroll = true;
            this.DoubleBuffered = true;
            BuildContextMenu();
        }

        private void BuildContextMenu()
        {
            _contextMenu = new ContextMenuStrip();
            ToolStripMenuItem itemRemove = new ToolStripMenuItem("移除工具",
                null, new EventHandler(OnRemoveTool));
            ToolStripMenuItem itemConfig = new ToolStripMenuItem("添加/删除工具...",
                null, new EventHandler(OnToolboxConfig));
            _contextMenu.Items.Add(itemRemove);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(itemConfig);
        }

        private ToolboxItem _contextTarget = null;

        public void ReloadFromRegistry()
        {
            _items.Clear();
            _selectedItem = null;

            List<string> categories = ShapeTypeRegistry.Instance.GetCategories();
            foreach (string category in categories)
            {
                List<ShapeType> types = ShapeTypeRegistry.Instance.GetTypesByCategory(category);
                foreach (ShapeType type in types)
                {
                    ToolboxItem item = new ToolboxItem();
                    item.Name = type.Name;
                    item.Category = type.Category;
                    item.CreateShape = new CreateShapeHandler(type.CreateInstance);
                    item.Icon = CreateIconFromType(type);
                    _items.Add(item);
                }
            }

            int totalHeight = CalculateContentHeight();
            this.AutoScrollMinSize = new Size(0, totalHeight);
            this.Invalidate(true);
            this.Update();
        }

        /// <summary>
        /// 根据可见图形名列表过滤工具箱显示
        /// </summary>
        public void ApplyVisibilityFilter(List<string> visibleNames)
        {
            if (visibleNames == null || visibleNames.Count == 0)
            {
                // 空/null 表示全部显示
                foreach (ToolboxItem item in _items)
                    item.Visible = true;
            }
            else
            {
                foreach (ToolboxItem item in _items)
                    item.Visible = visibleNames.Contains(item.Name);
            }
            int totalHeight = CalculateContentHeight();
            this.AutoScrollMinSize = new Size(0, totalHeight);
            this.Invalidate(true);
        }

        /// <summary>
        /// 获取当前工具箱中可见的图形名列表
        /// </summary>
        public List<string> GetVisibleNames()
        {
            List<string> names = new List<string>();
            foreach (ToolboxItem item in _items)
            {
                if (item.Visible)
                    names.Add(item.Name);
            }
            return names;
        }

        private int CalculateContentHeight()
        {
            if (_items.Count == 0)
                return 80;

            int total = _padding;
            string currentCategory = null;
            foreach (ToolboxItem item in _items)
            {
                if (!item.Visible) continue;
                if (item.Category != currentCategory)
                {
                    currentCategory = item.Category;
                    total += _padding + 20;
                }
                total += _itemHeight + 2;
            }
            return total + _padding;
        }

        public void AddItem(ToolboxItem item)
        {
            if (item == null)
                return;
            if (item.Icon == null)
                item.Icon = CreateDefaultIcon(item);
            _items.Add(item);
            Invalidate();
        }

        public void ClearItems()
        {
            _items.Clear();
            _selectedItem = null;
            Invalidate();
        }

        public ToolboxItem SelectedItem { get { return _selectedItem; } }

        /// <summary>
        /// 获取所有工具箱项（包括不可见的）
        /// </summary>
        public List<ToolboxItem> AllItems { get { return _items; } }

        public event EventHandler ItemSelected;
        public event EventHandler ToolboxChanged;

        protected virtual void OnItemSelected(ToolboxItem item)
        {
            if (ItemSelected != null)
                ItemSelected(this, EventArgs.Empty);
        }

        protected virtual void OnToolboxChanged()
        {
            if (ToolboxChanged != null)
                ToolboxChanged(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (_items.Count == 0)
            {
                using (Font font = new Font("Microsoft YaHei", 9f, FontStyle.Regular))
                using (Brush brush = new SolidBrush(Color.FromArgb(160, 160, 160)))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("工具箱为空\n请在 InitializeShapeTypes 中注册图形类型", font, brush, this.ClientRectangle, sf);
                }
                return;
            }

            float y = _padding + AutoScrollPosition.Y;
            string currentCategory = null;

            foreach (ToolboxItem item in _items)
            {
                if (!item.Visible) continue;

                if (item.Category != currentCategory)
                {
                    currentCategory = item.Category;
                    y += _padding;
                    using (Font font = new Font("Microsoft YaHei", 9f, FontStyle.Bold))
                    using (Brush brush = new SolidBrush(Color.FromArgb(80, 80, 80)))
                    {
                        g.DrawString(currentCategory, font, brush, _padding, y);
                    }
                    y += 20;
                }

                RectangleF itemRect = new RectangleF(_padding, y, ClientSize.Width - _padding * 2, _itemHeight);
                bool isSelected = (_selectedItem == item);
                bool isHovered = itemRect.Contains(PointToClient(Cursor.Position));

                if (isSelected)
                {
                    using (Brush brush = new SolidBrush(Color.FromArgb(220, 240, 255)))
                    {
                        g.FillRectangle(brush, itemRect);
                    }
                    using (Pen pen = new Pen(Color.FromArgb(0, 120, 215)))
                    {
                        g.DrawRectangle(pen, itemRect.X, itemRect.Y, itemRect.Width, itemRect.Height);
                    }
                }
                else if (isHovered)
                {
                    using (Brush brush = new SolidBrush(Color.FromArgb(240, 248, 255)))
                    {
                        g.FillRectangle(brush, itemRect);
                    }
                }

                if (item.Icon != null)
                {
                    g.DrawImage(item.Icon, itemRect.X + 4, itemRect.Y + (_itemHeight - _iconSize) / 2, _iconSize, _iconSize);
                }

                using (Font font = new Font("Microsoft YaHei", 9f, FontStyle.Regular))
                using (Brush brush = new SolidBrush(Color.FromArgb(40, 40, 40)))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Center;

                    RectangleF textRect = itemRect;
                    textRect.X += _iconSize + 10;
                    textRect.Width -= _iconSize + 14;
                    g.DrawString(item.Name, font, brush, textRect, sf);
                }

                y += _itemHeight + 2;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Right)
            {
                // 非设计模式下不显示编辑菜单
                if (!_designMode)
                    return;

                ToolboxItem hit = HitTestItem(e.Location);
                _contextTarget = hit;
                _contextMenu.Items[0].Enabled = (hit != null); // 移除工具仅当点中工具时可用
                _contextMenu.Show(this, e.Location);
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            ToolboxItem clicked = HitTestItem(e.Location);
            if (clicked != null && clicked.Visible)
            {
                _selectedItem = clicked;
                OnItemSelected(clicked);
                Invalidate();
                DoDragDrop(clicked, DragDropEffects.Copy);
            }
        }

        private ToolboxItem HitTestItem(Point location)
        {
            float y = _padding + AutoScrollPosition.Y;
            string currentCategory = null;

            foreach (ToolboxItem item in _items)
            {
                if (!item.Visible) continue;
                if (item.Category != currentCategory)
                {
                    currentCategory = item.Category;
                    y += _padding + 20;
                }

                RectangleF itemRect = new RectangleF(_padding, y, ClientSize.Width - _padding * 2, _itemHeight);
                if (itemRect.Contains(location))
                    return item;

                y += _itemHeight + 2;
            }
            return null;
        }

        private void OnRemoveTool(object sender, EventArgs e)
        {
            if (_contextTarget != null)
            {
                // 自定义工具同时从注册表移除
                if (_contextTarget.Category == "自定义")
                {
                    ShapeTypeRegistry.Instance.Unregister(_contextTarget.Name);
                }
                _items.Remove(_contextTarget);
                if (_selectedItem == _contextTarget)
                    _selectedItem = null;
                _contextTarget = null;
                int totalHeight = CalculateContentHeight();
                this.AutoScrollMinSize = new Size(0, totalHeight);
                this.Invalidate(true);
                OnToolboxChanged();
            }
        }

        private void OnToolboxConfig(object sender, EventArgs e)
        {
            using (ToolboxConfigDialog dlg = new ToolboxConfigDialog(_items, ShapeTypeRegistry.Instance))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _items.Clear();
                    foreach (ToolboxItem item in dlg.ResultItems)
                    {
                        // 为新增项生成图标
                        if (item.Icon == null)
                            item.Icon = CreateDefaultIcon(item);
                        _items.Add(item);
                    }
                    if (!_items.Contains(_selectedItem))
                        _selectedItem = null;
                    int totalHeight = CalculateContentHeight();
                    this.AutoScrollMinSize = new Size(0, totalHeight);
                    this.Invalidate(true);
                    OnToolboxChanged();
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Invalidate();
        }

        private Bitmap CreateIconFromType(ShapeType shapeType)
        {
            Bitmap bmp = new Bitmap(_iconSize, _iconSize);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                try
                {
                    ShapeBase shape = shapeType.CreateInstance();
                    shape.Bounds = new RectangleF(2, 2, _iconSize - 4, _iconSize - 4);
                    shape.Draw(g, 1.0f);
                }
                catch
                {
                    using (Brush brush = new SolidBrush(Color.FromArgb(200, 200, 200)))
                    using (Pen pen = new Pen(Color.FromArgb(120, 120, 120)))
                    {
                        g.FillRectangle(brush, 2, 2, _iconSize - 4, _iconSize - 4);
                        g.DrawRectangle(pen, 2, 2, _iconSize - 4, _iconSize - 4);
                    }
                }
            }
            return bmp;
        }

        private Bitmap CreateDefaultIcon(ToolboxItem item)
        {
            Bitmap bmp = new Bitmap(_iconSize, _iconSize);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                Rectangle rect = new Rectangle(2, 2, _iconSize - 4, _iconSize - 4);

                if (item.CreateShape != null)
                {
                    try
                    {
                        ShapeBase shape = item.CreateShape();
                        shape.Bounds = new RectangleF(2, 2, _iconSize - 4, _iconSize - 4);
                        shape.Draw(g, 1.0f);
                    }
                    catch
                    {
                        DrawFallbackIcon(g, rect);
                    }
                }
                else
                {
                    DrawFallbackIcon(g, rect);
                }
            }
            return bmp;
        }

        private void DrawFallbackIcon(Graphics g, Rectangle rect)
        {
            using (Brush brush = new SolidBrush(Color.FromArgb(200, 200, 200)))
            using (Pen pen = new Pen(Color.FromArgb(120, 120, 120)))
            {
                g.FillRectangle(brush, rect);
                g.DrawRectangle(pen, rect);
            }
        }
    }
}
