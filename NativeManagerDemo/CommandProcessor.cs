using System;
using System.Collections.Generic;
using System.Text;

namespace NativeManagerDemo
{
    /// <summary>命令执行结果。</summary>
    public class CommandResult
    {
        public bool Success;
        public string Text;
        public CommandResult(bool success, string text)
        {
            Success = success;
            Text = text;
        }
    }

    /// <summary>
    /// 命令行引擎。把文本命令翻译为对 ITopologyHost 的调用。
    /// 不依赖任何控件类型，可脱离 UI 单独测试。
    /// </summary>
    public class CommandProcessor
    {
        private readonly ITopologyHost _host;

        public CommandProcessor(ITopologyHost host)
        {
            if (host == null) throw new ArgumentNullException("host");
            _host = host;
        }

        public CommandResult Execute(string input)
        {
            List<string> tk = Tokenize(input);
            if (tk.Count == 0) return new CommandResult(false, "(空命令)");

            string verb = tk[0].ToLowerInvariant();
            try
            {
                switch (verb)
                {
                    case "help": case "?": return Ok(HelpText());
                    case "types": return Ok(TypesText());
                    case "add": return CmdAdd(tk);
                    case "list": case "ls": return Ok(ListText());
                    case "conns": case "connections": case "connlist": return Ok(_host.DescribeConnections());
                    case "count": return Ok(string.Format("图形: {0}   连线: {1}", _host.ShapeCount, _host.ConnectionCount));
                    case "select": return CmdSelect(tk);
                    case "rename": return CmdRename(tk);
                    case "set": return CmdSet(tk);
                    case "state": return CmdState(tk);
                    case "connect": case "link": return CmdConnect(tk);
                    case "delete": case "del": case "rm": return CmdDelete();
                    case "front": return CmdFront();
                    case "back": return CmdBack();
                    case "zoom": return CmdZoom(tk);
                    case "fit": _host.FitView(); return Ok("已适应视图");
                    case "mode": return CmdMode(tk);
                    case "new": _host.NewDocument(); return Ok("已新建文档");
                    case "save": return CmdSave(tk);
                    case "load": return CmdLoad(tk);
                    default:
                        return new CommandResult(false, "未知命令: " + tk[0] + "（输入 help 查看帮助）");
                }
            }
            catch (Exception ex)
            {
                return new CommandResult(false, "执行失败: " + ex.Message);
            }
        }

        // ===== 各命令 =====

        private CommandResult CmdAdd(List<string> tk)
        {
            if (tk.Count < 2) return Fail("用法: add <类型> [名称] [x] [y]");

            string typeName = tk[1];
            string name = tk.Count >= 3 ? tk[2] : typeName + "-" + (_host.ShapeCount + 1);
            float x = tk.Count >= 4 ? ParseFloat(tk[3], AutoX()) : AutoX();
            float y = tk.Count >= 5 ? ParseFloat(tk[4], AutoY()) : AutoY();

            ShapeInfo si = _host.AddShape(typeName, name, x, y);
            if (si == null) return Fail("未知图形类型: " + typeName + "（输入 types 查看可用类型）");

            _host.SelectByName(si.Name);
            return Ok(string.Format("已新增: {0} ({1}) @ ({2:0.#}, {3:0.#})", si.Name, si.TypeName, x, y));
        }

        private CommandResult CmdSelect(List<string> tk)
        {
            if (tk.Count < 2) return Fail("用法: select all | none | <名称>");
            string a = tk[1].ToLowerInvariant();
            if (a == "all")
            {
                _host.SelectAll();
                return Ok("已全选 " + _host.ListSelection().Count + " 个对象");
            }
            if (a == "none" || a == "clear")
            {
                _host.ClearSelection();
                return Ok("已取消选择");
            }
            return _host.SelectByName(tk[1])
                ? Ok("已选中: " + tk[1])
                : Fail("未找到图形: " + tk[1]);
        }

