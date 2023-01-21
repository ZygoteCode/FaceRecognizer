using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using FaceRecognitionSharp;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public partial class MainForm : Form
{
    [DllImport("psapi.dll")]
    static extern int EmptyWorkingSet(IntPtr hwProc);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);

    private VideoCapture _capture;
    private Mat _frame;

    public MainForm()
    {
        InitializeComponent();
        FaceRecognition.Initialize();
        CheckForIllegalCrossThreadCalls = false;
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

        _frame = new Mat();
        _capture = new VideoCapture(0);
        _capture.Open(0);

        Thread captureCameraThread = new Thread(CaptureCamera);
        captureCameraThread.Priority = ThreadPriority.Highest;
        captureCameraThread.Start();

        Thread clearRamThread = new Thread(ClearRam);
        clearRamThread.Priority = ThreadPriority.Highest;
        clearRamThread.Start();
    }

    public void ClearRam()
    {
        while (true)
        {
            Thread.Sleep(100);
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration);
            GC.WaitForPendingFinalizers();
            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (UIntPtr)0xFFFFFFFF, (UIntPtr)0xFFFFFFFF);
        }
    }

    public void CaptureCamera()
    {
        if (_capture.IsOpened())
        {
            while (true)
            {
                _capture.Read(_frame);
                pictureBox1.Image = FaceRecognition.DrawRectsAndLandmarksToFaces(ResizeImage(BitmapConverter.ToBitmap(_frame), pictureBox1.Width, pictureBox1.Height), new DlibDotNet.RgbPixel(255, 255, 255), 1, new DlibDotNet.RgbPixel(255, 255, 0), 4);
            }
        }
    }

    public static Bitmap ResizeImage(Image image, int width, int height)
    {
        var destRect = new Rectangle(0, 0, width, height);
        var destImage = new Bitmap(width, height);

        destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

        using (var graphics = Graphics.FromImage(destImage))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (var wrapMode = new ImageAttributes())
            {
                wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
            }
        }

        return destImage;
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        Process.GetCurrentProcess().Kill();
    }
}