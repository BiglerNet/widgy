using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Widgy.Core.Config;
using Widgy.Core.Layout;
using Widgy.Core.Plugin;

namespace Widgy.Host
{
    public partial class App
    {
        private GridLayoutManager _layoutManager;
        private ConfigStore _configStore;
        private WidgetPluginLoader _pluginLoader;

        protected override async void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            var screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
            var screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
            _layoutManager = new GridLayoutManager(screenWidth, screenHeight);

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var pluginsDir = Path.Combine(appDir, "plugins");
            Directory.CreateDirectory(pluginsDir);

            var configPath = Path.Combine(appDir, "widgy-config.json");
            _configStore = new ConfigStore(configPath);

            _pluginLoader = new WidgetPluginLoader();
            await _pluginLoader.ScanAndLoadPlugins(pluginsDir);

            var types = _pluginLoader.GetRegisteredWidgetTypes();
            foreach (var t in types)
                System.Diagnostics.Debug.WriteLine("Loaded widget: " + t);
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(appDir, "widgy.log"),
                    $"[{DateTime.Now:HH:mm:ss.fff}] plugins dir={pluginsDir} registered=[{string.Join(", ", types)}] config={configPath} widgets={_configStore.Config.Pages[_configStore.Config.ActivePage].Widgets.Count}\n");
            }
            catch { }

            var mw = new MainWindow(_layoutManager, _configStore, _pluginLoader);

            // Place the window on the configured monitor.
            // Prefer a named monitor (stable across AllScreens reordering); fall back to 1-based index.
            var screens = System.Windows.Forms.Screen.AllScreens;
            System.IO.File.AppendAllText(System.IO.Path.Combine(appDir, "widgy.log"), $"[{DateTime.Now:HH:mm:ss.fff}] screens={screens.Length}\n");
            for (int si = 0; si < screens.Length; si++)
            {
                var sb = screens[si].Bounds;
                System.IO.File.AppendAllText(System.IO.Path.Combine(appDir, "widgy.log"),
                    $"[{DateTime.Now:HH:mm:ss.fff}]   [{si}] {screens[si].DeviceName} X={sb.Left} Y={sb.Top} W={sb.Width} H={sb.Height} primary={screens[si].Primary}\n");
            }

            System.Windows.Forms.Screen target = null;
            var monName = (_configStore.Config?.MonitorName ?? "").Trim().ToLowerInvariant();
            if (monName == "primary") target = System.Windows.Forms.Screen.PrimaryScreen;
            else if (monName.Length > 0)
            {
                Func<System.Windows.Forms.Screen, double> metric = monName switch
                {
                    "tallest" or "tallest-portrait" => s => (s.Bounds.Height >= s.Bounds.Width ? s.Bounds.Height : -1),
                    "widest" => s => s.Bounds.Width,
                    "largest" => s => (double)s.Bounds.Width * s.Bounds.Height,
                    _ => s => -1
                };
                var best = -1.0;
                foreach (var s in screens)
                {
                    var mval = metric(s);
                    if (mval > best) { best = mval; target = s; }
                }
                if (target == null || best < 0) target = null;
            }
            if (target == null)
            {
                int monitor = Math.Max(1, _configStore.Config?.Monitor ?? 1);
                if (monitor >= 1 && monitor <= screens.Length) target = screens[monitor - 1];
                else target = System.Windows.Forms.Screen.PrimaryScreen;
            }

            if (target != null)
            {
                var b = target.Bounds;
                double scale = (double)System.Windows.SystemParameters.VirtualScreenWidth
                             / System.Windows.Forms.SystemInformation.VirtualScreen.Width;
                mw.WindowState = System.Windows.WindowState.Normal;
                mw.Left = b.Left / scale;
                mw.Top = b.Top / scale;
                mw.Width = b.Width / scale;
                mw.Height = b.Height / scale;
                System.IO.File.AppendAllText(System.IO.Path.Combine(appDir, "widgy.log"),
                    $"[{DateTime.Now:HH:mm:ss.fff}] placed on {target.DeviceName} {b.Width}x{b.Height} at {b.Left},{b.Top} (scale={scale:F3})\n");
            }

            mw.Show();
        }
    }
}
