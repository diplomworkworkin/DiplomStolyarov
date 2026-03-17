using System.Windows.Controls;
using SchoolScheduleApp.ViewModels;

namespace SchoolScheduleApp.Views.Pages
{
    public partial class ClassSchedulePage : Page
    {
        public ClassSchedulePage()
            : this(null, null)
        {
        }

        public ClassSchedulePage(int? fixedClassId, string? fixedClassName)
        {
            InitializeComponent();
            DataContext = new ClassScheduleViewModel(fixedClassId, fixedClassName);
        }
    }
}
