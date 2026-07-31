# Marvin's DiagramDesigner 架构规格说明书

## 1. 系统概述

### 1.1 项目名称

Marvin's DiagramDesigner

### 1.2 项目目标

Marvin's DiagramDesigner 是一个可复用的 WinForms 可视化图表设计器控件库，兼容 .NET Framework 2.0（Visual Studio 2005+）。它提供完整的图形绘制、连线、容器嵌套、序列化等功能，可被宿主应用程序直接拖放使用或编程集成，适用于构建各类可视化图表编辑器（架构图、流程图、UML 类图等）。

所有代码位于 `DiagramDesigner` 命名空间下，按职责划分为 Core、Shapes、Controls、Config、Serialization 五个子命名空间。

### 1.3 解决的问题

- **可视化设计器的重复开发**：提供开箱即用的画布、工具箱、属性栏、工具栏、状态栏、菜单等完整 UI 组件，避免每个项目从零搭建。
- **图形类型的灵活扩展**：通过 RenderCommand 数据驱动 + ShapeTypeRegistry 运行时注册机制，支持不修改控件库代码即可新增图形类型。
- **控件与宿主的解耦**：控件库不包含任何图形类型定义，图形注册由宿主完成；菜单通过注入机制集成，不强制宿主创建特定结构的菜单。
- **低版本 .NET 兼容**：严格使用 .NET 2.0 API（不使用 var、lambda、LINQ 等高版本语法），可在老旧环境中部署。

---

## 2. 解决方案结构

### 2.1 目录结构

解决方案采用控件库与演示应用并行的目录结构：

```
MarvinDiagramDesigner/
  ├── DiagramDesigner.sln          # 解决方案文件（VS2005 格式）
  ├── ArchitectureSpecification.md
  ├── QualityAttributes.md
  ├── README.md
  ├── DiagramDesigner/             # 控件库项目
  │   ├── DiagramDesigner.csproj   # OutputType=Library, TargetFrameworkVersion=v2.0
  │   ├── Core/                    # 基础数据模型与工具
  │   ├── Shapes/                  # 具体图形实现
  │   ├── Controls/                # UI 控件与对话框
  │   ├── Config/                  # 全局配置
  │   ├── Serialization/           # 持久化
  │   └── Icons/                   # 嵌入资源图标
  └── DemoApp/                     # 演示应用项目
      ├── DemoApp.csproj           # OutputType=WinExe, TargetFrameworkVersion=v2.0
      ├── Program.cs
      └── MainForm.cs
```

### 2.2 解决方案组成

| 项目 | 类型 | 输出 | 说明 |
|------|------|------|------|
| DiagramDesigner | Library | DiagramDesigner.dll | 控件库，包含所有核心、控件、序列化代码 |
| DemoApp | WinExe | DemoApp.exe | 演示应用，展示宿主集成与图形类型注册 |

### 2.3 解决方案文件

| 文件 | 说明 |
|------|------|
| `DiagramDesigner.sln` | Visual Studio 2005 格式解决方案文件（Format Version 9.00） |
| `DiagramDesigner/DiagramDesigner.csproj` | 控件库项目文件（ToolsVersion=2.0, TargetFrameworkVersion=v2.0） |
| `DemoApp/DemoApp.csproj` | 演示应用项目文件，通过 ProjectReference 引用控件库 |

### 2.4 源文件清单

#### Core 层（`DiagramDesigner/Core/`）

