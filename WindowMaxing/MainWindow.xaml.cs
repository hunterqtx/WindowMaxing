using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using WpfAnimatedGif;

namespace WindowMaxing
{
    public partial class MainWindow : Window
    {
        private string[] imageFiles;
        private int currentIndex = -1;
        private DispatcherTimer fadeOutTimer;
        private DispatcherTimer gifTimer;
        private int gifInterval = 100;
        private bool isResizing = false;
        private Point lastMousePosition;
        private bool isVideoPlaying = false;
        private bool isVideoLooping = true;
        private bool isTopMost = false;
        private bool isDragging = false;
        private DispatcherTimer timer;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private DispatcherTimer topmostTimer;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        public MainWindow()
            : this(null)
        {
        }



        public MainWindow(string[] args)
        {
            InitializeComponent();
            SetButtonMaxWidth();
            this.MouseMove += MainWindow_MouseMove;
            this.MouseLeftButtonUp += MainWindow_MouseLeftButtonUp;
            this.KeyDown += MainWindow_KeyDown;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            InitializeFadeOutTimer();
            ShowUIElementsTemporarily();
            SetMaxWidthForGrid();
            TopPriorityMenuItem.IsChecked = isTopMost;

            topmostTimer = new DispatcherTimer();
            topmostTimer.Interval = TimeSpan.FromSeconds(1);
            topmostTimer.Tick += TopmostTimer_Tick;
            topmostTimer.Start();

            if (args != null && args.Length > 0)
            {
                string filePath = args[0];
                if (File.Exists(filePath))
                {
                    LoadMedia(filePath);
                    string directory = Path.GetDirectoryName(filePath);
                    imageFiles = Directory.GetFiles(directory, "*.*").Where(file => new[] { ".jpg", ".jpeg", ".png", ".tiff", ".gif", ".bmp", ".ico", ".mp4", ".avi", ".mov", ".wmv", ".mkv" }
                        .Contains(Path.GetExtension(file).ToLower())).ToArray();
                    currentIndex = Array.IndexOf(imageFiles, filePath);
                    UpdateNavigationButtons();
                }
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Width < MinWidth)
            {
                Width = MinWidth;
            }
            if (Height < MinHeight)
            {
                Height = MinHeight;
            }
        }

        private void SetButtonMaxWidth()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            MoveButton.Width = screenWidth;
        }

