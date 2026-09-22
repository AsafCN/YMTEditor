using System.Windows;

namespace YMTEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Opens whatever was passed on the command line, which is how Windows starts
        /// us when a .ymt file is double clicked ("YMTEditor.exe C:\...\ig_mike.ymt").
        /// A folder is built from instead, so a ped folder can be dropped on the exe.
        /// </summary>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            MainWindow window = new MainWindow();
            window.Show();

            if (e.Args.Length > 0)
            {
                window.OpenFromStartup(e.Args[0]);
            }
        }
    }
}