| 文件 | 职责 |
|------|------|
| `ShapeBase.cs` | 所有图形的抽象基类，定义属性、HitTest、Clone、选中绘制、ResizeHandle |
| `Connection.cs` | 连线模型，支持 Straight/Curve/Orthogonal 三种模式，含标签、箭头绘制 |
| `DrawingDocument.cs` | 文档模型，管理形状集合与连线集合，提供选中管理、ZOrder 排序、HitTest |
| `RenderCommand.cs` | 渲染命令，7 种类型（Rectangle/Ellipse/RoundedRect/Polygon/Line/Text/MemberArea），数据驱动绘制 |
| `ShapeType.cs` | 图形类型描述，包含名称、分类、RenderCommand 列表、默认尺寸/颜色、状态列表，可创建实例 |
| `ShapeTypeRegistry.cs` | 图形类型运行时注册表（单例），提供按名称/分类查询、注册/注销功能 |
| `ShapeMember.cs` | 类图成员定义（Property/Method/Event/Constraint/Field），含可见性、签名生成 |
| `ShapeMemberParameter.cs` | 成员参数定义（名称、类型、默认值） |
| `ShapeState.cs` | 图形状态定义，包含状态名及各部位颜色（填充/边框/文字/标题栏） |
| `ShapeAction.cs` | 图形行为定义，支持状态切换与宿主回调两种类型，挂载到右键菜单 |
| `ShapeLibrary.cs` | 内置图形库，预定义常用图形类型，宿主可一行代码批量注册 |
| `ShapeComposer.cs` | 图形组合器，支持将多个图形叠加/差集/交集组合为复合图形 |
| `CanvasConfig.cs` | 画布配置，存储编辑器 UI 状态与工具配置，随文档序列化 |
| `GraphicsUtility.cs` | 公共图形工具类，提供圆角矩形路径创建与绘制 |
| `XmlColor.cs` | 可序列化颜色包装类，使用 int ARGB 值替代 Color 以支持 XML 序列化 |

#### Shapes 层（`DiagramDesigner/Shapes/`）

| 文件 | 职责 |
|------|------|
| `GenericShape.cs` | 通用图形，通过 ShapeTypeRegistry 查找 RenderCommand 列表动态绘制，支持成员管理、状态切换 |
| `ContainerShape.cs` | 容器图形，支持嵌套子元素、HeaderHeight 标题栏、裁剪绘制子图形、联动移动子元素 |

#### Controls 层（`DiagramDesigner/Controls/`）

| 文件 | 职责 |
|------|------|
| `DrawingCanvas.cs` | 画布控件（交互部分）：双缓冲、缩放/平移、选择/拖拽/连线/框选/调整尺寸、键盘快捷键、拖放接收 |
| `DrawingCanvas.Rendering.cs` | 画布控件（渲染部分）：OnPaint 渲染管线、背景渐变、网格、形状绘制、连线绘制、橡皮筋线、框选矩形 |
| `ToolboxPanel.cs` | 工具箱面板：按分类显示已注册图形类型，支持拖放创建图形实例，自动生成图标 |
| `DiagramEditor.cs` | 复合控件（Facade）：封装画布+工具箱+属性栏+工具栏+状态栏+右键菜单+菜单注入+主题系统 |
| `DiagramEditor.Commands.cs` | 复合控件的命令/事件处理部分：文件操作、编辑操作、视图切换、工具切换、右键菜单、上下文感知菜单 |
| `CustomShapeDialog.cs` | 自定义图形构建器对话框，支持状态与行为编辑、多边形顶点编辑 |
| `ToolboxConfigDialog.cs` | 工具箱配置对话框，管理工具项的启用/排序/增删 |
| `ShapeActionEditDialog.cs` | 图形行为编辑对话框，添加或编辑单个 ShapeAction |

#### Config 层（`DiagramDesigner/Config/`）

| 文件 | 职责 |
|------|------|
| `GlobalConfig.cs` | 全局配置单例：连线参数、网格参数、画布参数、主题枚举、主题感知颜色属性 |

#### Serialization 层（`DiagramDesigner/Serialization/`）

| 文件 | 职责 |
|------|------|
| `DocumentData.cs` | 数据传输对象（DTO）：DocumentData/ShapeData/ConnectionData/MemberData/ParameterData/StateData |
| `XmlShapeSerializer.cs` | XML 序列化/反序列化器，实现 DrawingDocument 与 DocumentData 之间的双向转换 |

#### DemoApp（`DemoApp/`）

