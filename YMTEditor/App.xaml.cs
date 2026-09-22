using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YMTEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private const int UseImmersiveDarkMode = 20;      // windows 10 1903+
        private const int UseImmersiveDarkModeOld = 19;   // windows 10 1809

        public App()
        {
            //every window, dialogs included, gets a dark title bar to match the theme
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded));
        }

        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Window window = sender as Window;
            if (window == null)
            {
                return;
            }

            //an implicit Window style only matches the Window type itself, never
            //MainWindow/NewYMTWindow/..., so the theme is applied here instead
            if (window.ReadLocalValue(Window.BackgroundProperty) == DependencyProperty.UnsetValue)
            {
                window.SetResourceReference(Window.BackgroundProperty, "Bg");
            }
            if (window.ReadLocalValue(Window.ForegroundProperty) == DependencyProperty.UnsetValue)
            {
                window.SetResourceReference(Window.ForegroundProperty, "Text");
            }
            window.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                int on = 1;
                if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref on, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(handle, UseImmersiveDarkModeOld, ref on, sizeof(int));
                }
            }
            catch (DllNotFoundException)
            {
                //older windows without dwmapi: the title bar just stays light
            }
        }

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