        private CommandResult CmdRename(List<string> tk)
        {
            if (tk.Count < 2) return Fail("用法: rename <新名称>");
            return _host.RenameSelection(tk[1])
                ? Ok("已重命名选中对象为: " + tk[1])
                : Fail("没有选中对象");
        }

        private CommandResult CmdSet(List<string> tk)
        {
            if (tk.Count < 3) return Fail("用法: set <属性> <值>   （属性: name/x/y/width/height/fill/border/state）");
            string prop = tk[1];
            string value = Join(tk, 2);
            return _host.SetProperty(prop, value)
                ? Ok("已设置 " + prop + " = " + value)
                : Fail("设置失败（无选中对象，或属性/取值无效）: " + prop);
        }

        private CommandResult CmdState(List<string> tk)
        {
            if (tk.Count < 2) return Fail("用法: state <状态名>");
            IList<ShapeInfo> sel = _host.ListSelection();
            if (sel.Count == 0) return Fail("没有选中对象");

            int ok = 0;
            foreach (ShapeInfo si in sel)
                if (_host.SetShapeState(si.Id, tk[1])) ok++;

            return ok > 0
                ? Ok("已切换状态为: " + tk[1] + "（共 " + ok + " 个对象）")
                : Fail("选中对象没有状态 '" + tk[1] + "'（可用状态见右侧对象操作面板）");
        }

        private CommandResult CmdConnect(List<string> tk)
        {
            if (tk.Count < 3) return Fail("用法: connect <起点> <终点> [标签]");
            string label = tk.Count >= 4 ? Join(tk, 3) : "";
            return _host.Connect(tk[1], tk[2], label)
                ? Ok("已连接: " + tk[1] + " -> " + tk[2] + (label.Length > 0 ? " [" + label + "]" : ""))
                : Fail("连接失败：未找到图形 " + tk[1] + " 或 " + tk[2]);
        }

        private CommandResult CmdDelete()
        {
            int n = _host.RemoveSelection();
            return n > 0 ? Ok("已删除 " + n + " 个对象") : Fail("没有选中对象");
        }

        private CommandResult CmdFront()
        {
            _host.BringToFrontSelection();
            return Ok("已置顶选中对象");
        }

        private CommandResult CmdBack()
        {
            _host.SendToBackSelection();
            return Ok("已置底选中对象");
        }

        private CommandResult CmdZoom(List<string> tk)
        {
            if (tk.Count < 2) return Fail("用法: zoom in | out | reset | <百分比>");
            string a = tk[1].ToLowerInvariant();
            if (a == "in") { _host.Zoom(1.25f); return Ok(ZoomText()); }
            if (a == "out") { _host.Zoom(0.8f); return Ok(ZoomText()); }
            if (a == "reset") { _host.SetZoom(1f); return Ok("缩放复位 100%"); }

            float pct;
            if (float.TryParse(tk[1], out pct))
            {
                _host.SetZoom(pct / 100f);
                return Ok(ZoomText());
            }
            return Fail("无法识别的缩放参数: " + tk[1]);
        }

        private CommandResult CmdMode(List<string> tk)
        {
            if (tk.Count < 2) return Fail("用法: mode straight | curve | ortho");
            return _host.SetConnectionMode(tk[1])
                ? Ok("连线模式已设为: " + tk[1])
                : Fail("无法识别的连线模式: " + tk[1]);
        }

        private CommandResult CmdSave(List<string> tk)
        {
            if (tk.Count < 2) return Fail("用法: save <文件路径>");
            string path = Join(tk, 1);
            _host.SaveDocument(path);
            return Ok("已保存: " + path);
        }

        private CommandResult CmdLoad(List<string> tk)
        {
            if (tk.Count < 2) return Fail("用法: load <文件路径>");
            string path = Join(tk, 1);
            _host.LoadDocument(path);
            return Ok("已加载: " + path);
        }