| 文件 | 职责 |
|------|------|
| `Program.cs` | 应用程序入口点（STAThread, EnableVisualStyles） |
| `MainForm.cs` | 宿主窗体：创建 DiagramEditor、注册图形类型、注入菜单、初始化示例数据 |

---

## 3. 核心架构设计

### 3.1 复合控件模式（Facade）

`DiagramEditor` 是整个控件库的唯一公共入口（Facade），它封装了所有子组件：

```
DiagramEditor (UserControl)
  +-- ToolStrip（工具栏：选择/连线/直线/曲线/折线/缩放/置顶/置底/删除）
  +-- StatusStrip（状态栏：状态文本 + 缩放/坐标显示）
  +-- SplitContainer (_mainSplit)
  |     +-- Panel1: ToolboxPanel（工具箱）
  |     +-- Panel2: SplitContainer (_rightSplit)
  |           +-- Panel1: DrawingCanvas（画布）
  |           +-- Panel2: PropertyGrid（属性栏）
  +-- ContextMenuStrip（右键上下文菜单，运行时动态创建）
  +-- MenuStrip（宿主菜单注入）
```

宿主窗体只需创建一个 `DiagramEditor` 实例，即可获得完整的可视化编辑器功能。公共属性 `Canvas`、`Toolbox`、`ToolStrip`、`StatusBar`、`Document` 允许宿主在需要时直接访问子组件。

### 3.2 控件-宿主协议

控件库通过两个公共方法与宿主窗体集成：

- **`ConfigureHostForm(Form parentForm)`**：获取宿主窗体的 `MainMenuStrip`，将其作为菜单注入目标。
- **`ConfigureMenu(MenuStrip hostMenu)`**：直接指定要注入的 MenuStrip，并立即执行菜单注入（`InjectMenus`）。

两个方法均为可选调用，控件在没有宿主集成的情况下仍可正常工作（仅无菜单功能）。调用顺序不受限。

### 3.3 RenderCommand 驱动渲染

图形绘制采用数据驱动的 RenderCommand 模式：

1. `ShapeType` 包含一个 `List<RenderCommand>`，描述图形的绘制指令序列。
2. `GenericShape.Draw()` 从 `ShapeTypeRegistry` 获取对应的 `ShapeType`，然后依次执行每个 `RenderCommand.Execute()`。
3. 每个 `RenderCommand` 使用相对坐标（0~1 范围），在 `Execute` 时根据形状实际 Bounds 映射为绝对坐标。

这实现了图形外观与控件库代码的完全分离——新增图形类型只需注册新的 `ShapeType`，无需修改任何控件库代码。`ShapeLibrary` 预定义了大量常用图形类型，宿主可一行代码批量注册；`ShapeComposer` 支持将多个图形组合为复合图形。

### 3.4 容器-子元素模型

`ContainerShape` 继承 `ShapeBase`，维护一个 `Children` 列表：

- 子元素的 `Parent` 指向容器。
- 容器移动时，所有子元素联动移动（`Move` 方法重写）。
- 容器绘制时，使用 `Graphics.Clip` 裁剪区域，子元素只在容器 body 区域内绘制。
- 容器内的连线在容器绘制阶段单独渲染，也使用裁剪区域。
- `DrawingDocument.RemoveShape` 会级联移除子元素和相关连线。

### 3.5 序列化架构

采用 DTO（Data Transfer Object）模式：

```
DrawingDocument (运行时模型)
       |  ^
  ConvertToData / ConvertFromData
       |  |
DocumentData (XML 可序列化 DTO)
```

- `XmlShapeSerializer` 提供 `Save`/`Load` 静态方法，负责 DocumentData 的 XML 读写。
- `ConvertToData`/`ConvertFromData` 负责运行时对象与 DTO 之间的双向映射。
- 颜色通过 `XmlColor`（int ARGB）在 DTO 中存储，连接模式/可见性等枚举以字符串形式存储，反序列化时通过 `Enum.Parse` 恢复（带 try-catch 回退）。
- 容器父子关系通过 `ParentId` 在 DTO 中保存，反序列化时重建引用。

