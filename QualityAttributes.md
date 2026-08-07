# Marvin's DiagramDesigner 质量属性说明书

## 1. 可用性

### 1.1 拖放即用

DiagramEditor 作为标准 UserControl，可被 Visual Studio 设计器直接拖放到 WinForms 窗体上。控件库项目（`DiagramDesigner/`）输出类型为 Library，宿主应用通过 ProjectReference 引用后即可在设计器中使用。

DemoApp 中仅需 3 行代码即可完成集成：

```csharp
_editor = new DiagramEditor();
_editor.Dock = DockStyle.Fill;
this.Controls.Add(_editor);
```

### 1.2 延迟初始化

DiagramEditor 的 SplitterDistance 不在构造函数中设置，而是通过 `Layout` 事件延迟初始化。`OnEditorLayout` 在控件首次获得有效尺寸（`Width > 50 && Height > 50`）时才执行 `ApplySplitterDistances`，避免在任何宿主窗体尺寸下因 SplitterDistance 超出范围而抛出异常。

### 1.3 SplitterDistance 安全钳制

`SafeSetSplitterDistance` 对 SplitterDistance 进行多层保护：根据 `FixedPanel` 方向判断计算方式，使用 `Panel1MinSize` / `Panel2MinSize` 计算合法范围并钳制目标值，超出范围时按 `fallbackRatio` 比例回退，整个操作包裹在 try-catch 中静默忽略异常。

### 1.4 初始化顺序无关性

`ConfigureMenu` / `ConfigureHostForm` 均为可选调用。控件在没有调用这些方法的情况下仍可正常工作（画布、工具箱、属性栏、工具栏、状态栏均可使用，仅缺少菜单注入功能）。两个方法的调用顺序也不受限。

---

## 2. 可复用性

### 2.1 控件库与演示应用分离

解决方案采用并行目录结构，分为两个项目：

| 项目 | 目录 | 类型 | 职责 |
|------|------|------|------|
| DiagramDesigner | `DiagramDesigner/` | Library | 纯控件库，不包含任何宿主逻辑 |
| DemoApp | `DemoApp/` | WinExe | 演示应用，展示集成方式和图形类型注册 |

宿主应用通过 ProjectReference 引用控件库 DLL，实现了完全的编译时分离。

### 2.2 DiagramEditor 单对象封装

DiagramEditor 作为 Facade 模式的复合控件，封装了所有子组件（DrawingCanvas、ToolboxPanel、PropertyGrid、ToolStrip、StatusStrip），宿主只需与一个 DiagramEditor 实例交互即可使用全部功能。公共属性 `Canvas`、`Toolbox`、`ToolStrip`、`StatusBar`、`Document` 允许宿主在需要时直接访问子组件。

### 2.3 图形类型从控件剥离

控件库不包含任何图形类型定义。所有图形类型由宿主应用注册。`ShapeTypeRegistry` 是一个通用的存储机制，不与特定图形类型耦合。不同的宿主应用可以注册完全不同的图形集合，而控件库无需任何修改。

### 2.4 菜单注入机制

DiagramEditor 的菜单通过注入机制集成到宿主菜单栏。`FindOrCreateMenu` 按文本查找已有菜单项，不存在则创建新的，不强制宿主创建特定结构的菜单。菜单注入是可选的，不注入不影响控件其他功能。

---

## 3. 可扩展性

### 3.1 RenderCommand 可扩展的图形类型

RenderCommand 使用相对坐标（0~1 范围）描述图形绘制指令。新增图形类型只需定义一组 RenderCommand 即可实现任意外观，无需修改控件库代码。

当前支持的 RenderCommandType：`Rectangle`、`RoundedRect`、`Ellipse`、`Polygon`、`CompoundPolygon`（多路径布尔运算）、`Line`、`Text`、`MemberArea`。新增绘制类型只需在枚举中添加新值，并在 `Execute` 的 switch 中添加对应分支。

`ShapeLibrary` 预定义了大量常用图形类型供宿主直接使用，`ShapeComposer` 支持通过 Union/Subtract/Intersect/Xor 布尔运算组合复合图形。`CompoundPolygon` 类型支持单图形内多条封闭路径的布尔运算，实现更复杂的几何外观。

### 3.2 Zone 分区布局系统

每个图形类型通过 `EnsureDefaultZones()` 自动获得默认标题 Zone，可在编辑器中添加/编辑/删除自定义 Zone：