        // ===== 文本输出 =====

        private string ListText()
        {
            IList<ShapeInfo> shapes = _host.ListShapes();
            if (shapes.Count == 0) return "(无图形)";

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < shapes.Count; i++)
            {
                ShapeInfo si = shapes[i];
                string state = si.HasStates ? "  状态:" + si.StateName : "";
                sb.AppendLine(string.Format("  {0}. {1} [{2}]  ({3:0.#},{4:0.#}) {5:0.#}x{6:0.#}{7}",
                    i + 1, si.Name, si.TypeName, si.X, si.Y, si.Width, si.Height, state));
            }
            return sb.ToString();
        }

        private string TypesText()
        {
            IList<string> names = _host.ListTypeNames();
            if (names.Count == 0) return "(未注册任何图形类型)";
            StringBuilder sb = new StringBuilder();
            sb.Append("可用图形类型(").Append(names.Count).Append("): ");
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(names[i]);
            }
            return sb.ToString();
        }

        private string ZoomText()
        {
            return string.Format("缩放: {0:0}%", _host.GetZoom() * 100f);
        }

        private static string HelpText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("可用命令：");
            sb.AppendLine("  help / ?                    显示本帮助");
            sb.AppendLine("  types                       列出可用图形类型");
            sb.AppendLine("  add <类型> [名称] [x] [y]    新增图形");
            sb.AppendLine("  list / ls                   列出全部图形");
            sb.AppendLine("  conns                       列出全部连线");
            sb.AppendLine("  count                       统计图形与连线数量");
            sb.AppendLine("  select all|none|<名称>      选择对象");
            sb.AppendLine("  rename <新名称>             重命名选中对象");
            sb.AppendLine("  set <属性> <值>             设置选中对象属性(name/x/y/width/height/fill/border)");
            sb.AppendLine("  state <状态名>              切换选中对象状态");
            sb.AppendLine("  connect <起点> <终点> [标签]  连接两个图形");
            sb.AppendLine("  delete / del                删除选中对象");
            sb.AppendLine("  front / back                置顶 / 置底");
            sb.AppendLine("  zoom in|out|reset|<百分比>   缩放视图");
            sb.AppendLine("  fit                         适应全部内容");
            sb.AppendLine("  mode straight|curve|ortho   设置连线模式");
            sb.AppendLine("  new                         新建文档");
            sb.AppendLine("  save <路径> / load <路径>    保存 / 加载文档");
            sb.AppendLine("  cls                         清屏");
            return sb.ToString();
        }

        // ===== 解析辅助 =====

        private static List<string> Tokenize(string s)
        {
            List<string> list = new List<string>();
            if (string.IsNullOrEmpty(s)) return list;

            StringBuilder cur = new StringBuilder();
            bool inQuote = false;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '"') { inQuote = !inQuote; continue; }
                if (!inQuote && char.IsWhiteSpace(ch))
                {
                    if (cur.Length > 0) { list.Add(cur.ToString()); cur.Length = 0; }
                }
                else
                {
                    cur.Append(ch);
                }
            }
            if (cur.Length > 0) list.Add(cur.ToString());
            return list;
        }

        private static string Join(List<string> tk, int start)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = start; i < tk.Count; i++)
            {
                if (i > start) sb.Append(' ');
                sb.Append(tk[i]);
            }
            return sb.ToString();
        }

        private static float ParseFloat(string s, float fallback)
        {
            float f;
            return float.TryParse(s, out f) ? f : fallback;
        }

        private float AutoX() { return 60f + 150f * (_host.ShapeCount % 5); }
        private float AutoY() { return 60f + 120f * ((_host.ShapeCount / 5) % 4); }

        private static CommandResult Ok(string text) { return new CommandResult(true, text); }
        private static CommandResult Fail(string text) { return new CommandResult(false, text); }
    }
}
