using SchoolScheduleApp.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace SchoolScheduleApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            MouseDown += (_, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            };
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = UserPasswordBox.Password.Length > 0
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LoginViewModel vm || string.IsNullOrEmpty(vm.InitialPassword))
            {
                return;
            }

            UserPasswordBox.Password = vm.InitialPassword;
            PasswordPlaceholder.Visibility = Visibility.Collapsed;
        }
    }
}