- **ZoneLayout**：None（无布局）、Title（标题区域）、Stack（垂直堆叠）、Flow（水平流式）、Member（成员列表）。
- **ZoneScaling**：None（随图形等比缩放）、Freeze（冻结边角，缩放时保持绝对像素尺寸，使标题不漂移）。
- Zone 采用归一化坐标（0~1），通过 `RefWidth/RefHeight` 参考尺寸计算绝对像素。
- 状态可拥有独立的 `CustomZones` 列表，实现不同状态下的不同分区布局。

### 3.3 ShapeTypeRegistry 运行时注册

图形类型可在运行时动态注册、注销、清空重建。`ToolboxPanel.ReloadFromRegistry()` 可随时刷新工具箱以反映注册表的最新状态。

### 3.4 类图成员与状态系统

GenericShape 的 `ShapeMember`/`ShapeMemberParameter`/`ShapeState` 构成了完整的类图成员系统：

- **ShapeMember**：支持 5 种成员类型（Property/Method/Event/Constraint/Field）、4 种可见性（Public/Private/Protected/Internal）、静态/抽象修饰符、参数列表，`GetSignature()` 自动生成 UML 风格签名。
- **ShapeState**：支持多状态定义，每种状态有独立的颜色方案，可使用自定义 RenderCommand 呈现不同图形、自定义 Zone 列表呈现不同分区布局，图形可在状态间切换。

### 3.5 连线模式可扩展

ConnectionMode 枚举当前定义了 3 种模式：`Straight`、`Curve`、`Orthogonal`。新增连线模式只需在枚举中添加新值，并在 `GetDrawPoints()` 和 `Draw()` 中添加对应的控制点计算和绘制逻辑。

### 3.6 主题颜色可配置

GlobalConfig 提供了 9 个主题感知的只读颜色属性，通过 EditorTheme 枚举在 Light/Dark 之间切换。MyColorTable 覆盖了 20+ 个 ProfessionalColorTable 属性，所有颜色值均可根据主题需求调整。

---

## 4. 性能

### 4.1 双缓冲绘制

DrawingCanvas 使用 `BufferedGraphicsContext` 实现双缓冲绘制，最大缓冲 4096x4096。在 `OnPaint` 中，所有绘制操作先执行到缓冲区，最后一次性通过 `_bufferedGraphics.Render(e.Graphics)` 输出到屏幕。结合构造函数中的 `ControlStyles.OptimizedDoubleBuffer | AllPaintingInWmPaint` 设置，实现了多层缓冲保护。

### 4.2 GraphicsState Save/Restore

框选矩形绘制和容器内连线裁剪使用 `Graphics.Save()` / `Graphics.Restore()` 保存和恢复图形状态，裁剪操作不会影响后续绘制。

### 4.3 容器裁剪避免冗余绘制

ContainerShape.Draw 使用裁剪区域限制子元素绘制范围，避免子元素绘制溢出到容器外部造成的视觉污染和冗余绘制。连线也采用分层绘制策略：容器内连线在容器绘制阶段裁剪渲染，全局连线阶段跳过已渲染的容器内连线，避免重复绘制。

### 4.4 背景绘制不受缩放影响

背景渐变绘制在 `TranslateTransform` / `ScaleTransform` 之前执行，使用屏幕像素坐标直接绘制。背景效果在任何缩放级别下保持一致，且不参与坐标变换计算，减少不必要的矩阵运算。

### 4.5 控件级优化

- `ToolboxPanel.DoubleBuffered = true`：工具箱面板启用双缓冲。
- `ToolboxPanel.AutoScroll = true`：支持自动滚动，只在可视区域内绘制。
- 图标预生成（`CreateIconFromType`）：工具箱图标在注册时一次性生成，不在每次绘制时重新创建。

---

## 5. 可维护性

### 5.1 partial class 拆分

DrawingCanvas 和 DiagramEditor 均使用 partial class 拆分为两个文件，使每个文件保持在合理的代码量内，职责清晰，便于定位和修改。

