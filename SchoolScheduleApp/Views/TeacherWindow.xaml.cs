using SchoolScheduleApp.Core;
using SchoolScheduleApp.ViewModels;
using SchoolScheduleApp.Views.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace SchoolScheduleApp.Views
{
    public partial class TeacherWindow : Window
    {
        private readonly DispatcherTimer _messagesTimer;
        private readonly List<FilterOption> _curatorClasses = new();

        private const string MyScheduleTitle = "\u041C\u043E\u0451 \u0440\u0430\u0441\u043F\u0438\u0441\u0430\u043D\u0438\u0435";
        private const string ClassScheduleBaseTitle = "\u0420\u0430\u0441\u043F\u0438\u0441\u0430\u043D\u0438\u0435 \u043A\u043B\u0430\u0441\u0441\u0430";
        private const string MessagesTitle = "\u0421\u043E\u043E\u0431\u0449\u0435\u043D\u0438\u044F";
        private const string MessagesMenuPrefix = "\u2709\uFE0F   ";
        private const string SettingsTitle = "\u041D\u0430\u0441\u0442\u0440\u043E\u0439\u043A\u0438";

        public TeacherWindow()
        {
            InitializeComponent();
            ConfigureCuratorClassNavigation();

            MainFrame.Navigated += MainFrame_Navigated;
            NavigateTo(new TeacherSchedulePage(), MyScheduleTitle);

            MouseDown += (_, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            };

            Activated += (_, _) => UpdateMessagesBadge();
            _messagesTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _messagesTimer.Tick += (_, _) => UpdateMessagesBadge();
            _messagesTimer.Start();
            Closed += (_, _) => _messagesTimer.Stop();

            UpdateMessagesBadge();

            StateChanged += (_, _) => ApplyMaximizedBounds();
            Loaded += (_, _) => ApplyMaximizedBounds();
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is not Page page)
            {
                return;
            }

            page.Opacity = 0;
            var anim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            page.BeginAnimation(OpacityProperty, anim);
        }

        private void NavigateTo(Page page, string title)
        {
            MainFrame.Navigate(page);
            PageTitle.Text = title;
            UpdateMessagesBadge();
        }

        private void BtnSchedule_Click(object sender, RoutedEventArgs e)
            => NavigateTo(new TeacherSchedulePage(), MyScheduleTitle);

        private void BtnCuratorClassSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (_curatorClasses.Count == 0)
            {
                ToastService.Show(
                    "\u0412\u044B \u043D\u0435 \u043D\u0430\u0437\u043D\u0430\u0447\u0435\u043D\u044B \u043A\u043B\u0430\u0441\u0441\u043D\u044B\u043C \u0440\u0443\u043A\u043E\u0432\u043E\u0434\u0438\u0442\u0435\u043B\u0435\u043C.",
                    ClassScheduleBaseTitle,
                    true);
                return;
            }

            var title = ClassScheduleBaseTitle;

            NavigateTo(new ClassSchedulePage(null, null, _curatorClasses), title);
        }

        private void BtnMessages_Click(object sender, RoutedEventArgs e)
            => NavigateTo(new MessagesPage(), MessagesTitle);

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
            => NavigateTo(new SettingsPage(), SettingsTitle);

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Application.Current.Shutdown();

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void ApplyMaximizedBounds()
        {
            if (WindowState == WindowState.Maximized)
            {
                var workArea = SystemParameters.WorkArea;
                MaxWidth = workArea.Width;
                MaxHeight = workArea.Height;
                Left = workArea.Left;
                Top = workArea.Top;
            }
            else
            {
                MaxWidth = double.PositiveInfinity;
                MaxHeight = double.PositiveInfinity;
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            UserSession.Clear();
            var loginWindow = new MainWindow();
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();
            Close();
        }

        private void UpdateMessagesBadge()
        {
            var teacherId = UserSession.CurrentUser?.TeacherId;
            var unread = teacherId.HasValue
                ? MessageRequestService.GetUnreadTeacherCount(teacherId.Value)
                : 0;

            BtnMessages.Content = unread > 0
                ? $"{MessagesMenuPrefix}{MessagesTitle} ({unread})"
                : $"{MessagesMenuPrefix}{MessagesTitle}";
        }

        private void ConfigureCuratorClassNavigation()
        {
            BtnCuratorClassSchedule.Visibility = Visibility.Collapsed;
            _curatorClasses.Clear();

            var teacherId = UserSession.CurrentUser?.TeacherId;
            if (!teacherId.HasValue)
            {
                return;
            }

            try
            {
                var curatorClasses = SchoolApiClient.GetAcademicClasses()
                    .OrderBy(x => x.Name)
                    .Where(x => x.CuratorTeacherId == teacherId.Value)
                    .Take(2)
                    .ToList();

                if (curatorClasses.Count == 0)
                {
                    return;
                }

                foreach (var item in curatorClasses)
                {
                    _curatorClasses.Add(new FilterOption
                    {
                        Id = item.Id,
                        Name = item.Name ?? string.Empty
                    });
                }

                BtnCuratorClassSchedule.Content = "\U0001F3EB   \u0420\u0430\u0441\u043F\u0438\u0441\u0430\u043D\u0438\u0435 \u043A\u043B\u0430\u0441\u0441\u0430";
                BtnCuratorClassSchedule.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to configure curator class schedule menu.", ex);
            }
        }
    }
}
