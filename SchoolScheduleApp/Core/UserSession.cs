using SchoolSchedule.Entites;

namespace SchoolScheduleApp.Core
{
    public static class UserSession
    {
        public static User? CurrentUser { get; private set; }

        public static void SetUser(User? user)
        {
            CurrentUser = user;
        }

        public static void Clear()
        {
            CurrentUser = null;
        }
    }
}
