using SchoolScheduleApp.Core;

namespace SchoolScheduleApp.ViewModels
{
    public class StudentShellViewModel : ViewModelBase
    {
        private string _displayName = "Ученик";
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }

        public StudentShellViewModel()
        {
            var user = UserSession.CurrentUser;
            if (user != null)
                DisplayName = user.FullName;
        }
    }
}
