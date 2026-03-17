using SchoolScheduleApp.Core;

namespace SchoolScheduleApp.ViewModels
{
    public class TeacherShellViewModel : ViewModelBase
    {
        private string _displayName = "Учитель";
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }

        public TeacherShellViewModel()
        {
            var user = UserSession.CurrentUser;
            if (user != null)
                DisplayName = user.FullName;
        }
    }
}
