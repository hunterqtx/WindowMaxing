using FFmpeg.AutoGen;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Unosquare.FFME;

namespace WindowMaxing
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Library.FFmpegDirectory = @"d:\ffmpeg" + (Environment.Is64BitProcess ? @"\x64" : string.Empty);

            MessageBox.Show($"FFmpeg-Verzeichnis: {Library.FFmpegDirectory}");

            base.OnStartup(e);
            MainWindow mainWindow = new MainWindow(e.Args);
            mainWindow.Show();

            Task.Run(async () =>
            {
                try
                {
                    // Pre-load FFmpeg
                    await Library.LoadFFmpegAsync();
                }
                catch (Exception ex)
                {
                    var dispatcher = Current?.Dispatcher;
                    if (dispatcher != null)
                    {
                        await dispatcher.BeginInvoke(new Action(() =>
                        {
                            MessageBox.Show(MainWindow,
                                $"Unable to Load FFmpeg Libraries from path:\r\n    {Library.FFmpegDirectory}" +
                                $"\r\nMake sure the above folder contains FFmpeg shared binaries (dll files) for the " +
                                $"applicantion's architecture ({(Environment.Is64BitProcess ? "64-bit" : "32-bit")})" +
                                $"\r\nTIP: You can download builds from https://ffmpeg.org/download.html" +
                                $"\r\n{ex.GetType().Name}: {ex.Message}\r\n\r\nApplication will exit.",
                                "FFmpeg Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);

                            Current?.Shutdown();
                        }));
                    }
                }
            });
        }
    }
}