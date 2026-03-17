using System;

namespace SchoolScheduleApp.Core
{
    public static class SchedulePresentationHelper
    {
        public static string DayToText(int day)
        {
            return day switch
            {
                1 => "Понедельник",
                2 => "Вторник",
                3 => "Среда",
                4 => "Четверг",
                5 => "Пятница",
                _ => "—"
            };
        }

        public static string LessonIndexToTimeRange(int lessonIndex)
        {
            if (lessonIndex <= 0)
            {
                return "—";
            }

            var settings = AppSettingsService.Load();
            var lessonDuration = settings.LessonDuration > 0 ? settings.LessonDuration : 45;
            var breakDuration = settings.BreakDuration >= 0 ? settings.BreakDuration : 10;
            var startOfDay = TimeSpan.TryParse(settings.StartTime, out var parsedStart)
                ? parsedStart
                : new TimeSpan(8, 0, 0);

            var offsetMinutes = (lessonIndex - 1) * (lessonDuration + breakDuration);
            var lessonStart = startOfDay.Add(TimeSpan.FromMinutes(offsetMinutes));
            var lessonEnd = lessonStart.Add(TimeSpan.FromMinutes(lessonDuration));

            return $"{lessonStart:hh\\:mm}–{lessonEnd:hh\\:mm}";
        }
    }
}