---

## 4. 模块设计

### 4.1 Core 层

#### ShapeBase（抽象基类）

命名空间 `DiagramDesigner.Core`。所有图形（GenericShape、ContainerShape）的抽象基类，定义位置与尺寸、外观颜色、选中/悬停状态、ZOrder、可见性、可调整尺寸等属性，并提供命中测试、调整手柄检测、克隆、选中绘制等抽象与虚方法。

`ResizeHandle` 枚举定义 9 个方向：`None / TopLeft / TopCenter / TopRight / MiddleLeft / MiddleRight / BottomLeft / BottomCenter / BottomRight`。属性变更时触发 `Changed` 事件。

#### Connection（连线）

命名空间 `DiagramDesigner.Core`。表示两个图形之间的连线，支持 `Straight`（直线）、`Curve`（贝塞尔曲线）、`Orthogonal`（折线）三种模式。连线持有起止图形引用与坐标，颜色、线宽、线型、末端箭头、标签均可配置。`UpdateEndpoints()` 根据起止图形当前位置重新计算连接点。

#### DrawingDocument（文档模型）

命名空间 `DiagramDesigner.Core`。管理所有图形和连线，按 ZOrder 排序。提供增删图形/连线、选中管理、层级调整（置顶/置底）、点命中测试、矩形框选、清空等操作。`RemoveShape` 会级联移除关联连线和容器子元素。文档内容变更时触发 `DocumentChanged` 事件。

#### RenderCommand（渲染命令）

命名空间 `DiagramDesigner.Core`。数据驱动的图形绘制指令，支持 7 种类型：`Rectangle / Ellipse / RoundedRect / Polygon / Line / Text / MemberArea`。所有坐标使用相对值（0~1），在 `Execute` 时映射到图形实际 Bounds。可通过 `UseShapeColors` 决定使用图形自身颜色还是命令自定义颜色。`ShapeColors` 辅助类封装填充/边框/文字/标题栏四种颜色。

#### ShapeType / ShapeTypeRegistry

`ShapeType` 描述一种图形类型的元数据：名称、分类、渲染命令列表、默认尺寸/颜色、行为标志（IsContainer、SupportsMembers）、默认状态列表。`CreateInstance()` 根据类型创建图形实例（容器类型创建 ContainerShape，否则创建 GenericShape）。

`ShapeTypeRegistry` 是全局单例注册表，以 Name 为键存储 ShapeType，提供注册/注销/查询/按分类获取功能。控件库本身不注册任何图形类型，全部由宿主完成。

#### ShapeMember / ShapeState / ShapeAction

- **ShapeMember**：类图成员，支持 5 种类型（Property/Method/Event/Constraint/Field）和 4 种可见性（Public/Private/Protected/Internal），`GetSignature()` 生成 UML 风格签名（如 `+ GetName() : string`）。
- **ShapeState**：图形状态，定义特定状态下的填充/边框/文字/标题栏颜色方案，图形可在多状态间切换。
- **ShapeAction**：图形行为，支持状态切换（StateChange）与宿主回调（HostCallback）两种类型，挂载到图形右键菜单，实现行为与控件解耦。

#### ShapeLibrary / ShapeComposer

- **ShapeLibrary**：内置图形库静态类，预定义矩形、椭圆、菱形、六边形、数据库、组件、流程图节点、UML 类图等常用图形类型，宿主通过属性访问或批量注册。
- **ShapeComposer**：图形组合器，支持 Union/Subtract/Intersect/Xor 四种布尔运算，将多个图形组合为新的复合 ShapeType。

#### CanvasConfig

画布配置类，存储编辑器的 UI 状态（面板显隐、主题、连线模式、设计模式）和工具配置（启用/可见工具列表、自定义图形类型列表），作为 DrawingDocument 的一部分随文档序列化。

