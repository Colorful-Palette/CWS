using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;

namespace CWS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 强制设置工作目录为程序所在目录
            // 这兴许能解决自启动问题
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            Directory.SetCurrentDirectory(baseDirectory);

            // 如果注册表里加了 --autostart 
            bool isAutoStart = e.Args.Contains("--autostart");

            if (isAutoStart)
            {
                // 这里可以决定自启动时的行为
                // 例如：如果不希望自启动时弹出大窗口，可以在这里处理
                // 或者在 MainWindow_Loaded 里判断并隐藏主界面
            }

            base.OnStartup(e);
        }
    }
}