| 控件 | 文件 | 职责 |
|------|------|------|
| DrawingCanvas | `DrawingCanvas.cs` | 交互逻辑：鼠标事件、键盘事件、拖放、坐标转换 |
| DrawingCanvas | `DrawingCanvas.Rendering.cs` | 渲染逻辑：OnPaint 管线、DrawBackground、DrawGrid、DrawShapes |
| DiagramEditor | `DiagramEditor.cs` | 组件初始化、布局、公共属性/方法、菜单注入、主题系统 |
| DiagramEditor | `DiagramEditor.Commands.cs` | 菜单/工具栏/画布事件处理、右键菜单、上下文感知菜单 |

### 5.2 GraphicsUtility 消除重复代码

`GraphicsUtility` 静态工具类集中了圆角矩形路径创建和绘制方法，被 ContainerShape.Draw、RenderCommand 等多处复用，避免了重复编写圆角矩形路径构建代码。

### 5.3 关注点分离

代码按 5 个层次组织，每个层次有明确的职责边界：

| 层 | 命名空间 | 职责 |
|---|----------|------|
| Core | `DiagramDesigner.Core` | 基础数据模型和工具 |
| Shapes | `DiagramDesigner.Shapes` | 具体图形实现 |
| Controls | `DiagramDesigner.Controls` | UI 控件与对话框 |
| Config | `DiagramDesigner.Config` | 全局配置 |
| Serialization | `DiagramDesigner.Serialization` | 持久化 |

类间依赖关系清晰：Core 不依赖 Shapes 和 Controls；Shapes 依赖 Core；Controls 依赖 Core、Shapes、Config、Serialization；Config 依赖 Core；Serialization 依赖 Core、Shapes。

### 5.4 C# 2.0 严格兼容

整个代码库严格遵守 .NET Framework 2.0 的语法限制：不使用 `var`、lambda 表达式、LINQ、扩展方法、自动属性、对象/集合初始化器、null 条件运算符（`?.`）、字符串插值（使用 `string.Format`）。这保证了代码可以在 Visual Studio 2005 / .NET Framework 2.0 环境中编译和运行。

---

## 6. 兼容性

### 6.1 .NET Framework 2.0 / VS2005+

- 项目文件使用旧式 .csproj 格式（`ToolsVersion="2.0"`）
- 解决方案文件格式为 Visual Studio 2005（`Format Version 9.00`）
- 目标框架版本：`<TargetFrameworkVersion>v2.0</TargetFrameworkVersion>`

### 6.2 引用依赖

控件库仅依赖 4 个标准 .NET 程序集，无任何第三方依赖：

| 程序集 | 用途 |
|--------|------|
| `System` | 基础类型、集合、IO、事件 |
| `System.Drawing` | Graphics、Pen、Brush、Font、Bitmap 等绘图 API |
| `System.Windows.Forms` | Control、UserControl、PropertyGrid 等控件 |
| `System.Xml` | XmlSerializer（用于文档序列化） |

### 6.3 System.Drawing.Drawing2D API

代码大量使用 `System.Drawing.Drawing2D` 命名空间：`GraphicsPath`、`LinearGradientBrush` / `PathGradientBrush`、坐标变换、`GraphicsState`、`SmoothingMode`、`DashStyle`、`WrapMode`。这些 API 在 .NET Framework 1.1+ 中均已存在，具有极高的向后兼容性。

### 6.4 WinForms 标准控件

DiagramEditor 使用的子控件全部为 WinForms 标准控件（SplitContainer、PropertyGrid、ToolStrip、MenuStrip、StatusStrip、ContextMenuStrip、OpenFileDialog/SaveFileDialog、ToolTip），不依赖任何第三方 UI 组件库。

---

## 7. 健壮性

### 7.1 SplitterDistance 范围钳制

`SafeSetSplitterDistance` 对 SplitterDistance 的设置进行了三层保护：范围计算、值钳制、异常捕获。即使宿主窗体尺寸极小或布局异常，也不会抛出未处理异常。

### 7.2 null 防护

DiagramEditor 中所有菜单项字段、工具栏按钮字段、状态栏标签、宿主 MenuStrip 在使用前均进行 null 检查。这在 `InjectMenus()` 未被调用（宿主未调用 `ConfigureMenu`）的情况下，所有属性设置和事件处理方法不会因 null 引用而崩溃。

### 7.3 容器内连线裁剪

容器内的连线通过裁剪区域绘制，连线不会溢出容器边界，避免了视觉混乱和闪烁。全局连线绘制阶段跳过同一容器内的连线，避免重复绘制和 ZOrder 错乱。

### 7.4 HitTest 精度控制