        private void SetMaxWidthForGrid()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            MainGrid.MaxWidth = screenWidth;
        }

        private void InitializeFadeOutTimer()
        {
            fadeOutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            fadeOutTimer.Tick += (s, args) =>
            {
                HideUIElements();
                fadeOutTimer.Stop();
            };
        }

        private void MainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (isResizing)
            {
                var mousePosition = e.GetPosition(this);
                var deltaX = mousePosition.X - lastMousePosition.X;
                var deltaY = mousePosition.Y - lastMousePosition.Y;

                this.Width += deltaX;
                this.Height += deltaY;

                lastMousePosition = mousePosition;
            }
            else
            {
                ShowUIElements();
                fadeOutTimer.Stop();
                fadeOutTimer.Start();
            }
        }

        private void MainWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isResizing)
            {
                isResizing = false;
                Mouse.Capture(null);
            }
        }

        private void ShowUIElements()
        {
            TopBar.Visibility = Visibility.Visible;
            MetadataButton.Visibility = Visibility.Visible;
            SettingsButton.Visibility = Visibility.Visible;
            PreviousButton.Visibility = Visibility.Visible;
            NextButton.Visibility = Visibility.Visible;
            FitToAspectRatioButton.Visibility = Visibility.Visible;
            if (videoPlayer.Visibility == Visibility.Visible)
            {
                VideoControlBar.Visibility = Visibility.Visible;
            }
        }

        private void HideUIElements()
        {
            TopBar.Visibility = Visibility.Collapsed;
            MetadataButton.Visibility = Visibility.Collapsed;
            SettingsButton.Visibility = Visibility.Collapsed;
            PreviousButton.Visibility = Visibility.Collapsed;
            NextButton.Visibility = Visibility.Collapsed;
            FitToAspectRatioButton.Visibility = Visibility.Collapsed;
            VideoControlBar.Visibility = Visibility.Collapsed;
        }


        private void ShowUIElementsTemporarily()
        {
            ShowUIElements();
            var initialTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            initialTimer.Tick += (s, args) =>
            {
                HideUIElements();
                initialTimer.Stop();
            };
            initialTimer.Start();
        }

        private void LoadImage(string filePath)
        {
            if (gifTimer != null)
            {
                gifTimer.Stop();
                gifTimer = null;
            }

            if (Path.GetExtension(filePath).ToLower() == ".gif")
            {
                // Load Gif using WpfAnimatedGif
                var image = new BitmapImage(new Uri(filePath));
                ImageBehavior.SetAnimatedSource(photoDisplay, image);
            }
            else
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                photoDisplay.Source = bitmap;
            }
        }


        private void CheckIfGifAndPlay(BitmapImage bitmap, string filePath)
        {
            if (Path.GetExtension(filePath).ToLower() == ".gif")
            {
                var gifDecoder = new GifBitmapDecoder(new Uri(filePath), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                var animation = gifDecoder.Frames;

                gifTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(gifInterval) };
                int frameIndex = 0;

                gifTimer.Tick += (s, args) =>
                {
                    photoDisplay.Source = animation[frameIndex];
                    frameIndex = (frameIndex + 1) % animation.Count;
                };

                gifTimer.Start();
            }
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPreviousImage();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            NavigateToNextImage();
        }

        private void MetadataButton_Click(object sender, RoutedEventArgs e)
        {
            if (imageFiles == null || imageFiles.Length == 0) return;

            string filePath = imageFiles[currentIndex];
            string fileName = Path.GetFileName(filePath);
            string codec = string.Empty;
            string resolution = string.Empty;
            string fileSize = string.Empty;

            try
            {
                var shellFile = ShellFile.FromFilePath(filePath);
                codec = shellFile.Properties.System.Video.Compression.Value?.ToString() ?? "Unknown";
                resolution = $"{shellFile.Properties.System.Video.FrameWidth.Value} x {shellFile.Properties.System.Video.FrameHeight.Value}";
                fileSize = new FileInfo(filePath).Length.ToString("N0") + " bytes";
            }
            catch (Exception)
            {
                codec = "Codec information not available.";
                resolution = "Resolution information not available.";
                fileSize = "File size information not available.";
            }

            string metadataInfo = $"Filename: {fileName}\n" +
                                  $"Path: {filePath}\n" +
                                  $"Codec: {codec}\n" +
                                  $"Resolution: {resolution}\n" +
                                  $"File Size: {fileSize}";

            TextBox textBox = new TextBox
            {
                Text = metadataInfo,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            Window infoWindow = new Window
            {
                Title = "Information",
                Content = textBox,
                Width = 400,
                Height = 300
            };
            infoWindow.ShowDialog();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.A)
            {
                NavigateToPreviousImage();
            }
            else if (e.Key == Key.Right || e.Key == Key.D)
            {
                NavigateToNextImage();
            }
            else if (e.Key == Key.Space)
            {
                if (videoPlayer.Visibility == Visibility.Visible && videoPlayer.Source != null)
                {
                    PlayPauseVideo_Click(null, null);
                }
                e.Handled = true;
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (videoPlayer.Visibility == Visibility.Visible && videoPlayer.Source != null)
                {
                    PlayPauseVideo_Click(null, null);
                }
                e.Handled = true;
            }
        }


        private void NavigateToPreviousImage()
        {
            if (imageFiles == null || imageFiles.Length == 0 || currentIndex <= 0) return;

            currentIndex = (currentIndex - 1 + imageFiles.Length) % imageFiles.Length;
            LoadMedia(imageFiles[currentIndex]);
            //UpdateNavigationButtons();
        }

        private void NavigateToNextImage()
        {
            if (imageFiles == null || imageFiles.Length == 0 || currentIndex >= imageFiles.Length - 1) return;

            currentIndex = (currentIndex + 1) % imageFiles.Length;
            LoadMedia(imageFiles[currentIndex]);
            //UpdateNavigationButtons();
        }

        private void UpdateNavigationButtons()
        {
            PreviousButton.Visibility = currentIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            NextButton.Visibility = currentIndex < imageFiles.Length - 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MoveWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            (sender as Button).ContextMenu.IsOpen = true;
        }

        

        private void SetVideoSpeed_Click(object sender, RoutedEventArgs e)
        {
            if (videoPlayer != null)
            {
                foreach (MenuItem item in VideoSpeedMenu.Items)
                {
                    item.IsChecked = false;
                }

                MenuItem clickedItem = sender as MenuItem;
                clickedItem.IsChecked = true;

                double speed = 1.0;
                switch (clickedItem.Name)
                {
                    case "Speed05x":
                        speed = 0.5;
                        break;
                    case "Speed075x":
                        speed = 0.75;
                        break;
                    case "Speed1x":
                        speed = 1.0;
                        break;
                    case "Speed125x":
                        speed = 1.25;
                        break;
                    case "Speed15x":
                        speed = 1.5;
                        break;
                    case "Speed175x":
                        speed = 1.75;
                        break;
                    case "Speed2x":
                        speed = 2.0;
                        break;
                }

                videoPlayer.SpeedRatio = speed;
            }
        }

        private void SetGifTimer_Click(object sender, RoutedEventArgs e)
        {
            if (gifTimer != null)
            {
                foreach (MenuItem item in GifIntervalMenu.Items)
                {
                    item.IsChecked = false;
                }

                MenuItem clickedItem = sender as MenuItem;
                clickedItem.IsChecked = true;

                int interval = 100;
                switch (clickedItem.Name)
                {
                    case "Gif125ms":
                        interval = 125;
                        break;
                    case "Gif100ms":
                        interval = 100;
                        break;
                    case "Gif90ms":
                        interval = 90;
                        break;
                    case "Gif80ms":
                        interval = 80;
                        break;
                    case "Gif70ms":
                        interval = 70;
                        break;
                    case "Gif60ms":
                        interval = 60;
                        break;
                    case "Gif50ms":
                        interval = 50;
                        break;
                }

                gifInterval = interval;
                RestartGifTimer();
            }
        }

        private void RestartGifTimer()
        {
            if (gifTimer != null)
            {
                gifTimer.Stop();
                gifTimer.Interval = TimeSpan.FromMilliseconds(gifInterval);
                gifTimer.Start();
            }
        }

        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                isResizing = true;
                lastMousePosition = e.GetPosition(this);
                Mouse.Capture(sender as UIElement);
            }
        }

        private void LoadMedia(string filePath)
        {
            if (gifTimer != null)
            {
                gifTimer.Stop();
                gifTimer = null;
            }

            if (videoPlayer.Source != null)
            {
                videoPlayer.Stop();
                videoPlayer.MediaOpened -= VideoPlayer_MediaOpened;
                videoPlayer.MediaEnded -= VideoPlayer_MediaEnded;
                videoPlayer.Source = null;
            }

            string extension = Path.GetExtension(filePath).ToLower();
            if (new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv" }.Contains(extension))
            {
                photoDisplay.Visibility = Visibility.Collapsed;
                videoPlayer.Visibility = Visibility.Visible;

                videoPlayer.Source = new Uri(filePath);
                videoPlayer.MediaOpened += VideoPlayer_MediaOpened;
                videoPlayer.MediaEnded += VideoPlayer_MediaEnded;
                videoPlayer.Play();
                isVideoPlaying = true;
                PlayPauseButton.Content = "⏸";
            }
            else
            {
                photoDisplay.Visibility = Visibility.Visible;
                videoPlayer.Visibility = Visibility.Collapsed;
                VideoControlBar.Visibility = Visibility.Collapsed;
                LoadImage(filePath);
            }
        }



        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            VideoSlider.Maximum = videoPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            TotalTimeText.Text = videoPlayer.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");

            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += Timer_Tick;
            timer.Start();
            isVideoPlaying = true;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!isDragging)
            {
                VideoSlider.Value = videoPlayer.Position.TotalSeconds;
                CurrentTimeText.Text = videoPlayer.Position.ToString(@"hh\:mm\:ss");
            }
        }

        private void PlayPauseVideo_Click(object sender, RoutedEventArgs e)
        {
            if (videoPlayer.Source == null)
            {
                return;
            }

            if (videoPlayer.Position == videoPlayer.NaturalDuration.TimeSpan)
            {
                videoPlayer.Position = TimeSpan.Zero;
                videoPlayer.Play();
                PlayPauseButton.Content = "⏸";
                isVideoPlaying = true;
            }
            else if (isVideoPlaying)
            {
                videoPlayer.Pause();
                PlayPauseButton.Content = "▶";
                isVideoPlaying = false;
            }
            else
            {
                videoPlayer.Play();
                PlayPauseButton.Content = "⏸";
                isVideoPlaying = true;
            }
        }

        private void VideoSlider_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
            videoPlayer.Pause();
        }

        private void VideoSlider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            videoPlayer.Position = TimeSpan.FromSeconds(VideoSlider.Value);
            videoPlayer.Play();
        }

        private void VideoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isDragging && videoPlayer.NaturalDuration.HasTimeSpan)
            {
                videoPlayer.Position = TimeSpan.FromSeconds(VideoSlider.Value);
            }
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (isVideoLooping)
            {
                videoPlayer.Position = TimeSpan.FromSeconds(0.01);
                videoPlayer.Play();
            }
            else
            {
                PlayPauseButton.Content = "⏮️";
                isVideoPlaying = false;
            }
        }

        private void LoopVideo_Click(object sender, RoutedEventArgs e)
        {
            isVideoLooping = !isVideoLooping;
            ((Button)sender).Content = isVideoLooping ? "🔁 On" : "🔁 Off";
        }

        private void FitToAspectRatio_Click(object sender, RoutedEventArgs e)
        {
            if (photoDisplay.Visibility == Visibility.Visible && photoDisplay.Source is BitmapSource bitmapSource)
            {
                double aspectRatio = bitmapSource.Width / bitmapSource.Height;
                double newHeight = this.ActualHeight;
                double newWidth = newHeight * aspectRatio;

                if (newWidth > SystemParameters.PrimaryScreenWidth)
                {
                    newWidth = SystemParameters.PrimaryScreenWidth;
                    newHeight = newWidth / aspectRatio;
                }

                this.Width = newWidth;
                this.Height = newHeight;
            }
            else if (videoPlayer.Visibility == Visibility.Visible && videoPlayer.NaturalVideoWidth > 0 && videoPlayer.NaturalVideoHeight > 0)
            {
                double aspectRatio = (double)videoPlayer.NaturalVideoWidth / videoPlayer.NaturalVideoHeight;
                double newHeight = this.ActualHeight;
                double newWidth = newHeight * aspectRatio;

                if (newWidth > SystemParameters.PrimaryScreenWidth)
                {
                    newWidth = SystemParameters.PrimaryScreenWidth;
                    newHeight = newWidth / aspectRatio;
                }

                this.Width = newWidth;
                this.Height = newHeight;
            }
        }

        private void TopPriority_Click(object sender, RoutedEventArgs e)
        {
            isTopMost = !isTopMost;
            SetTopmost(isTopMost);

            MenuItem menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                menuItem.IsChecked = isTopMost;
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.Topmost = false;
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (isTopMost)
            {
                this.Topmost = true;
            }
        }

        private void SetTopmost(bool topmost)
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            if (topmost)
            {
                SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            else
            {
                SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            isTopMost = topmost;
        }

        private void ToggleTopmost_Click(object sender, RoutedEventArgs e)
        {
            bool isTopmost = this.Topmost;
            SetTopmost(!isTopmost);
            this.Topmost = !isTopmost;
        }

        private void TopmostTimer_Tick(object sender, EventArgs e)
        {
            if (isTopMost && !this.Topmost)
            {
                SetTopmost(true);
            }
        }
    }
}
