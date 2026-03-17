using System;

namespace SchoolScheduleApp.Core
{
    public static class AcademicWeekHelper
    {
        public static (DateTime Monday, DateTime Friday) GetCurrentWeekRange(DateTime? referenceDate = null)
            => GetWeekRange(0, referenceDate);

        public static (DateTime Monday, DateTime Friday) GetWeekRange(int weekOffset, DateTime? referenceDate = null)
        {
            var today = (referenceDate ?? DateTime.Today).Date;
            var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var monday = today.AddDays(-diff).AddDays(weekOffset * 7);
            var friday = monday.AddDays(4);
            return (monday, friday);
        }

        public static DateTime GetWeekStartDate(int weekOffset, DateTime? referenceDate = null)
            => GetWeekRange(weekOffset, referenceDate).Monday.Date;

        public static string GetWeekStartKey(int weekOffset, DateTime? referenceDate = null)
            => GetWeekStartDate(weekOffset, referenceDate).ToString("yyyy-MM-dd");

        public static string GetWeekText(int weekOffset, string prefix = "Неделя: ")
        {
            var (monday, friday) = GetWeekRange(weekOffset);
            return $"{prefix}{monday:dd.MM.yyyy} - {friday:dd.MM.yyyy}";
        }

        public static string GetCurrentWeekText(string prefix = "Неделя: ")
            => GetWeekText(0, prefix);

        public static string GetCurrentWeekPeriodText()
        {
            var (monday, friday) = GetCurrentWeekRange();
            return $"{monday:dd.MM.yyyy} - {friday:dd.MM.yyyy}";
        }
    }
}