#### GraphicsUtility / XmlColor

- **GraphicsUtility**：静态工具类，集中圆角矩形路径创建与描边方法，被多处复用。
- **XmlColor**：将 `System.Drawing.Color` 包装为可 XML 序列化的形式，通过 int ARGB 值存储，支持与 Color 的隐式双向转换。

### 4.2 Shapes 层

#### GenericShape（通用图形）

命名空间 `DiagramDesigner.Shapes`，继承 `ShapeBase`。通过 RenderCommand 列表驱动绘制，支持类图成员管理和多状态切换。绘制流程为：查找 ShapeType -> 执行 RenderCommand 列表 -> 绘制名称 -> 绘制成员 -> 绘制选中指示器。当 ShapeType 未找到时使用 `DrawFallback` 后备绘制。椭圆类型使用椭圆边缘计算连接点，多边形类型回退到矩形算法。`Clone` 执行深拷贝。

#### ContainerShape（容器图形）

命名空间 `DiagramDesigner.Shapes`，继承 `ShapeBase`。可嵌套子元素，带标题栏（HeaderHeight）和裁剪绘制。绘制流程为：圆角矩形背景 -> 标题栏 -> body 分隔线 -> 裁剪区域绘制子元素 -> 选中指示器。`Move` 重写以联动移动所有子元素。子元素按 ZOrder 排序后绘制。

### 4.3 Controls 层

#### DrawingCanvas（画布控件）

命名空间 `DiagramDesigner.Controls`，类型为 `partial class DrawingCanvas : Control`，拆分为交互逻辑（`DrawingCanvas.cs`）与渲染逻辑（`DrawingCanvas.Rendering.cs`）。

画布持有 `Document`（文档模型）、`CurrentTool`（Select/Connect）、`Zoom`（0.1~5.0）、`Offset`（平移偏移）等状态。构造函数设置双缓冲控件样式并初始化 `BufferedGraphicsContext`（最大缓冲 4096x4096）。

交互能力包括：中键平移、左键选择/拖拽/连线/框选/调整尺寸、滚轮缩放（以鼠标为中心）、拖放接收（工具箱创建图形）、键盘快捷键（Delete/Esc/Ctrl+A）。拖拽结束后通过 `UpdateContainerMembership` 自动检测容器归属。提供 `ScreenToWorld`/`WorldToScreen` 坐标转换。

事件：`SelectionChanged`、`DocumentModified`。

#### ToolboxPanel（工具箱面板）

命名空间 `DiagramDesigner.Controls`，类型为 `ToolboxPanel : Panel`。按分类显示已注册图形类型，支持拖放创建图形实例。`ReloadFromRegistry()` 从 ShapeTypeRegistry 重新加载并自动生成图标（通过创建临时图形实例绘制到 28x28 Bitmap）。自定义绘制分类标题与工具项，选中项蓝色高亮，悬停项浅蓝背景。

事件：`ItemSelected`、`ToolboxChanged`。

#### DiagramEditor（复合控件）

命名空间 `DiagramDesigner.Controls`，类型为 `partial class DiagramEditor : UserControl`，拆分为组件初始化/布局/公共 API（`DiagramEditor.cs`）与命令/事件处理（`DiagramEditor.Commands.cs`）。

公共属性包括 `Canvas`、`Toolbox`、`ToolStrip`、`StatusBar`、`Document`、`CurrentFilePath`，以及面板显隐开关（ShowToolbar/ShowPropertyPanel/ShowToolboxPanel/ShowMenuStrip/ShowStatusBar/ShowContextMenu）、`ShowToolbarText`、`Theme`。

公共方法包括缩放操作、工具切换、连线模式设置、删除/全选/层级调整、文档新建/保存/加载、宿主集成（ConfigureHostForm/ConfigureMenu）、主题应用。

**菜单注入机制**：`InjectMenus()` 通过 `FindOrCreateMenu` 查找或创建文件、编辑、视图、工具、图形五个顶级菜单，向其中追加菜单项。已有同名菜单则追加，否则自动创建。

