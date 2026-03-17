using SchoolSchedule.Entites;
using SchoolScheduleApp.Core;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SchoolSchedule.Tests
{
    public class ScheduleGeneratorTests
    {
        [Fact]
        public void TeacherScheduleQuery_Filters_ByTeacherAndClass()
        {
            var lessons = new List<Lesson>
            {
                new() { Id = 1, TeacherId = 1, SubjectId = 1, AcademicClassId = 1, DayOfWeek = 1, LessonIndex = 1 },
                new() { Id = 2, TeacherId = 1, SubjectId = 1, AcademicClassId = 2, DayOfWeek = 2, LessonIndex = 2 },
                new() { Id = 3, TeacherId = 2, SubjectId = 2, AcademicClassId = 1, DayOfWeek = 3, LessonIndex = 3 }
            };

            var rows = ScheduleQueries.BuildTeacherSchedule(
                lessons,
                teacherId: 1,
                dayOfWeek: null,
                academicClassId: 1,
                subjectId: null);

            Assert.Single(rows);
            Assert.Equal(1, rows[0].AcademicClassId);
            Assert.Equal(1, rows[0].TeacherId);
        }

        [Fact]
        public void ClassScheduleQuery_Filters_ByClassAndDay()
        {
            var lessons = new List<Lesson>
            {
                new() { Id = 1, TeacherId = 1, SubjectId = 1, AcademicClassId = 1, DayOfWeek = 1, LessonIndex = 1 },
                new() { Id = 2, TeacherId = 2, SubjectId = 2, AcademicClassId = 1, DayOfWeek = 2, LessonIndex = 2 },
                new() { Id = 3, TeacherId = 3, SubjectId = 3, AcademicClassId = 2, DayOfWeek = 1, LessonIndex = 3 }
            };

            var rows = ScheduleQueries.BuildClassSchedule(
                lessons,
                academicClassId: 1,
                dayOfWeek: 2);

            Assert.Single(rows);
            Assert.Equal(1, rows[0].AcademicClassId);
            Assert.Equal(2, rows[0].DayOfWeek);
        }
    }
}
