using System.Windows;

namespace WindowMaxing
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            MainWindow mainWindow = new MainWindow(e.Args);
            mainWindow.Show();
        }
    }
}
