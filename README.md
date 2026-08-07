# Marvin's DiagramDesigner

## 项目简介

Marvin's DiagramDesigner 是一个兼容 .NET Framework 2.0 的 WinForms 可视化图表设计器控件库。它提供完整的图形绘制、连线、容器嵌套、序列化、自定义图形、状态管理、右键菜单等功能，可被宿主应用程序直接拖放使用或编程集成，适用于构建架构图、流程图、UML 类图等各类可视化图表编辑器。

所有代码位于 `DiagramDesigner` 命名空间下，无任何第三方依赖。

## 技术特性

- .NET Framework 2.0 兼容（Visual Studio 2005+），严格使用 C# 2.0 语法
- 纯 WinForms 实现，仅依赖 System / System.Drawing / System.Windows.Forms / System.Xml 四个标准程序集
- RenderCommand 数据驱动渲染，图形外观与控件代码完全分离
- 运行时图形类型注册，新增图形无需修改控件库
- Zone 分区布局系统：每个图形默认拥有标题 Zone，支持自定义 Zone 区域（标题、成员列表、堆叠、流式布局）、冻结缩放
- 多路径与布尔运算：单个图形可由多个封闭路径组成，支持并集/差集/交集/异或运算
- 双缓冲绘制，支持缩放、平移、网格吸附
- 容器嵌套与裁剪绘制，容器内连线自动裁剪
- XML 序列化，文档可持久化与还原（含 Zone、多路径、自定义图形配置）
- 亮色/暗色双主题
- Facade 模式封装，宿主只需与一个 DiagramEditor 实例交互

## 快速开始

### 1. 创建 DiagramEditor 实例

```csharp
_editor = new DiagramEditor();
_editor.Dock = DockStyle.Fill;
this.Controls.Add(_editor);
```

### 2. 注入菜单（可选）

```csharp
_menuStrip = new MenuStrip();
this.MainMenuStrip = _menuStrip;
this.Controls.Add(_menuStrip);
// 控件会自动注入编辑/视图/工具/图形等菜单
_editor.ConfigureMenu(_menuStrip);
```

### 3. 注册图形类型

```csharp
// 方式一：一键注册全部内置图形（44+ 种）
ShapeLibrary.RegisterAll();

// 方式二：按分类选择性注册
ShapeLibrary.RegisterCategory("基本图形");
ShapeLibrary.RegisterCategory("流程图");

// 方式三：自定义图形
ShapeType rect = new ShapeType();
rect.Name = "矩形";
rect.Category = "基本图形";
rect.RenderCommands.Add(new RenderCommand());
ShapeTypeRegistry.Instance.Register(rect);

// 刷新工具箱
_editor.Toolbox.ReloadFromRegistry();
```

### 4. 订阅事件（可选）

编辑器提供图形/连线增删与状态切换事件，宿主可据此联动业务逻辑：

```csharp
_editor.ShapeAdded += delegate(object s, ShapeEventArgs e) {
    Console.WriteLine("添加图形: " + e.ShapeName + " (" + e.ShapeTypeName + ")");
};
_editor.ShapeStateChanged += delegate(object s, ShapeStateChangedEventArgs e) {
    Console.WriteLine(e.ShapeName + ": " + e.OldStateName + " -> " + e.NewStateName);
};
// 另有 ShapeDeleted / ConnectionAdded / ConnectionDeleted 事件
```

## 目录结构

```
MarvinDiagramDesigner/
  ├── DiagramDesigner.sln            # 解决方案文件（VS2005）
  ├── ArchitectureSpecification.md   # 架构规格说明书
  ├── QualityAttributes.md           # 质量属性说明书
  ├── README.md
  ├── DiagramDesigner/               # 控件库项目
  │   ├── DiagramDesigner.csproj
  │   ├── Core/                      # 基础数据模型与工具
  │   ├── Shapes/                    # 具体图形实现
  │   ├── Controls/                  # UI 控件与对话框
  │   ├── Config/                    # 全局配置
  │   ├── Serialization/             # 持久化
  │   └── Icons/                     # 嵌入资源图标
  └── DemoApp/                       # 演示应用项目
      ├── DemoApp.csproj
      ├── Program.cs
      └── MainForm.cs
```

| 层 | 命名空间 | 职责 |
|---|----------|------|
| Core | `DiagramDesigner.Core` | 图形基类、连线、文档模型、渲染命令、图形类型注册表 |
| Shapes | `DiagramDesigner.Shapes` | 通用图形、容器图形 |
| Controls | `DiagramDesigner.Controls` | 画布、工具箱、复合编辑器、对话框 |
| Config | `DiagramDesigner.Config` | 全局配置单例 |
| Serialization | `DiagramDesigner.Serialization` | XML 序列化与 DTO |