**上下文感知菜单**：`UpdateMenuAvailability` 根据选中状态动态启用/禁用菜单项；`ShowCanvasContextMenu` 运行时动态构建右键菜单，根据上下文显示不同项。

**Layout 延迟初始化**：SplitterDistance 不在构造函数中设置，而是通过 `Layout` 事件在控件首次获得有效尺寸时调用 `ApplySplitterDistances`，避免在尺寸未定时抛出异常。

事件：`ThemeChanged`、`DocumentSaved`、`DocumentLoaded`、`NewDocumentCreated`。

#### 对话框控件

- **CustomShapeDialog**：自定义图形构建器，含状态编辑与多边形顶点编辑选项卡。
- **ToolboxConfigDialog**：工具箱配置，管理工具项启用/排序/增删。
- **ShapeActionEditDialog**：图形行为编辑，添加或编辑单个 ShapeAction。

### 4.4 Config 层

#### GlobalConfig（全局配置）

命名空间 `DiagramDesigner.Config`。全局配置单例，管理连线参数（默认模式、相交允许、相交圆弧）、网格参数（吸附、大小、显示）、画布参数（抗锯齿、主题）等可编辑属性。提供 9 个主题感知只读颜色属性（CanvasBackground、GridColor、SelectionColor 等），根据 EditorTheme（Light/Dark）返回不同颜色。配置变更时触发 `Changed` 事件，画布订阅此事件以重绘。

### 4.5 Serialization 层

#### DocumentData / XmlShapeSerializer

命名空间 `DiagramDesigner.Serialization`。`DocumentData` 及其嵌套类（ShapeData、ConnectionData、MemberData、ParameterData、StateData）是 XML 可序列化的 DTO，所有类标记 `[Serializable]`。

`XmlShapeSerializer` 实现运行时模型与 DTO 之间的双向转换：

- **ConvertToData**：遍历图形生成 ShapeData（建立 idMap），回填 ParentId，遍历连线生成 ConnectionData。
- **ConvertFromData**：遍历 ShapeData 重建图形（建立 shapeMap），重建父子关系，遍历 ConnectionData 通过 shapeMap 恢复连线引用，枚举解析带 try-catch 回退。

`Save`/`Load` 静态方法封装 XML 文件读写。

---

## 5. 渲染管线

### 5.1 完整渲染流程

`DrawingCanvas.OnPaint` 按以下顺序执行：

```
OnPaint(PaintEventArgs e)
    |
    +-- 1. 分配/复用 BufferedGraphics（尺寸变化时重新分配）
    |
    +-- 2. DrawBackground(g)          // 背景绘制（不受缩放影响）
    |     纯色填充 → 水平渐变 → 垂直半透明渐变 → 椭圆柔化高光
    |
    +-- 3. 设置 SmoothingMode.AntiAlias
    |
    +-- 4. g.TranslateTransform(offset) + g.ScaleTransform(zoom)
    |
    +-- 5. DrawGrid(g)                // 网格绘制
    |
    +-- 6. DrawShapes(g)              // 图形绘制
    |     遍历 Parent==null 且 Visible 的图形
    |     ContainerShape 额外裁剪绘制容器内连线
    |
    +-- 7. DrawConnections(g)         // 全局连线绘制（跳过容器内连线）
    |
    +-- 8. DrawRubberBand(g)          // 连线橡皮筋
    |
    +-- 9. DrawSelectionRect(g)       // 框选矩形
    |
    +-- 10. g.ResetTransform()
    |
    +-- 11. _bufferedGraphics.Render(e.Graphics)   // 输出到屏幕
```

### 5.2 背景渐变

`DrawBackground` 在坐标变换之前执行，使用屏幕像素坐标绘制，保证任何缩放级别下背景效果一致。实现为四层叠加：底层纯色、水平线性渐变、垂直半透明渐变、右下角椭圆 PathGradientBrush 柔化高光。

