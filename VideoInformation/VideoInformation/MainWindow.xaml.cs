using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DirectShowLib;
using DirectShowLib.DES;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using System.ComponentModel;

namespace VideoInformation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public class Video
        {
            public string File { get; set; }
            public String FileLocation
            {
                get
                {
                    return String.Join("\\", File.Split('\\').Reverse().Skip(1).Reverse());
                }
            }
            public String FileName
            {
                get
                {
                    return File.Split('\\').Last();
                }
            }
            public String Type
            {
                get
                {
                    return File.Split('.').Last();
                }
            }
            public long Size { get; set; }
            public double Duration { get; set; }
            public int TotalBitRate
            {
                get
                {
                    return (int)Math.Round((double)(Size / Duration));
                }
            }
            public int Height { get; set; }
            public int Width { get; set; }
        }

        private List<string> files;

        public MainWindow()
        {
            InitializeComponent();
        }

        public delegate void ChangeMainDataGridCallback();
        public void ChangeMainDataGrid(List<Video> source)
        {
            MainDataGrid.ItemsSource = source;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            FilePathTextBox.Text = dialog.SelectedPath;

            files = Directory.GetFiles(dialog.SelectedPath, "*", SearchOption.AllDirectories).Where(a => a.EndsWith(".mkv") || a.EndsWith(".avi") || a.EndsWith(".m4v") || a.EndsWith(".mp4")).ToList();
            MainProgressBar.Maximum = files.Count;

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += DoWorkHandler;            
            worker.WorkerReportsProgress = true;
            worker.ProgressChanged += (s, exx) => { MainProgressBar.Value++; };

            worker.RunWorkerAsync();       
        }

        [STAThreadAttribute]
        public void DoWorkHandler(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            var results = files.AsParallel().Select(a => GetVideoFromFile(a, worker)).ToList();

            MainDataGrid.Dispatcher.BeginInvoke(
              DispatcherPriority.Background,
              new Action(() => MainDataGrid.ItemsSource = results));
        }

        private Video GetVideoFromFile(String fileName, BackgroundWorker worker)
        {
            worker.ReportProgress(1);

            FileInfo fileInfo = new FileInfo(fileName);

            try
            {
                var mediaDet = (IMediaDet)new MediaDet();
                DsError.ThrowExceptionForHR(mediaDet.put_Filename(fileName));

                // find the video stream in the file                
                var type = Guid.Empty;
                for (int index = 0; index < 1000 && type != MediaType.Video; index++)
                {
                    mediaDet.put_CurrentStream(index);
                    mediaDet.get_StreamType(out type);
                }

                // retrieve some measurements from the video
                double frameRate;
                mediaDet.get_FrameRate(out frameRate);

                var mediaType = new AMMediaType();
                mediaDet.get_StreamMediaType(mediaType);
                var videoInfo = (VideoInfoHeader)Marshal.PtrToStructure(mediaType.formatPtr, typeof(VideoInfoHeader));
                DsUtils.FreeAMMediaType(mediaType);
                var width = videoInfo.BmiHeader.Width;
                var height = videoInfo.BmiHeader.Height;

                double mediaLength;
                mediaDet.get_StreamLength(out mediaLength);
                var frameCount = (int)(frameRate * mediaLength);
                var duration = frameCount / frameRate;

                return new Video { File = fileName, Size = fileInfo.Length, Duration = duration, Height = height, Width = width };
            }
            catch (Exception e)
            {
                //TODO fix this
            }
            return new Video { File = fileName, Size = fileInfo.Length, Duration = 0, Height = 0, Width = 0 };
        }
    }
}
