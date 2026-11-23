using System;
using System.Windows.Forms;

namespace NativeManagerDemo
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // 无头自检模式：不创建任何窗口，验证命令行引擎
            if (args.Length > 0 && args[0] == "--selftest")
            {
                Environment.ExitCode = SelfTest.Run();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ConsoleShellForm());
        }
    }
}
