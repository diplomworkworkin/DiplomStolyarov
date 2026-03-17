using SchoolScheduleApp.Core;
using SchoolScheduleApp.ViewModels;
using SchoolScheduleApp.Views.Windows;
using System.Windows;
using System.Windows.Controls;

namespace SchoolScheduleApp.Views.Pages
{
    public partial class SchedulePage : Page
    {
        public SchedulePage()
        {
            InitializeComponent();
            DataContext = new ScheduleViewModel();
        }

        private void BtnManualEdit_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ScheduleViewModel vm)
            {
                return;
            }

            if (vm.SelectedClassId <= 0)
            {
                ToastService.Show("Сначала выберите класс.", "Редактирование");
                return;
            }

            var wnd = new ManualScheduleEditWindow(vm.SelectedClassId, vm.SelectedDay, vm.SelectedWeekStartDate)
            {
                Owner = Window.GetWindow(this)
            };

            if (wnd.ShowDialog() == true)
            {
                vm.RefreshData();
            }
        }
    }
}
