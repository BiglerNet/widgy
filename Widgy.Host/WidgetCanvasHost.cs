using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace Widgy.Host
{
    internal class WidgetCanvasHost
    {
        public Canvas Panel { get; } = new Canvas();
        public Image Image { get; } = new Image();
        public int Width { get; }
        public int Height { get; }

        public WidgetCanvasHost(int width, int height)
        {
            Width = width;
            Height = height;
            Image.Width = width;
            Image.Height = height;
            Image.Stretch = Stretch.UniformToFill;
            Panel.Children.Add(Image);

            // Solid initial frame (avoid any GPU surface on the UI thread).
            Image.Source = MakeSolid(width, height, Color.FromRgb(0x1a, 0x1a, 0x2e));
        }

        // Build a WPF WriteableBitmap from a BGRA byte buffer.
        // This runs on the UI thread but only touches WPF types (no SkiaSharp GPU).
        private static WriteableBitmap MakeSolid(int w, int h, Color color)
        {
            var wbmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            byte[] buf = new byte[w * 4 * h];
            for (int i = 0; i < buf.Length; i += 4)
            {
                buf[i] = color.B; buf[i + 1] = color.G; buf[i + 2] = color.R; buf[i + 3] = 255;
            }
            wbmp.WritePixels(new Int32Rect(0, 0, w, h), buf, w * 4, 0);
            return wbmp;
        }

        // Receive rendered pixels (BGRA) produced on a worker thread and display them.
        public void Render(byte[] bgra, int w, int h, int rowBytes)
        {
            Action apply = () =>
            {
                try
                {
                    var wbmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                    wbmp.WritePixels(new Int32Rect(0, 0, w, h), bgra, rowBytes, 0);
                    Image.Source = wbmp;
                }
                catch (Exception ex)
                {
                    Log("Render apply: " + ex.Message);
                }
            };
            if (Image.Dispatcher.CheckAccess()) apply();
            else Image.Dispatcher.Invoke(apply);

            if (!_reported)
            {
                _reported = true;
                // Quick self-check on the UI thread.
                try
                {
                    var wbmp = Image.Source as WriteableBitmap;
                    if (wbmp != null)
                    {
                        int white = 0;
                        var buf = new byte[w * 4 * h];
                        wbmp.CopyPixels(buf, w * 4, 0);
                        for (int y = 0; y < h; y += 4)
                            for (int x = 0; x < w; x += 4)
                            {
                                int i = (y * w + x) * 4;
                                if (buf[i + 2] > 200 && buf[i + 1] > 200 && buf[i] > 200) white++;
                            }
                        Log($"Render applied {w}x{h}: white~={white}");
                    }
                }
                catch { }
            }
        }

        private static bool _reported = false;

        private static void Log(string message)
        {
            try
            {
                var path = System.IO.Path.Combine(AppContext.BaseDirectory, "widgy.log");
                System.IO.File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            }
            catch { }
        }
    }
}
