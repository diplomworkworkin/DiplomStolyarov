using System.Windows;

namespace SchoolScheduleApp.Views.Windows
{
    public enum ClearDialogsMode
    {
        Cancel = 0,
        DeleteForMe = 1,
        DeleteForEveryone = 2
    }

    public partial class ClearDialogsWindow : Window
    {
        public ClearDialogsMode Result { get; private set; } = ClearDialogsMode.Cancel;

        public ClearDialogsWindow(bool allowDeleteForEveryone)
        {
            InitializeComponent();
            Title = "\u041E\u0447\u0438\u0441\u0442\u043A\u0430 \u0434\u0438\u0430\u043B\u043E\u0433\u043E\u0432";
            TitleText.Text = "\u041E\u0447\u0438\u0441\u0442\u0438\u0442\u044C \u0434\u0438\u0430\u043B\u043E\u0433\u0438";
            DescriptionText.Text = "\u0412\u044B\u0431\u0435\u0440\u0438\u0442\u0435 \u0434\u0435\u0439\u0441\u0442\u0432\u0438\u0435 \u0434\u043B\u044F \u0432\u044B\u0431\u0440\u0430\u043D\u043D\u044B\u0445 \u0434\u0438\u0430\u043B\u043E\u0433\u043E\u0432:";
            CancelButton.Content = "\u041E\u0442\u043C\u0435\u043D\u0430";
            DeleteForMeButton.Content = "\u0423\u0434\u0430\u043B\u0438\u0442\u044C \u0443 \u043C\u0435\u043D\u044F";
            DeleteForAllButton.Content = "\u0423\u0434\u0430\u043B\u0438\u0442\u044C \u0443 \u0432\u0441\u0435\u0445";
            DeleteForAllButton.IsEnabled = allowDeleteForEveryone;
            DeleteForAllButton.Visibility = allowDeleteForEveryone ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = ClearDialogsMode.Cancel;
            DialogResult = false;
        }

        private void DeleteForMe_Click(object sender, RoutedEventArgs e)
        {
            Result = ClearDialogsMode.DeleteForMe;
            DialogResult = true;
        }

        private void DeleteForAll_Click(object sender, RoutedEventArgs e)
        {
            Result = ClearDialogsMode.DeleteForEveryone;
            DialogResult = true;
        }
    }
}
