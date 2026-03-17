using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace SchoolScheduleApp.Views.Windows
{
    public partial class ToastNotificationWindow : Window
    {
        private readonly Action? _action;

        public ToastNotificationWindow(
            string title,
            string message,
            bool isError,
            string? actionText = null,
            Action? action = null)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
            _action = action;

            if (!string.IsNullOrWhiteSpace(actionText) && action != null)
            {
                ActionButton.Content = actionText;
                ActionButton.Visibility = Visibility.Visible;
            }

            if (isError)
            {
                RootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 107, 107));
            }
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            var action = _action;
            Close();

            if (action != null)
            {
                Dispatcher.BeginInvoke(action, DispatcherPriority.ContextIdle);
            }
        }
    }
}
