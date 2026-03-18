using System.Windows.Controls;
using SchoolScheduleApp.ViewModels;
using System.Collections.Generic;

namespace SchoolScheduleApp.Views.Pages
{
    public partial class ClassSchedulePage : Page
    {
        public ClassSchedulePage()
            : this(null, null)
        {
        }

        public ClassSchedulePage(int? fixedClassId, string? fixedClassName)
            : this(fixedClassId, fixedClassName, null)
        {
        }

        public ClassSchedulePage(int? fixedClassId, string? fixedClassName, IReadOnlyList<FilterOption>? allowedClasses)
        {
            InitializeComponent();
            DataContext = new ClassScheduleViewModel(fixedClassId, fixedClassName, allowedClasses);
        }
    }
}
