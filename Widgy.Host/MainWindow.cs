using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Widgy.Core.Config;
using Widgy.Core.Diagnostics;
using Widgy.Core.Layout;
using Widgy.Core.Plugin;
using Widgy.Core.Rendering;

namespace Widgy.Host
{
    public sealed class MainWindow : Window
    {
        private readonly ConfigStore _configStore;
        private readonly WidgetPluginLoader _plugins;
        private readonly Int32Rect _targetBounds;
        private readonly ThemeColors _theme = ThemeColors.DefaultDark;
        private readonly Canvas _surface = new();
        private readonly List<WidgetView> _views = new();

        internal MainWindow(ConfigStore configStore, WidgetPluginLoader plugins, MonitorInfo target)
        {
            _configStore = configStore;
            _plugins = plugins;
            _targetBounds = target.Bounds;

            Title = "Widgy";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Background = new SolidColorBrush(Color.FromRgb(_theme.BackgroundColor.Red, _theme.BackgroundColor.Green, _theme.BackgroundColor.Blue));
            Content = _surface;

            SourceInitialized += (_, _) => MonitorPlacement.Cover(this, _targetBounds);
            // Moving onto a monitor with a different DPI makes WPF resize the window by the DPI ratio;
            // re-apply the exact physical bounds once that settles.
            DpiChanged += (_, _) => Dispatcher.BeginInvoke(() => MonitorPlacement.Cover(this, _targetBounds));
            Loaded += OnLoaded;
            SizeChanged += (_, e) => { if (e.NewSize.Width >= 16 && e.NewSize.Height >= 16) RebuildLayout(); };

            _configStore.ConfigChanged += _ => Dispatcher.BeginInvoke(RebuildLayout);
            _plugins.PluginsChanged += () => Dispatcher.BeginInvoke(RebuildLayout);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var actual = MonitorPlacement.GetWindowBounds(this);
            var dpi = VisualTreeHelper.GetDpi(this);
            WidgyLog.Info($"Window placed at {actual.X},{actual.Y} {actual.Width}x{actual.Height} physical " +
                          $"(target {_targetBounds.X},{_targetBounds.Y} {_targetBounds.Width}x{_targetBounds.Height}), " +
                          $"{ActualWidth:0.#}x{ActualHeight:0.#} DIPs, scale {dpi.DpiScaleX:0.##}");
            if (!actual.Equals(_targetBounds))
                WidgyLog.Warn("Window bounds do not match the target monitor.");
        }

        private void RebuildLayout()
        {
            foreach (var view in _views) view.Dispose();
            _views.Clear();
            _surface.Children.Clear();

            var page = _configStore.Config.CurrentPage;
            if (page == null || ActualWidth < 16 || ActualHeight < 16) return;

            // Layout is computed in DIPs; each SKElement then renders at the monitor's physical resolution.
            var screen = new System.Drawing.Size((int)ActualWidth, (int)ActualHeight);
            var layout = new GridLayoutManager(screen.Width, screen.Height).RenderWidgetLayout(page.Widgets, screen);

            for (var i = 0; i < page.Widgets.Count; i++)
            {
                var config = page.Widgets[i];
                if (!config.IsVisible) continue;

                var descriptor = _plugins.GetDescriptor(config.WidgetTypeId);
                if (descriptor == null)
                {
                    WidgyLog.Warn($"Widget type '{config.WidgetTypeId}' is not registered; leaving its cell empty. " +
                                  $"Registered: [{string.Join(", ", _plugins.GetRegisteredWidgetTypes())}]");
                    continue;
                }

                try
                {
                    var widget = _plugins.CreateWidget(config)!;
                    var item = layout[i];
                    var view = new WidgetView(widget, descriptor, _theme)
                    {
                        Width = item.Size.Width,
                        Height = item.Size.Height,
                    };
                    Canvas.SetLeft(view, item.Position.X);
                    Canvas.SetTop(view, item.Position.Y);
                    _surface.Children.Add(view);
                    _views.Add(view);
                    WidgyLog.Info($"Placed '{config.WidgetTypeId}' at grid ({config.Col},{config.Row}) {config.Width}x{config.Height} " +
                                  $"-> {item.Position.X},{item.Position.Y} {item.Size.Width}x{item.Size.Height} DIPs");
                }
                catch (Exception ex)
                {
                    WidgyLog.Error($"Failed to create widget '{config.WidgetTypeId}'", ex);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (var view in _views) view.Dispose();
            _views.Clear();
            base.OnClosed(e);
        }
    }
}