---

## Project Overview

Marvin's DiagramDesigner is a WinForms visual diagram designer control library compatible with .NET Framework 2.0. It provides complete shape drawing, connection, container nesting, serialization, custom shape definition, state management, and context menu capabilities. Host applications can use it via drag-and-drop in the designer or through programmatic integration, suitable for building architecture diagrams, flowcharts, UML class diagrams, and other visual editors.

All code resides under the `DiagramDesigner` namespace with no third-party dependencies.

## Technical Features

- .NET Framework 2.0 compatible (Visual Studio 2005+), strict C# 2.0 syntax
- Pure WinForms, depends only on System / System.Drawing / System.Windows.Forms / System.Xml
- RenderCommand data-driven rendering, fully separating shape appearance from control code
- Runtime shape type registration, adding shapes requires no control library changes
- Zone-based layout system: every shape has a default Title Zone; supports custom Zones (title, member list, stack, flow layout) with freeze scaling
- Multi-path with boolean operations: a single shape can consist of multiple closed paths with Union/Subtract/Intersect/Xor operations
- Double-buffered rendering with zoom, pan, and grid snapping
- Container nesting with clip-based drawing, automatic clipping of in-container connections
- XML serialization for document persistence and restoration (including Zones, multi-paths, custom shape configs)
- Light/Dark dual themes
- Facade pattern encapsulation, host interacts with a single DiagramEditor instance

## Quick Start

### 1. Create a DiagramEditor instance

```csharp
_editor = new DiagramEditor();
_editor.Dock = DockStyle.Fill;
this.Controls.Add(_editor);
```

### 2. Inject menus (optional)

```csharp
_menuStrip = new MenuStrip();
this.MainMenuStrip = _menuStrip;
this.Controls.Add(_menuStrip);
// The control auto-injects Edit/View/Tools/Shape menus
_editor.ConfigureMenu(_menuStrip);
```

### 3. Register shape types

```csharp
// Option A: register all built-in shapes at once (44+ types)
ShapeLibrary.RegisterAll();

// Option B: register by category
ShapeLibrary.RegisterCategory("Basic Shapes");

// Option C: custom shape
ShapeType rect = new ShapeType();
rect.Name = "Rectangle";
rect.Category = "Basic Shapes";
rect.RenderCommands.Add(new RenderCommand());
ShapeTypeRegistry.Instance.Register(rect);

// Refresh the toolbox
_editor.Toolbox.ReloadFromRegistry();
```

### 4. Subscribe to events (optional)

The editor exposes shape/connection add/remove and state-change events for host business logic:

```csharp
_editor.ShapeAdded += delegate(object s, ShapeEventArgs e) {
    Console.WriteLine("Shape added: " + e.ShapeName + " (" + e.ShapeTypeName + ")");
};
_editor.ShapeStateChanged += delegate(object s, ShapeStateChangedEventArgs e) {
    Console.WriteLine(e.ShapeName + ": " + e.OldStateName + " -> " + e.NewStateName);
};
// Also: ShapeDeleted / ConnectionAdded / ConnectionDeleted
```

## Directory Structure

```
MarvinDiagramDesigner/
  ├── DiagramDesigner.sln            # Solution file (VS2005)
  ├── ArchitectureSpecification.md   # Architecture specification
  ├── QualityAttributes.md           # Quality attributes
  ├── README.md
  ├── DiagramDesigner/               # Control library project
  │   ├── DiagramDesigner.csproj
  │   ├── Core/                      # Core data models and utilities
  │   ├── Shapes/                    # Concrete shape implementations
  │   ├── Controls/                  # UI controls and dialogs
  │   ├── Config/                    # Global configuration
  │   ├── Serialization/             # Persistence
  │   └── Icons/                     # Embedded resource icons
  └── DemoApp/                       # Demo application project
      ├── DemoApp.csproj
      ├── Program.cs
      └── MainForm.cs
```

| Layer | Namespace | Responsibility |
|-------|-----------|----------------|
| Core | `DiagramDesigner.Core` | Shape base, connection, document model, render commands, shape type registry |
| Shapes | `DiagramDesigner.Shapes` | Generic shape, container shape |
| Controls | `DiagramDesigner.Controls` | Canvas, toolbox, composite editor, dialogs |
| Config | `DiagramDesigner.Config` | Global configuration singleton |
| Serialization | `DiagramDesigner.Serialization` | XML serialization and DTOs |