所有 HitTest 操作都考虑了缩放因子：

- **图形命中**：使用世界坐标直接测试。
- **连线命中**：tolerance 参数为 `6f / _zoom`，在放大时提高精度，缩小时降低精度。
- **ResizeHandle 命中**：tolerance 参数为 `8f / _zoom`，同样适应缩放。

`ShapeBase.HitTest` 会先检查 `Visible` 属性，不可见的图形不参与命中测试。

### 7.5 参数边界保护

多处对输入参数进行了边界保护：

- `ShapeBase.Width` / `Height`：设置值必须 > 10
- `ShapeBase.MinWidth` / `MinHeight`：设置值必须 > 0
- `DrawingCanvas.Zoom`：限制在 `[0.1f, 5.0f]` 范围内
- `GraphicsUtility.CreateRoundedRectPath`：radius 自动钳制不超过 width/height 的一半
- `ResizeHandle` 调整：最终尺寸不低于 MinWidth/MinHeight
- `GenericShape.Name` / `Description`、`Connection.Label`：null 值自动替换为空字符串

### 7.6 反序列化异常处理

`XmlShapeSerializer.ConvertFromData` 中对枚举解析使用了 try-catch 回退。`ConnectionMode` 和 `DashStyle` 均有此保护，即使 XML 数据损坏或版本不匹配，也不会导致反序列化失败。

### 7.7 事件触发安全

所有事件触发均使用 null 检查模式：

```csharp
protected void NotifyChanged()
{
    if (Changed != null)
        Changed(this, EventArgs.Empty);
}
```

这在 C# 2.0 环境中是标准的事件触发模式，避免了没有订阅者时的 NullReferenceException。

---

## 8. 易用性

### 8.1 PropertyGrid 集成

DiagramEditor 内嵌 `PropertyGrid` 控件，选中图形时自动显示其属性。所有公开属性均使用 C# 特性标注（`[Category]`、`[DisplayName]`、`[Description]`），在 PropertyGrid 中提供友好的分类和描述。PropertyGrid 设置为 `PropertySort.CategorizedAlphabetical`，按分类字母序排列。

### 8.2 工具提示

所有工具栏按钮设置了 `ToolTipText`，提供快捷键提示（如 "选择工具 (V)"、"连线工具 (L)"、"删除选中 (Delete)"）。

### 8.3 工具栏图标/文字切换

DiagramEditor 提供 `ShowToolbarText` 属性控制工具栏显示模式：默认仅显示图标，设为 true 时同时显示图标和文字。可通过属性面板或视图菜单切换。

### 8.4 上下文感知菜单

**菜单可用性动态更新**（`UpdateMenuAvailability`）根据当前选中状态动态启用/禁用菜单项：

| 菜单项 | 启用条件 |
|--------|----------|
| 删除 | 有选中的图形或连线 |
| 置顶/置底 | 有选中的图形 |
| 添加成员 | 选中单个 GenericShape 且其 ShapeType.SupportsMembers=true |
| 切换状态 | 选中单个 GenericShape 且有 2 个以上 State |

**右键上下文菜单**（`ShowCanvasContextMenu`）根据上下文动态构建菜单内容，选中单个支持成员的图形时显示"添加成员"/"切换状态"，选中多个图形时显示"置顶"/"置底"，始终显示"删除"和"属性..."。

### 8.5 键盘快捷键

| 快捷键 | 功能 |
|--------|------|
| `Delete` | 删除选中 |
| `Escape` | 取消操作、切换到选择工具 |
| `Ctrl+A` | 全选 |
| `Ctrl+O` | 打开文件 |
| `Ctrl+S` | 保存文件 |
| 鼠标中键拖动 | 平移画布 |
| 鼠标滚轮 | 缩放画布（以鼠标位置为中心） |

### 8.6 状态栏信息反馈

状态栏实时显示操作提示（"就绪"、"新建文档"、选中统计等）和缩放比例、鼠标世界坐标。坐标显示跟随鼠标移动实时更新，缩放显示在每次缩放操作后更新。

### 8.7 工具箱拖放创建

用户可以直接从工具箱拖动图形类型到画布上创建图形实例。新图形放置在鼠标位置中心，如开启网格吸附则对齐到网格。自动检测是否落入容器，若在容器 body 区域内则自动设为容器子元素。新图形自动选中，旧选中清除。
