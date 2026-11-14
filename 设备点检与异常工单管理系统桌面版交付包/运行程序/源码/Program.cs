using System;
using System.IO;
using System.Windows.Forms;

namespace Inspection
{
    static class Program
    {
        public const string AppName = "设备点检与异常工单管理系统";
        public const string AppVersion = "1.0.0";

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
                return SelfTest.Run();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                MessageBox.Show(e.Exception.Message, "程序异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            try
            {
                Store.Open(ResolveDataDir());
            }
            catch (Exception ex)
            {
                MessageBox.Show("数据库初始化失败：" + ex.Message + Environment.NewLine +
                    "请确认程序目录可写，或删除数据库文件后重试。", AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 2;
            }

            try
            {
                Application.Run(new MainForm());
            }
            finally
            {
                Store.Close();
            }
            return 0;
        }

        static string ResolveDataDir()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string preferred = Path.Combine(baseDir, "数据库");
            try
            {
                if (!Directory.Exists(preferred)) Directory.CreateDirectory(preferred);
                string probe = Path.Combine(preferred, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return preferred;
            }
            catch
            {
                string fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "InspectionSystem");
                if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
                return fallback;
            }
        }
    }
}