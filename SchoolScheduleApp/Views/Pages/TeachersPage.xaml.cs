using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SchoolScheduleApp.Views.Pages
{
    public partial class TeachersPage : Page
    {
        public TeachersPage()
        {
            InitializeComponent();
        }

        private void NonInteractiveGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) != null
                || FindVisualParent<ScrollBar>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            e.Handled = true;
        }

        private void NonInteractiveGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T typed)
                {
                    return typed;
                }

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }
    }
}
