using SchoolScheduleApp.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SchoolScheduleApp.Core
{
    public static class ToastService
    {
        private const double Margin = 16;
        private const double ToastSpacing = 10;
        private static readonly object Locker = new();
        private static readonly List<ToastNotificationWindow> ActiveToasts = new();

        public static void Show(
            string message,
            string title = "Уведомление",
            bool isError = false,
            string? actionText = null,
            Action? action = null)
        {
            Application.Current?.Dispatcher.InvokeAsync(
                () => ShowInternal(message, title, isError, actionText, action),
                DispatcherPriority.Normal);
        }

        private static async void ShowInternal(
            string message,
            string title,
            bool isError,
            string? actionText,
            Action? action)
        {
            var owner = GetHostWindow();
            var toast = new ToastNotificationWindow(title, ShortenMessage(message), isError, actionText, action)
            {
                Opacity = 0,
                Topmost = true,
                ShowInTaskbar = false
            };

            toast.Closed += (_, _) =>
            {
                lock (Locker)
                {
                    ActiveToasts.Remove(toast);
                }

                RepositionToasts(GetHostWindow());
            };

            if (owner != null)
            {
                toast.Owner = owner;
            }

            toast.Show();
            toast.UpdateLayout();

            lock (Locker)
            {
                ActiveToasts.Add(toast);
            }

            RepositionToasts(owner);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
            toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            var durationMs = action != null ? 8000 : 3200;
            await Task.Delay(durationMs);
            if (!toast.IsLoaded)
            {
                return;
            }

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(320));
            fadeOut.Completed += (_, _) =>
            {
                if (toast.IsLoaded)
                {
                    toast.Close();
                }
            };

            toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private static Window? GetHostWindow()
        {
            var main = Application.Current?.MainWindow;
            if (main != null
                && main.IsVisible
                && main.WindowState != WindowState.Minimized
                && main is not ToastNotificationWindow)
            {
                return main;
            }

            var windows = Application.Current?.Windows.OfType<Window>()
                .Where(w => w.IsVisible
                            && w.WindowState != WindowState.Minimized
                            && w is not ToastNotificationWindow)
                .ToList();

            if (windows == null || windows.Count == 0)
            {
                return main;
            }

            var rootWindows = windows
                .Where(w => w.Owner == null)
                .ToList();

            var activeRoot = rootWindows.FirstOrDefault(w => w.IsActive);
            if (activeRoot != null)
            {
                return activeRoot;
            }

            var largestRoot = rootWindows
                .OrderByDescending(w => Math.Max(0, w.ActualWidth) * Math.Max(0, w.ActualHeight))
                .FirstOrDefault();

            if (largestRoot != null)
            {
                return largestRoot;
            }

            var active = windows.FirstOrDefault(w => w.IsActive) ?? windows.LastOrDefault();
            if (active == null)
            {
                return Application.Current?.MainWindow;
            }

            while (active.Owner != null)
            {
                active = active.Owner;
            }

            return active;
        }

        private static void RepositionToasts(Window? owner)
        {
            List<ToastNotificationWindow> snapshot;
            lock (Locker)
            {
                snapshot = ActiveToasts.ToList();
            }

            if (snapshot.Count == 0)
            {
                return;
            }

            var work = SystemParameters.WorkArea;
            Rect hostRect;

            if (owner != null && owner.WindowState != WindowState.Minimized)
            {
                hostRect = new Rect(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight);

                var left = Math.Max(hostRect.Left, work.Left);
                var top = Math.Max(hostRect.Top, work.Top);
                var right = Math.Min(hostRect.Right, work.Right);
                var bottom = Math.Min(hostRect.Bottom, work.Bottom);

                hostRect = new Rect(new Point(left, top), new Point(right, bottom));
            }
            else
            {
                hostRect = work;
            }

            var y = hostRect.Bottom - Margin;
            foreach (var toast in snapshot.AsEnumerable().Reverse())
            {
                var width = toast.ActualWidth > 0 ? toast.ActualWidth : toast.Width;
                var height = toast.ActualHeight > 0 ? toast.ActualHeight : toast.Height;

                y -= height;
                toast.Left = Math.Max(hostRect.Left + Margin, hostRect.Right - width - Margin);
                toast.Top = Math.Max(hostRect.Top + Margin, y);
                y -= ToastSpacing;
            }
        }

        private static string ShortenMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            var compact = message.Replace("\r\n", " ").Replace('\n', ' ').Trim();
            const int maxLength = 220;
            return compact.Length <= maxLength ? compact : compact[..maxLength] + "…";
        }
    }
}
