using SchoolSchedule.Entites;
using System.Collections.Generic;
using System.Linq;

namespace SchoolScheduleApp.Core
{
    public static class ScheduleQueries
    {
        public static List<Lesson> BuildTeacherSchedule(
            IEnumerable<Lesson> lessons,
            int teacherId,
            int? dayOfWeek,
            int? academicClassId,
            int? subjectId)
        {
            var query = lessons.Where(x => x.TeacherId == teacherId);

            if (dayOfWeek.HasValue && dayOfWeek.Value > 0)
            {
                query = query.Where(x => x.DayOfWeek == dayOfWeek.Value);
            }

            if (academicClassId.HasValue && academicClassId.Value > 0)
            {
                query = query.Where(x => x.AcademicClassId == academicClassId.Value);
            }

            if (subjectId.HasValue && subjectId.Value > 0)
            {
                query = query.Where(x => x.SubjectId == subjectId.Value);
            }

            return query.OrderBy(x => x.DayOfWeek).ThenBy(x => x.LessonIndex).ToList();
        }

        public static List<Lesson> BuildClassSchedule(
            IEnumerable<Lesson> lessons,
            int academicClassId,
            int? dayOfWeek)
        {
            var query = lessons.Where(x => x.AcademicClassId == academicClassId);

            if (dayOfWeek.HasValue && dayOfWeek.Value > 0)
            {
                query = query.Where(x => x.DayOfWeek == dayOfWeek.Value);
            }

            return query.OrderBy(x => x.DayOfWeek).ThenBy(x => x.LessonIndex).ToList();
        }
    }
}