### 5.3 容器内连线裁剪绘制

容器内连线采用分层绘制策略避免溢出和重复：

- **DrawShapes 阶段**：绘制容器时，对容器内连线使用 `g.Save()` / `g.Clip = new Region(bodyRect)` / `g.Restore(state)` 裁剪后绘制。
- **DrawConnections 阶段**：全局连线遍历时跳过 `FromShape.Parent == ToShape.Parent` 的连线。

---

## 6. 交互管线

### 6.1 工具模式

| 工具 | 说明 |
|------|------|
| `Select` | 选择/拖拽/框选/调整尺寸模式，默认光标 |
| `Connect` | 连线创建模式，十字光标 |

### 6.2 选择流程

`OnMouseDown` 获取世界坐标后按优先级判断：中键平移 > Connect 工具 > ResizeHandle > Shape > Connection > 框选。

命中图形时，未按 Ctrl 先清除其他选中，再设置选中。若命中调整手柄则进入调整尺寸模式，否则进入拖拽模式（`ExpandWithChildren` 自动扩展选中集包含容器子元素）。命中连线时清除其他选中并选中连线。未命中任何对象时清除选中并进入框选模式。

### 6.3 拖拽流程

`OnMouseMove` 计算位移后，如开启 `SnapToGrid` 则对齐网格，更新所有拖拽图形位置。`OnMouseUp` 结束拖拽并调用 `UpdateContainerMembership` 检查容器归属。

### 6.4 框选流程

空白区域按下鼠标进入框选模式，移动时更新矩形范围，抬起时通过 `GetShapesInRect` 批量选中范围内图形。

### 6.5 连线创建流程

Connect 工具下，按下鼠标找到起始图形并计算连接点，移动时更新橡皮筋终点，抬起时找到目标图形（不能与起始相同）创建 Connection 并添加到文档。

### 6.6 调整尺寸流程

检测 `HitTestResizeHandle` 命中后，移动时根据手柄方向计算新的 X/Y/Width/Height，应用 MinWidth/MinHeight 限制和网格对齐，抬起时结束。

---

## 7. 主题系统

### 7.1 EditorTheme 枚举

```csharp
public enum EditorTheme
{
    Light,  // 亮色主题
    Dark    // 暗色主题
}
```

### 7.2 主题感知属性

`GlobalConfig` 定义 9 个主题感知只读属性，根据当前主题返回不同颜色：`CanvasBackground`、`GridColor`、`GradientCenterColor`、`SelectionColor`、`ToolPanelBackColor`、`ToolPanelCategoryColor`、`ToolPanelTextColor`、`ToolPanelBorderColor`、`RubberBandColor`。

### 7.3 MyColorTable

继承 `ProfessionalColorTable`，覆盖 20+ 颜色属性以支持亮色/暗色主题，涵盖 ToolStrip/MenuStrip/StatusStrip 渐变、菜单项选中、分隔线、按钮高亮等。

### 7.4 ApplyTheme

`DiagramEditor.ApplyTheme()` 在构造函数初始化、Theme 属性设置、主题菜单事件时调用，统一更新工具栏、菜单栏、状态栏的 BackColor/ForeColor/Renderer，并触发 `Invalidate(true)` 重绘。

---

## 8. 图形注册机制

### 8.1 注册流程

1. 宿主在初始化时调用 `ShapeTypeRegistry.Instance.Clear()` 清空注册表（可选）。
2. 对每种图形类型：创建 `ShapeType` 实例，设置名称、分类、描述、默认尺寸/颜色，创建 RenderCommand 列表（使用相对坐标 0~1），调用 `Register` 注册。
3. 调用 `_editor.Toolbox.ReloadFromRegistry()` 刷新工具箱显示。

宿主也可直接使用 `ShapeLibrary` 预定义的图形类型，或通过 `ShapeComposer` 组合复合图形。

### 8.2 控件剥离原则

