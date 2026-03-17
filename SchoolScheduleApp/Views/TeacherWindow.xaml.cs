using SchoolScheduleApp.Core;
using SchoolScheduleApp.Views.Pages;
using System;
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
        private int? _curatorClassId;
        private string _curatorClassName = string.Empty;

        public TeacherWindow()
        {
            InitializeComponent();
            ConfigureCuratorClassNavigation();

            MainFrame.Navigated += MainFrame_Navigated;
            NavigateTo(new TeacherSchedulePage(), "Моё расписание");

            MouseDown += (_, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    DragMove();
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
            if (e.Content is not Page page) return;

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
            => NavigateTo(new TeacherSchedulePage(), "Моё расписание");

        private void BtnCuratorClassSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (!_curatorClassId.HasValue)
            {
                ToastService.Show("Вы не назначены классным руководителем.", "Расписание класса", true);
                return;
            }

            var title = string.IsNullOrWhiteSpace(_curatorClassName)
                ? "Расписание класса"
                : $"Расписание класса {_curatorClassName}";

            NavigateTo(new ClassSchedulePage(_curatorClassId.Value, _curatorClassName), title);
        }

        private void BtnMessages_Click(object sender, RoutedEventArgs e)
            => NavigateTo(new MessagesPage(), "Сообщения");

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
            => NavigateTo(new SettingsPage(), "Настройки");

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
                ? $"✉️   Сообщения ({unread})"
                : "✉️   Сообщения";
        }

        private void ConfigureCuratorClassNavigation()
        {
            BtnCuratorClassSchedule.Visibility = Visibility.Collapsed;
            _curatorClassId = null;
            _curatorClassName = string.Empty;

            var teacherId = UserSession.CurrentUser?.TeacherId;
            if (!teacherId.HasValue)
            {
                return;
            }

            try
            {
                var curatorClass = SchoolApiClient.GetAcademicClasses()
                    .OrderBy(x => x.Name)
                    .FirstOrDefault(x => x.CuratorTeacherId == teacherId.Value);

                if (curatorClass == null)
                {
                    return;
                }

                _curatorClassId = curatorClass.Id;
                _curatorClassName = curatorClass.Name ?? string.Empty;

                BtnCuratorClassSchedule.Content = string.IsNullOrWhiteSpace(_curatorClassName)
                    ? "🏫   Расписание класса"
                    : $"🏫   Расписание класса {_curatorClassName}";
                BtnCuratorClassSchedule.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to configure curator class schedule menu.", ex);
            }
        }
    }
}
