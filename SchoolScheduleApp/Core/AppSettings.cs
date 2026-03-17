namespace SchoolScheduleApp.Core
{
    public class AppSettings
    {
        public bool IsDarkTheme { get; set; } = true;
        public string SchoolName { get; set; } = "School #11";
        public int LessonDuration { get; set; } = 45;
        public int BreakDuration { get; set; } = 10;
        public string StartTime { get; set; } = "08:00";
        public string ApiBaseUrl { get; set; } = "http://127.0.0.1:8000";
        public bool RememberMe { get; set; }
        public string SavedUsername { get; set; } = string.Empty;
        public string SavedPassword { get; set; } = string.Empty;

        public int MaxLessonDuration => 120;
        public int MaxBreakDuration => 60;
    }
}