控件库本身不注册任何图形类型。所有图形类型由宿主应用在初始化时注册。这保证了控件库可以独立于具体图形类型使用，不同的宿主应用可以注册不同的图形集合，新增图形类型不需要修改控件库代码。

---

## 9. 事件系统

控件通过事件向宿主暴露文档变更通知，宿主可订阅这些事件实现业务联动（如状态持久化、外部视图同步、权限校验等）。事件分两层：DrawingCanvas 在用户交互时触发基础事件，DiagramEditor 转发这些事件并补充状态切换事件，宿主只需订阅 DiagramEditor 的事件即可。

### 9.1 图形与连线事件

以下事件在 DrawingCanvas 中触发（拖放创建、连线工具、删除操作时），由 DiagramEditor 转发给宿主。事件参数携带图形类型名、显示名等上下文，宿主无需访问内部对象即可完成业务处理。

| 事件 | 触发时机 | 事件参数 |
|------|----------|----------|
| `ShapeAdded` | 图形通过工具箱拖放创建到画布 | `ShapeEventArgs`：图形实例、类型名、显示名 |
| `ShapeDeleted` | 图形被删除（Delete 键或菜单） | `ShapeEventArgs`：图形实例、类型名、显示名 |
| `ConnectionAdded` | 连线通过连线工具创建 | `ConnectionEventArgs`：连线实例、两端图形名 |
| `ConnectionDeleted` | 连线被删除 | `ConnectionEventArgs`：连线实例、两端图形名 |
| `ShapeStateChanged` | 图形通过右键菜单切换状态 | `ShapeStateChangedEventArgs`：图形实例、旧状态名、新状态名 |

### 9.2 编辑器生命周期事件

| 事件 | 触发时机 |
|------|----------|
| `DocumentSaved` | 文档保存完成 |
| `DocumentLoaded` | 文档加载完成 |
| `NewDocumentCreated` | 新建文档 |
| `ThemeChanged` | 主题切换 |

### 9.3 行为回调机制

除事件外，`ShapeAction` 提供宿主回调机制。当图形右键菜单中的 HostCallback 类型行为被触发时，控件通过 `ShapeActionEventArgs`（携带 Shape 与 ActionName）通知宿主，由宿主执行自定义逻辑。这使图形的交互行为与控件库彻底解耦。

---

## 10. 宿主集成指南

### 10.1 最简集成代码

```csharp
public class MyForm : Form
{
    private DiagramEditor _editor;
    private MenuStrip _menuStrip;

    public MyForm()
    {
        // 1. 创建宿主菜单栏
        _menuStrip = new MenuStrip();
        this.MainMenuStrip = _menuStrip;
        this.Controls.Add(_menuStrip);

        // 2. 创建编辑器控件
        _editor = new DiagramEditor();
        _editor.Dock = DockStyle.Fill;
        this.Controls.Add(_editor);

        // 3. 注入菜单（可选）
        _editor.ConfigureMenu(_menuStrip);

        // 4. 注册图形类型
        RegisterShapeTypes();

        // 5. 订阅事件（可选）
        _editor.ShapeAdded += delegate(object s, ShapeEventArgs e) { /* ... */ };
    }

    private void RegisterShapeTypes()
    {
        ShapeType rect = new ShapeType();
        rect.Name = "矩形";
        rect.Category = "基本图形";
        rect.RenderCommands.Add(new RenderCommand()); // 配置渲染命令
        ShapeTypeRegistry.Instance.Register(rect);

        _editor.Toolbox.ReloadFromRegistry();
    }
}
```

### 10.2 图形类型注册时机

图形类型注册必须在 DiagramEditor 创建之后、Toolbox 刷新之前完成：

```csharp
_editor = new DiagramEditor();        // 1. 创建控件
RegisterShapeTypes();                 // 2. 注册图形类型
_editor.Toolbox.ReloadFromRegistry(); // 3. 刷新工具箱
```

`ConfigureMenu`/`ConfigureHostForm` 是可选调用，不调用也能正常工作（仅无菜单功能）。
