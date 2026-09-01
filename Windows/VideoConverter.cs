using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Grove Swift Video Converter")]
[assembly: System.Reflection.AssemblyDescription("Portable video conversion, trimming and cropping powered by FFmpeg")]
[assembly: System.Reflection.AssemblyCompany("Graham Grove")]
[assembly: System.Reflection.AssemblyProduct("Grove Swift Video Converter")]
[assembly: System.Reflection.AssemblyCopyright("Copyright © 2026 Graham Grove")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace PortableVideoConverter
{
    public class VideoItem
    {
        public string Path { get; set; }
        public override string ToString() { return System.IO.Path.GetFileName(Path); }
    }

    public class CaptureResult
    {
        public string Output { get; set; }
        public bool TimedOut { get; set; }
        public int ExitCode { get; set; }
    }

    public class App : Application
    {
        [STAThread]
        public static void Main()
        {
            App app = new App();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(new MainWindow());
        }
    }

    public class MainWindow : Window
    {
        readonly Brush Accent = new SolidColorBrush(Color.FromRgb(65, 135, 255));
        readonly Brush Panel = new SolidColorBrush(Color.FromRgb(32, 36, 45));
        readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(235, 239, 245));
        readonly ListBox files = new ListBox();
        readonly MediaElement media = new MediaElement();
        readonly Canvas previewCanvas = new Canvas();
        readonly Rectangle cropRect = new Rectangle();
        readonly List<Thumb> cropHandles = new List<Thumb>();
        readonly Canvas trimCanvas = new Canvas();
        readonly Rectangle trimRail = new Rectangle();
        readonly Rectangle trimSelection = new Rectangle();
        readonly Rectangle trimBefore = new Rectangle();
        readonly Rectangle trimAfter = new Rectangle();
        readonly Thumb startCaliper = new Thumb();
        readonly Thumb endCaliper = new Thumb();
        readonly TextBlock startCaliperLabel = new TextBlock();
        readonly TextBlock endCaliperLabel = new TextBlock();
        readonly Border previewUnavailablePanel = new Border();
        readonly Slider playSlider = new Slider();
        readonly Slider startSlider = new Slider();
        readonly Slider endSlider = new Slider();
        readonly TextBlock timeLabel = new TextBlock();
        readonly TextBlock trimLabel = new TextBlock();
        readonly TextBlock cropLabel = new TextBlock();
        readonly TextBlock status = new TextBlock();
        readonly ProgressBar progress = new ProgressBar();
        readonly ComboBox format = new ComboBox();
        readonly ComboBox quality = new ComboBox();
        readonly Button playButton = new Button();
        readonly Button convertButton = new Button();
        readonly Button cancelButton = new Button();
        readonly System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();

        string currentFile;
        string outputFolder = "";
        string ffmpegPath;
        string ffprobePath;
        string videoCodec;
        string pixelFormat;
        bool usingPreviewProxy;
        int previewRequest;
        double duration;
        int videoWidth;
        int videoHeight;
        bool seeking;
        bool playing;
        bool converting;
        Process activeProcess;

        double cropX, cropY, cropW = 1, cropH = 1;
        const double HandleSize = 14;

        public MainWindow()
        {
            Title = "Grove Swift Video Converter";
            Width = 1220; Height = 790; MinWidth = 940; MinHeight = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(22, 25, 31));
            Foreground = TextBrush;
            AllowDrop = true;
            FontFamily = new FontFamily("Segoe UI");
            ffmpegPath = ToolPath("ffmpeg.exe");
            ffprobePath = ToolPath("ffprobe.exe");
            BuildUi();
            WireEvents();
            timer.Interval = TimeSpan.FromMilliseconds(200);
            timer.Tick += TimerTick;
            timer.Start();
            Loaded += delegate { CheckTools(); ResetCrop(); };
        }

        string ToolPath(string name)
        {
            string local = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", name);
            return File.Exists(local) ? local : name;
        }

        FrameworkElement Heading(string text, double size)
        {
            return new TextBlock { Text = text, FontSize = size, FontWeight = FontWeights.SemiBold, Foreground = TextBrush, Margin = new Thickness(0, 0, 0, 10) };
        }

        Button Btn(string text, double width)
        {
            return new Button { Content = text, Width = width, Height = 34, Margin = new Thickness(0, 0, 8, 0), Background = Accent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        }

        Border Card(UIElement child)
        {
            return new Border { Background = Panel, CornerRadius = new CornerRadius(8), Padding = new Thickness(16), Margin = new Thickness(0, 0, 12, 12), Child = child };
        }

        void BuildUi()
        {
            Grid root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 14) }; header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = "Grove Swift Video Converter", FontSize = 25, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            Button about = Btn("About", 82); about.Margin = new Thickness(0); about.Background = new SolidColorBrush(Color.FromRgb(70, 76, 88)); about.Click += delegate { ShowAbout(); }; Grid.SetColumn(about, 1); header.Children.Add(about); root.Children.Add(header);

            Grid body = new Grid(); Grid.SetRow(body, 1);
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });

            StackPanel left = new StackPanel();
            left.Children.Add(Heading("Videos", 17));
            Border drop = new Border { BorderBrush = Accent, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(7), Height = 82, Margin = new Thickness(0, 0, 0, 10), Background = new SolidColorBrush(Color.FromRgb(27, 41, 62)) };
            drop.Child = new TextBlock { Text = "Drop video files here\nor click Add videos", TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = TextBrush };
            left.Children.Add(drop);
            files.Height = 390; files.SelectionMode = SelectionMode.Extended; files.Background = new SolidColorBrush(Color.FromRgb(24, 27, 34)); files.Foreground = TextBrush; files.BorderThickness = new Thickness(0);
            ContextMenu queueMenu = new ContextMenu(); MenuItem removeMenuItem = new MenuItem { Header = "Remove selected videos" }; removeMenuItem.Click += delegate { RemoveSelected(); }; queueMenu.Items.Add(removeMenuItem); files.ContextMenu = queueMenu;
            files.PreviewMouseRightButtonDown += QueueRightClick;
            left.Children.Add(files);
            StackPanel fileButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            Button add = Btn("Add videos", 110); add.Click += delegate { OpenFiles(); };
            Button remove = Btn("Remove", 90); remove.Background = new SolidColorBrush(Color.FromRgb(70, 76, 88)); remove.Click += delegate { RemoveSelected(); };
            fileButtons.Children.Add(add); fileButtons.Children.Add(remove); left.Children.Add(fileButtons);
            Border leftCard = Card(left); Grid.SetColumn(leftCard, 0); body.Children.Add(leftCard);

            StackPanel center = new StackPanel(); center.Children.Add(Heading("Preview and crop", 17));
            Border previewBorder = new Border { Background = Brushes.Black, BorderBrush = new SolidColorBrush(Color.FromRgb(68, 73, 84)), BorderThickness = new Thickness(1), MinHeight = 400 };
            Grid previewGrid = new Grid();
            media.LoadedBehavior = MediaState.Manual; media.UnloadedBehavior = MediaState.Manual; media.Stretch = Stretch.Uniform; media.ScrubbingEnabled = true;
            previewGrid.Children.Add(media);
            previewCanvas.Background = Brushes.Transparent; previewCanvas.ClipToBounds = true;
            cropRect.Stroke = new SolidColorBrush(Color.FromRgb(65, 210, 140)); cropRect.StrokeThickness = 2; cropRect.Fill = new SolidColorBrush(Color.FromArgb(28, 65, 210, 140));
            previewCanvas.Children.Add(cropRect);
            string[] tags = { "TL", "T", "TR", "R", "BR", "B", "BL", "L" };
            foreach (string tag in tags) { Thumb t = MakeHandle(tag); cropHandles.Add(t); previewCanvas.Children.Add(t); }
            previewGrid.Children.Add(previewCanvas);
            BuildPreviewTrimCalipers(); previewGrid.Children.Add(trimCanvas);
            previewUnavailablePanel.Background = new SolidColorBrush(Color.FromArgb(235, 12, 15, 20)); previewUnavailablePanel.Visibility = Visibility.Collapsed;
            previewUnavailablePanel.Child = new TextBlock { Text = "Unable to preview this image but it can still be converted to another format.", Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 520, Margin = new Thickness(30) };
            previewGrid.Children.Add(previewUnavailablePanel);
            previewBorder.Child = previewGrid; center.Children.Add(previewBorder);
            Grid controls = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); controls.ColumnDefinitions.Add(new ColumnDefinition()); controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
            playButton.Content = "▶ Play"; playButton.Width = 75; playButton.Height = 30; playButton.Margin = new Thickness(0, 0, 8, 0); controls.Children.Add(playButton);
            playSlider.Minimum = 0; playSlider.Maximum = 1; playSlider.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(playSlider, 1); controls.Children.Add(playSlider);
            timeLabel.Text = "00:00:00.0 / 00:00:00.0"; timeLabel.VerticalAlignment = VerticalAlignment.Center; timeLabel.TextAlignment = TextAlignment.Right; timeLabel.Foreground = TextBrush; Grid.SetColumn(timeLabel, 2); controls.Children.Add(timeLabel); center.Children.Add(controls);
            WrapPanel previewButtons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            Button stepBack = Btn("− 1 ms", 65); stepBack.Background = new SolidColorBrush(Color.FromRgb(70, 76, 88)); stepBack.ToolTip = "Pause and move back one millisecond"; stepBack.Click += delegate { StepPosition(-0.001); };
            Button stepForward = Btn("+ 1 ms", 65); stepForward.Background = new SolidColorBrush(Color.FromRgb(70, 76, 88)); stepForward.ToolTip = "Pause and move forward one millisecond"; stepForward.Click += delegate { StepPosition(0.001); };
            Button setStart = Btn("Start = current", 120); setStart.Click += delegate { startSlider.Value = CurrentPosition(); };
            Button setEnd = Btn("End = current", 120); setEnd.Click += delegate { endSlider.Value = CurrentPosition(); }; setEnd.Margin = new Thickness(0);
            previewButtons.Children.Add(stepBack); previewButtons.Children.Add(stepForward); previewButtons.Children.Add(setStart); previewButtons.Children.Add(setEnd); center.Children.Add(previewButtons);
            cropLabel.Text = "Crop: full frame"; cropLabel.Foreground = new SolidColorBrush(Color.FromRgb(174, 184, 199)); cropLabel.Margin = new Thickness(0, 8, 0, 0); center.Children.Add(cropLabel);
            Border centerCard = Card(center); Grid.SetColumn(centerCard, 1); body.Children.Add(centerCard);

            StackPanel right = new StackPanel(); right.Children.Add(Heading("Output", 17));
            right.Children.Add(new TextBlock { Text = "Format", Foreground = TextBrush, Margin = new Thickness(0, 3, 0, 5) });
            foreach (string s in new[] { "MP4", "MKV", "WebM", "MOV", "AVI", "GIF", "MP3 (audio only)" }) format.Items.Add(s); format.SelectedIndex = 0; format.Height = 32; right.Children.Add(format);
            right.Children.Add(new TextBlock { Text = "Quality", Foreground = TextBrush, Margin = new Thickness(0, 14, 0, 5) });
            foreach (string s in new[] { "High quality", "Balanced", "Smaller file" }) quality.Items.Add(s); quality.SelectedIndex = 1; quality.Height = 32; right.Children.Add(quality);
            right.Children.Add(new TextBlock { Text = "Trim", FontWeight = FontWeights.SemiBold, FontSize = 16, Foreground = TextBrush, Margin = new Thickness(0, 20, 0, 8) });
            right.Children.Add(new TextBlock { Text = "Start", Foreground = TextBrush }); right.Children.Add(startSlider);
            right.Children.Add(new TextBlock { Text = "End", Foreground = TextBrush, Margin = new Thickness(0, 8, 0, 0) }); right.Children.Add(endSlider);
            trimLabel.Text = "00:00.000 — 00:00.000"; trimLabel.Foreground = new SolidColorBrush(Color.FromRgb(174, 184, 199)); trimLabel.Margin = new Thickness(0, 7, 0, 8); right.Children.Add(trimLabel);
            Button resetCrop = Btn("Reset crop to full video", 240); resetCrop.Click += delegate { ResetCrop(); }; resetCrop.Margin = new Thickness(0, 14, 0, 0); resetCrop.Background = new SolidColorBrush(Color.FromRgb(70, 76, 88)); right.Children.Add(resetCrop);
            Button folder = Btn("Choose output folder", 240); folder.Click += delegate { ChooseFolder(); }; folder.Margin = new Thickness(0, 10, 0, 0); folder.Background = new SolidColorBrush(Color.FromRgb(70, 76, 88)); right.Children.Add(folder);
            convertButton.Content = "Convert all"; convertButton.Height = 44; convertButton.Margin = new Thickness(0, 18, 0, 0); convertButton.Background = Accent; convertButton.Foreground = Brushes.White; convertButton.BorderThickness = new Thickness(0); right.Children.Add(convertButton);
            cancelButton.Content = "Cancel"; cancelButton.Height = 34; cancelButton.Margin = new Thickness(0, 8, 0, 0); cancelButton.IsEnabled = false; right.Children.Add(cancelButton);
            Border rightCard = Card(right); rightCard.Margin = new Thickness(0, 0, 0, 12); Grid.SetColumn(rightCard, 2); body.Children.Add(rightCard);
            root.Children.Add(body);

            StackPanel footer = new StackPanel(); Grid.SetRow(footer, 2);
            progress.Height = 7; progress.Minimum = 0; progress.Maximum = 100; footer.Children.Add(progress);
            status.Text = "Ready — add one or more videos"; status.Foreground = new SolidColorBrush(Color.FromRgb(174, 184, 199)); status.Margin = new Thickness(0, 7, 0, 0); footer.Children.Add(status); root.Children.Add(footer);
            Content = root;
        }

        void BuildPreviewTrimCalipers()
        {
            trimCanvas.Height = 40; trimCanvas.VerticalAlignment = VerticalAlignment.Bottom;
            trimCanvas.Background = new SolidColorBrush(Color.FromArgb(190, 17, 20, 26));
            trimRail.Height = 6; trimRail.Fill = new SolidColorBrush(Color.FromRgb(85, 92, 105)); trimCanvas.Children.Add(trimRail);
            trimBefore.Height = 6; trimBefore.Fill = new SolidColorBrush(Color.FromRgb(70, 73, 80)); trimCanvas.Children.Add(trimBefore);
            trimSelection.Height = 6; trimSelection.Fill = Accent; trimCanvas.Children.Add(trimSelection);
            trimAfter.Height = 6; trimAfter.Fill = new SolidColorBrush(Color.FromRgb(70, 73, 80)); trimCanvas.Children.Add(trimAfter);
            ConfigureCaliper(startCaliper, new SolidColorBrush(Color.FromRgb(65, 210, 140)), "Drag to set trim start");
            ConfigureCaliper(endCaliper, new SolidColorBrush(Color.FromRgb(255, 163, 72)), "Drag to set trim end");
            trimCanvas.Children.Add(startCaliper); trimCanvas.Children.Add(endCaliper);
            ConfigureCaliperLabel(startCaliperLabel, "START", new SolidColorBrush(Color.FromRgb(65, 210, 140)));
            ConfigureCaliperLabel(endCaliperLabel, "END", new SolidColorBrush(Color.FromRgb(255, 163, 72)));
            trimCanvas.Children.Add(startCaliperLabel); trimCanvas.Children.Add(endCaliperLabel);
            startCaliper.DragDelta += delegate(object s, DragDeltaEventArgs e) { MoveCaliper(true, e.HorizontalChange); };
            endCaliper.DragDelta += delegate(object s, DragDeltaEventArgs e) { MoveCaliper(false, e.HorizontalChange); };
            trimCanvas.SizeChanged += delegate { LayoutTrimCalipers(); };
        }

        void ConfigureCaliper(Thumb thumb, Brush colour, string tip)
        {
            thumb.Width = 14; thumb.Height = 28; thumb.Background = colour; thumb.BorderBrush = Brushes.White; thumb.BorderThickness = new Thickness(1); thumb.Cursor = Cursors.SizeWE; thumb.ToolTip = tip;
        }

        void ConfigureCaliperLabel(TextBlock label, string text, Brush colour)
        {
            label.Text = text; label.Foreground = colour; label.FontSize = 10; label.FontWeight = FontWeights.Bold; label.IsHitTestVisible = false;
        }

        Thumb MakeHandle(string tag)
        {
            Thumb t = new Thumb { Width = HandleSize, Height = HandleSize, Tag = tag, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(65, 210, 140)), BorderThickness = new Thickness(2) };
            t.DragDelta += CropDrag;
            return t;
        }

        void WireEvents()
        {
            DragOver += delegate(object s, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            Drop += delegate(object s, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) AddFiles((string[])e.Data.GetData(DataFormats.FileDrop)); };
            files.SelectionChanged += delegate { LoadSelected(); };
            playButton.Click += TogglePlay;
            playSlider.PreviewMouseDown += delegate { seeking = true; };
            playSlider.PreviewMouseUp += delegate { if (media.Source != null) media.Position = TimeSpan.FromSeconds(playSlider.Value); seeking = false; };
            startSlider.ValueChanged += TrimChanged; endSlider.ValueChanged += TrimChanged;
            previewCanvas.SizeChanged += delegate { LayoutCrop(); };
            media.MediaOpened += MediaOpened; media.MediaEnded += delegate { media.Stop(); playing = false; playButton.Content = "▶ Play"; };
            media.MediaFailed += async delegate(object s, ExceptionRoutedEventArgs e) { if (!usingPreviewProxy && !String.IsNullOrEmpty(currentFile)) await PrepareAndLoadPreview(currentFile, ++previewRequest); else ShowPreviewUnavailable(); };
            convertButton.Click += async delegate { await ConvertAll(); };
            cancelButton.Click += delegate { if (activeProcess != null && !activeProcess.HasExited) try { activeProcess.Kill(); } catch { } };
            Closing += delegate { if (activeProcess != null && !activeProcess.HasExited) try { activeProcess.Kill(); } catch { } };
        }

        void CheckTools()
        {
            if (!File.Exists(ffmpegPath) || !File.Exists(ffprobePath)) status.Text = "FFmpeg tools are missing. Keep the tools folder beside VideoConverter.exe.";
        }

        void ShowAbout()
        {
            Window about = new Window { Title = "About Grove Swift Video Converter", Owner = this, Width = 520, Height = 545, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(28, 31, 38)), ShowInTaskbar = false };
            StackPanel panel = new StackPanel { Margin = new Thickness(28) };
            TextBlock title = new TextBlock { Text = "Grove Swift Video Converter", FontSize = 23, FontWeight = FontWeights.Bold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center };
            TextBlock version = new TextBlock { Text = "Version 1.0.0", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 185)), Margin = new Thickness(0, 5, 0, 22), TextAlignment = TextAlignment.Center };
            TextBlock description = new TextBlock { Text = "A free, portable video converter with format conversion, quality presets, precision trimming, visual cropping and batch processing.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, FontSize = 14, LineHeight = 21 };
            TextBlock support = new TextBlock { Text = "Grove Swift Video Converter is developed and maintained by Graham Grove as a personal hobby project.\n\nIf you find it useful, you’re welcome to help support the development of this and my other free apps and educational wikis.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, FontSize = 14, LineHeight = 21, Margin = new Thickness(0, 18, 0, 14) };
            Button coffee = Btn("☕  Buy me a coffee", 180); coffee.HorizontalAlignment = HorizontalAlignment.Center; coffee.Height = 38; coffee.Background = new SolidColorBrush(Color.FromRgb(255, 94, 91)); coffee.Click += delegate { try { Process.Start(new ProcessStartInfo("https://ko-fi.com/groveapps") { UseShellExecute = true }); } catch { MessageBox.Show(about, "Unable to open the web browser.", "Grove Swift Video Converter", MessageBoxButton.OK, MessageBoxImage.Warning); } };
            TextBlock optional = new TextBlock { Text = "Support is entirely optional and does not purchase additional features or services. Contributions are not tax deductible.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(190, 197, 208)), FontSize = 12, LineHeight = 18, Margin = new Thickness(0, 14, 0, 15), TextAlignment = TextAlignment.Center };
            TextBlock ffmpeg = new TextBlock { Text = "Powered by FFmpeg, a separate open-source project distributed under its applicable GPL licence. This application is not affiliated with the FFmpeg project.\n\nCopyright © 2026 Graham Grove", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 185)), FontSize = 11, LineHeight = 16, TextAlignment = TextAlignment.Center };
            Button close = Btn("Close", 90); close.HorizontalAlignment = HorizontalAlignment.Center; close.Margin = new Thickness(0, 18, 0, 0); close.Click += delegate { about.Close(); };
            panel.Children.Add(title); panel.Children.Add(version); panel.Children.Add(description); panel.Children.Add(support); panel.Children.Add(coffee); panel.Children.Add(optional); panel.Children.Add(ffmpeg); panel.Children.Add(close);
            about.Content = panel; about.ShowDialog();
        }

        void OpenFiles()
        {
            OpenFileDialog d = new OpenFileDialog { Multiselect = true, Filter = "Video files|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.wmv;*.m4v;*.mpeg;*.mpg;*.mts;*.ts;*.flv|All files|*.*" };
            if (d.ShowDialog() == true) AddFiles(d.FileNames);
        }

        void AddFiles(string[] paths)
        {
            string[] exts = { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".wmv", ".m4v", ".mpeg", ".mpg", ".mts", ".ts", ".flv" };
            foreach (string p in paths) if (File.Exists(p) && Array.IndexOf(exts, System.IO.Path.GetExtension(p).ToLowerInvariant()) >= 0) { bool exists = false; foreach (VideoItem x in files.Items) if (String.Equals(x.Path, p, StringComparison.OrdinalIgnoreCase)) exists = true; if (!exists) files.Items.Add(new VideoItem { Path = p }); }
            if (files.SelectedIndex < 0 && files.Items.Count > 0) files.SelectedIndex = 0;
            status.Text = files.Items.Count + " video(s) ready";
        }

        void QueueRightClick(object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null && !(source is ListBoxItem)) source = VisualTreeHelper.GetParent(source);
            ListBoxItem item = source as ListBoxItem;
            if (item != null && !item.IsSelected) { files.SelectedItems.Clear(); item.IsSelected = true; }
        }

        void RemoveSelected()
        {
            if (files.SelectedItems.Count == 0) return;
            int first = files.SelectedIndex; List<VideoItem> selected = new List<VideoItem>();
            foreach (VideoItem item in files.SelectedItems) selected.Add(item);
            foreach (VideoItem item in selected) files.Items.Remove(item);
            if (files.Items.Count > 0) files.SelectedIndex = Math.Min(Math.Max(0, first), files.Items.Count - 1);
            else { media.Close(); currentFile = null; duration = 0; playSlider.Value = 0; status.Text = "Ready — add one or more videos"; }
        }

        async void LoadSelected()
        {
            VideoItem item = files.SelectedItem as VideoItem; if (item == null) return;
            int request = ++previewRequest; currentFile = item.Path; playing = false; playButton.Content = "▶ Play"; media.Close(); usingPreviewProxy = false; previewUnavailablePanel.Visibility = Visibility.Collapsed;
            duration = 0; videoWidth = videoHeight = 0; ResetCrop();
            await Probe(item.Path);
            if (request != previewRequest || currentFile != item.Path) return;
            startSlider.Minimum = endSlider.Minimum = playSlider.Minimum = 0;
            startSlider.Maximum = endSlider.Maximum = playSlider.Maximum = Math.Max(duration, .1);
            startSlider.Value = 0; endSlider.Value = duration;
            if (CanPreviewDirectly(item.Path)) LoadPreviewSource(item.Path, false);
            else await PrepareAndLoadPreview(item.Path, request);
        }

        async Task Probe(string path)
        {
            videoCodec = pixelFormat = "";
            string args = "-v error -select_streams v:0 -show_entries stream=width,height,codec_name,pix_fmt:format=duration -of default=noprint_wrappers=1 " + Quote(path);
            string output = await RunCapture(ffprobePath, args);
            Match m = Regex.Match(output, @"width=(\d+)"); if (m.Success) videoWidth = Int32.Parse(m.Groups[1].Value);
            m = Regex.Match(output, @"height=(\d+)"); if (m.Success) videoHeight = Int32.Parse(m.Groups[1].Value);
            m = Regex.Match(output, @"codec_name=([^\r\n]+)"); if (m.Success) videoCodec = m.Groups[1].Value.Trim();
            m = Regex.Match(output, @"pix_fmt=([^\r\n]+)"); if (m.Success) pixelFormat = m.Groups[1].Value.Trim();
            m = Regex.Match(output, @"duration=([\d\.]+)"); if (m.Success) Double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
        }

        bool CanPreviewDirectly(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return (ext == ".mp4" || ext == ".m4v" || ext == ".mov") && videoCodec == "h264" && pixelFormat == "yuv420p";
        }

        async Task PrepareAndLoadPreview(string path, int request)
        {
            status.Text = "Preparing a Windows-compatible preview…";
            string cacheDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GroveSwiftVideoConverter", "PreviewCache");
            Directory.CreateDirectory(cacheDir);
            FileInfo info = new FileInfo(path); string key = StableKey(path + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks);
            string proxy = System.IO.Path.Combine(cacheDir, key + ".mp4");
            if (!File.Exists(proxy) || new FileInfo(proxy).Length < 1024)
            {
                string partial = proxy + ".building";
                string args = "-hide_banner -loglevel error -y -i " + Quote(path) + " -map 0:v:0 -map 0:a:0? -vf " + Quote("scale=1280:-2:force_original_aspect_ratio=decrease") + " -c:v libx264 -preset ultrafast -crf 30 -pix_fmt yuv420p -c:a aac -b:a 96k -movflags +faststart -f mp4 " + Quote(partial);
                CaptureResult result = await RunCaptureWithTimeout(ffmpegPath, args, 10000);
                if (result.TimedOut)
                {
                    if (File.Exists(partial)) try { File.Delete(partial); } catch { }
                    if (request == previewRequest && currentFile == path) ShowPreviewUnavailable();
                    return;
                }
                if (File.Exists(partial) && new FileInfo(partial).Length >= 1024) { if (File.Exists(proxy)) File.Delete(proxy); File.Move(partial, proxy); }
                else { if (request == previewRequest && currentFile == path) ShowPreviewUnavailable(); return; }
            }
            if (request != previewRequest || currentFile != path) return;
            LoadPreviewSource(proxy, true);
        }

        void LoadPreviewSource(string path, bool proxy)
        {
            previewUnavailablePanel.Visibility = Visibility.Collapsed; usingPreviewProxy = proxy; media.Source = new Uri(path); media.Position = TimeSpan.Zero; media.Play(); media.Pause();
            status.Text = proxy ? "Loaded compatible preview for " + System.IO.Path.GetFileName(currentFile) : "Loaded " + System.IO.Path.GetFileName(currentFile);
        }

        void ShowPreviewUnavailable()
        {
            media.Close(); playing = false; playButton.Content = "▶ Play"; previewUnavailablePanel.Visibility = Visibility.Visible;
            status.Text = "Preview unavailable — this video can still be converted.";
        }

        Task<CaptureResult> RunCaptureWithTimeout(string exe, string args, int timeoutMilliseconds)
        {
            return Task.Run(delegate
            {
                StringBuilder output = new StringBuilder();
                ProcessStartInfo psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (Process p = new Process { StartInfo = psi })
                {
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                    try
                    {
                        p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
                        if (!p.WaitForExit(timeoutMilliseconds)) { try { p.Kill(); } catch { } try { p.WaitForExit(); } catch { } return new CaptureResult { Output = output.ToString(), TimedOut = true, ExitCode = -1 }; }
                        p.WaitForExit(); return new CaptureResult { Output = output.ToString(), TimedOut = false, ExitCode = p.ExitCode };
                    }
                    catch (Exception ex) { return new CaptureResult { Output = ex.ToString(), TimedOut = false, ExitCode = -1 }; }
                }
            });
        }

        string StableKey(string value)
        {
            unchecked { uint a = 2166136261, b = 16777619; foreach (char c in value) { a = (a ^ c) * 16777619; b = (b + c) * 2166136261; } return a.ToString("x8") + b.ToString("x8"); }
        }

        Task<string> RunCapture(string exe, string args)
        {
            return Task.Run(delegate { ProcessStartInfo psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }; using (Process p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd(); string e = p.StandardError.ReadToEnd(); p.WaitForExit(); return o + "\n" + e; } });
        }

        void MediaOpened(object sender, RoutedEventArgs e)
        {
            if (duration <= 0 && media.NaturalDuration.HasTimeSpan) duration = media.NaturalDuration.TimeSpan.TotalSeconds;
            if (videoWidth <= 0) { videoWidth = media.NaturalVideoWidth; videoHeight = media.NaturalVideoHeight; }
            startSlider.Maximum = endSlider.Maximum = playSlider.Maximum = Math.Max(duration, .1); if (endSlider.Value <= 0) endSlider.Value = duration; LayoutCrop(); UpdateLabels();
        }

        void TogglePlay(object sender, RoutedEventArgs e)
        {
            if (media.Source == null) return;
            if (playing) { media.Pause(); playing = false; playButton.Content = "▶ Play"; }
            else { if (media.Position.TotalSeconds >= endSlider.Value - .05) media.Position = TimeSpan.FromSeconds(startSlider.Value); media.Play(); playing = true; playButton.Content = "❚❚ Pause"; }
        }

        void StepPosition(double seconds)
        {
            if (media.Source == null) return;
            if (playing) media.Pause();
            playing = false; playButton.Content = "▶ Play";
            double current = playSlider.Value;
            bool moveStart = Math.Abs(current - startSlider.Value) <= .002;
            bool moveEnd = Math.Abs(current - endSlider.Value) <= .002;
            double target = Math.Max(0, Math.Min(duration, current + seconds));
            if (moveStart) target = Math.Min(target, endSlider.Value);
            if (moveEnd) target = Math.Max(target, startSlider.Value);
            media.Position = TimeSpan.FromSeconds(target); playSlider.Value = target;
            if (moveStart) startSlider.Value = target;
            if (moveEnd) endSlider.Value = target;
            timeLabel.Text = FmtTenths(target) + " / " + FmtTenths(duration);
        }

        void TimerTick(object sender, EventArgs e)
        {
            if (media.Source == null) return;
            double p = CurrentPosition(); if (playing && p >= endSlider.Value && endSlider.Value > startSlider.Value) { media.Pause(); media.Position = TimeSpan.FromSeconds(startSlider.Value); playing = false; playButton.Content = "▶ Play"; p = startSlider.Value; }
            if (!seeking) playSlider.Value = Math.Min(playSlider.Maximum, p);
            timeLabel.Text = FmtTenths(p) + " / " + FmtTenths(duration);
        }

        double CurrentPosition() { try { return media.Position.TotalSeconds; } catch { return 0; } }
        string Fmt(double seconds, bool ms) { if (Double.IsNaN(seconds) || Double.IsInfinity(seconds)) seconds = 0; TimeSpan t = TimeSpan.FromSeconds(Math.Max(0, seconds)); return ms ? String.Format("{0:00}:{1:00}:{2:00}.{3:000}", (int)t.TotalHours, t.Minutes, t.Seconds, t.Milliseconds) : String.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds); }
        string FmtTenths(double seconds) { if (Double.IsNaN(seconds) || Double.IsInfinity(seconds)) seconds = 0; TimeSpan t = TimeSpan.FromSeconds(Math.Max(0, seconds)); return String.Format("{0:00}:{1:00}:{2:00}.{3:0}", (int)t.TotalHours, t.Minutes, t.Seconds, t.Milliseconds / 100); }

        void TrimChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (startSlider.Value > endSlider.Value) { if (sender == startSlider) endSlider.Value = startSlider.Value; else startSlider.Value = endSlider.Value; }
            UpdateLabels();
        }
        void UpdateLabels() { trimLabel.Text = Fmt(startSlider.Value, true) + " — " + Fmt(endSlider.Value, true) + "  (" + Fmt(Math.Max(0, endSlider.Value - startSlider.Value), false) + ")"; LayoutTrimCalipers(); }

        void MoveCaliper(bool isStart, double horizontalChange)
        {
            double usable = Math.Max(1, trimCanvas.ActualWidth - 36); if (duration <= 0) return;
            double delta = horizontalChange / usable * duration;
            if (isStart) startSlider.Value = Math.Max(0, Math.Min(endSlider.Value, startSlider.Value + delta));
            else endSlider.Value = Math.Min(duration, Math.Max(startSlider.Value, endSlider.Value + delta));
            media.Position = TimeSpan.FromSeconds(isStart ? startSlider.Value : endSlider.Value);
        }

        void LayoutTrimCalipers()
        {
            double width = trimCanvas.ActualWidth; if (width <= 36) return;
            double left = 18, usable = width - 36, y = 27;
            double startRatio = duration > 0 ? startSlider.Value / duration : 0;
            double endRatio = duration > 0 ? endSlider.Value / duration : 1;
            double sx = left + Math.Max(0, Math.Min(1, startRatio)) * usable;
            double ex = left + Math.Max(0, Math.Min(1, endRatio)) * usable;
            Canvas.SetLeft(trimRail, left); Canvas.SetTop(trimRail, y); trimRail.Width = usable;
            Canvas.SetLeft(trimBefore, left); Canvas.SetTop(trimBefore, y); trimBefore.Width = Math.Max(0, sx-left);
            Canvas.SetLeft(trimSelection, sx); Canvas.SetTop(trimSelection, y); trimSelection.Width = Math.Max(0, ex-sx);
            Canvas.SetLeft(trimAfter, ex); Canvas.SetTop(trimAfter, y); trimAfter.Width = Math.Max(0, left+usable-ex);
            Canvas.SetLeft(startCaliper, sx-startCaliper.Width/2); Canvas.SetTop(startCaliper, 12);
            Canvas.SetLeft(endCaliper, ex-endCaliper.Width/2); Canvas.SetTop(endCaliper, 12);
            Canvas.SetLeft(startCaliperLabel, Math.Max(2, Math.Min(width-72, sx-15))); Canvas.SetTop(startCaliperLabel, 0);
            Canvas.SetLeft(endCaliperLabel, Math.Max(42, Math.Min(width-27, ex-9))); Canvas.SetTop(endCaliperLabel, 0);
        }

        Rect VideoDisplayRect()
        {
            double cw = previewCanvas.ActualWidth, ch = previewCanvas.ActualHeight; if (cw <= 0 || ch <= 0 || videoWidth <= 0 || videoHeight <= 0) return new Rect(0, 0, Math.Max(0, cw), Math.Max(0, ch));
            double scale = Math.Min(cw / videoWidth, ch / videoHeight); double w = videoWidth * scale, h = videoHeight * scale; return new Rect((cw - w) / 2, (ch - h) / 2, w, h);
        }

        void ResetCrop() { cropX = cropY = 0; cropW = cropH = 1; LayoutCrop(); }
        void LayoutCrop()
        {
            Rect v = VideoDisplayRect(); if (v.Width <= 0 || v.Height <= 0) return;
            double x = v.X + cropX * v.Width, y = v.Y + cropY * v.Height, w = cropW * v.Width, h = cropH * v.Height;
            Canvas.SetLeft(cropRect, x); Canvas.SetTop(cropRect, y); cropRect.Width = w; cropRect.Height = h;
            double[,] pos = { { x,y }, { x+w/2,y }, { x+w,y }, { x+w,y+h/2 }, { x+w,y+h }, { x+w/2,y+h }, { x,y+h }, { x,y+h/2 } };
            for (int i=0;i<cropHandles.Count;i++) { Canvas.SetLeft(cropHandles[i], pos[i,0]-HandleSize/2); Canvas.SetTop(cropHandles[i], pos[i,1]-HandleSize/2); }
            int px = (int)Math.Round(cropX * videoWidth), py = (int)Math.Round(cropY * videoHeight), pw = (int)Math.Round(cropW * videoWidth), ph = (int)Math.Round(cropH * videoHeight);
            cropLabel.Text = cropW > .999 && cropH > .999 ? "Crop: full frame" : String.Format("Crop: {0} × {1} at ({2}, {3})", pw, ph, px, py);
        }

        void CropDrag(object sender, DragDeltaEventArgs e)
        {
            Rect v = VideoDisplayRect(); if (v.Width <= 0 || v.Height <= 0) return;
            double dx = e.HorizontalChange / v.Width, dy = e.VerticalChange / v.Height, minW = 24/v.Width, minH = 24/v.Height;
            string tag = (string)((Thumb)sender).Tag;
            if (tag.Contains("L")) { double nx = Math.Max(0, Math.Min(cropX + cropW - minW, cropX + dx)); cropW += cropX - nx; cropX = nx; }
            if (tag.Contains("R")) cropW = Math.Max(minW, Math.Min(1-cropX, cropW+dx));
            if (tag.Contains("T")) { double ny = Math.Max(0, Math.Min(cropY + cropH - minH, cropY + dy)); cropH += cropY - ny; cropY = ny; }
            if (tag.Contains("B")) cropH = Math.Max(minH, Math.Min(1-cropY, cropH+dy));
            LayoutCrop();
        }

        void ChooseFolder() { using (Forms.FolderBrowserDialog d = new Forms.FolderBrowserDialog()) { d.Description = "Choose where converted videos will be saved"; if (d.ShowDialog() == Forms.DialogResult.OK) { outputFolder = d.SelectedPath; status.Text = "Output: " + outputFolder; } } }

        async Task ConvertAll()
        {
            if (converting || files.Items.Count == 0) { if (files.Items.Count == 0) MessageBox.Show("Add at least one video first.", "Video Converter"); return; }
            if (!File.Exists(ffmpegPath)) { MessageBox.Show("FFmpeg was not found in the tools folder.", "Missing FFmpeg", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            converting = true; convertButton.IsEnabled = false; cancelButton.IsEnabled = true;
            int index = 0; bool cancelled = false;
            foreach (VideoItem item in files.Items)
            {
                index++; progress.Value = (index-1)*100.0/files.Items.Count; status.Text = "Converting " + index + " of " + files.Items.Count + ": " + item;
                string itemOutputFolder = String.IsNullOrEmpty(outputFolder) ? System.IO.Path.GetDirectoryName(item.Path) : outputFolder;
                Directory.CreateDirectory(itemOutputFolder);
                string ext = OutputExtension(); string output = UniquePath(System.IO.Path.Combine(itemOutputFolder, System.IO.Path.GetFileNameWithoutExtension(item.Path) + "_converted" + ext));
                double clipDuration = Math.Max(.01, endSlider.Value-startSlider.Value); string args = BuildArguments(item.Path, output, startSlider.Value, clipDuration);
                int code = await RunFfmpeg(args, clipDuration, index-1, files.Items.Count, itemOutputFolder);
                if (code != 0) { if (activeProcess == null || activeProcess.HasExited) { status.Text = "Conversion failed for " + item + ". See conversion-error.txt."; } cancelled = true; break; }
            }
            converting = false; convertButton.IsEnabled = true; cancelButton.IsEnabled = false; activeProcess = null;
            if (!cancelled) { string destination = String.IsNullOrEmpty(outputFolder) ? "beside each original video" : outputFolder; progress.Value = 100; status.Text = "Finished — files saved " + destination; MessageBox.Show("Conversion complete.\n\nFiles were saved " + destination + ".", "Video Converter", MessageBoxButton.OK, MessageBoxImage.Information); }
            else status.Text = "Conversion stopped.";
        }

        string OutputExtension() { string f = (string)format.SelectedItem; if (f.StartsWith("MP3")) return ".mp3"; return "." + f.ToLowerInvariant(); }

        string BuildArguments(string input, string output, double start, double length)
        {
            string f = (string)format.SelectedItem; int qi = quality.SelectedIndex; StringBuilder a = new StringBuilder();
            a.Append("-hide_banner -y -ss ").Append(start.ToString("0.###", CultureInfo.InvariantCulture)).Append(" -i ").Append(Quote(input));
            a.Append(" -t ").Append(length.ToString("0.###", CultureInfo.InvariantCulture));
            bool audioOnly = f.StartsWith("MP3");
            string cropFilter = String.Format(CultureInfo.InvariantCulture, "crop=trunc(iw*{0:0.######}/2)*2:trunc(ih*{1:0.######}/2)*2:trunc(iw*{2:0.######}/2)*2:trunc(ih*{3:0.######}/2)*2", cropW, cropH, cropX, cropY);
            if (!audioOnly && f != "GIF" && (cropW < .999 || cropH < .999 || cropX > .001 || cropY > .001))
            {
                a.Append(" -vf ").Append(Quote(cropFilter));
            }
            if (f == "MP4" || f == "MOV" || f == "MKV") a.Append(" -c:v libx264 -preset medium -crf ").Append(qi==0?"18":qi==1?"23":"29").Append(" -c:a aac -b:a ").Append(qi==2?"128k":"192k").Append(" -pix_fmt yuv420p");
            else if (f == "WebM") a.Append(" -c:v libvpx-vp9 -crf ").Append(qi==0?"24":qi==1?"31":"38").Append(" -b:v 0 -c:a libopus -b:a ").Append(qi==2?"96k":"160k");
            else if (f == "AVI") a.Append(" -c:v mpeg4 -q:v ").Append(qi==0?"2":qi==1?"5":"8").Append(" -c:a libmp3lame -q:a 3");
            else if (f == "GIF") a.Append(" -an -vf ").Append(Quote((cropW < .999 || cropH < .999 || cropX > .001 || cropY > .001 ? cropFilter+"," : "") + (qi==0?"fps=15":"fps=10") + ",scale='min(1280,iw)':-2:flags=lanczos"));
            else if (audioOnly) a.Append(" -vn -c:a libmp3lame -q:a ").Append(qi==0?"0":qi==1?"2":"5");
            a.Append(" -progress pipe:1 -nostats ").Append(Quote(output)); return a.ToString();
        }

        int Even(int n) { return Math.Max(0, n - (n % 2)); }
        string Quote(string s) { return "\"" + s.Replace("\"", "\\\"") + "\""; }
        string UniquePath(string p) { if (!File.Exists(p)) return p; string dir=System.IO.Path.GetDirectoryName(p), name=System.IO.Path.GetFileNameWithoutExtension(p), ext=System.IO.Path.GetExtension(p); int i=2; while(File.Exists(System.IO.Path.Combine(dir,name+"_"+i+ext))) i++; return System.IO.Path.Combine(dir,name+"_"+i+ext); }

        Task<int> RunFfmpeg(string args, double clipDuration, int completed, int total, string errorFolder)
        {
            return Task.Run(delegate
            {
                StringBuilder errors = new StringBuilder(); ProcessStartInfo psi = new ProcessStartInfo(ffmpegPath,args) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
                activeProcess = new Process { StartInfo=psi }; activeProcess.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if(e.Data!=null) lock(errors) errors.AppendLine(e.Data); };
                try { activeProcess.Start(); activeProcess.BeginErrorReadLine(); string line; while((line=activeProcess.StandardOutput.ReadLine())!=null) { if(line.StartsWith("out_time_ms=")) { long us; if(Int64.TryParse(line.Substring(12),out us)) Dispatcher.BeginInvoke(new Action(delegate { progress.Value=(completed + Math.Min(1,us/1000000.0/clipDuration))*100.0/total; })); } } activeProcess.WaitForExit(); if(activeProcess.ExitCode!=0) File.WriteAllText(System.IO.Path.Combine(errorFolder,"conversion-error.txt"), errors.ToString()); return activeProcess.ExitCode; }
                catch(Exception ex) { File.WriteAllText(System.IO.Path.Combine(errorFolder,"conversion-error.txt"), ex+"\r\n"+errors); return -1; }
            });
        }
    }
}
