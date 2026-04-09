using SchoolSchedule.Entites;
using SchoolScheduleApp.Core;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SchoolSchedule.Tests
{
    public class ScheduleQueriesTests
    {
        [Fact]
        public void BuildTeacherSchedule_SortsByDayThenLesson()
        {
            var lessons = new List<Lesson>
            {
                new() { Id = 1, TeacherId = 5, SubjectId = 1, AcademicClassId = 1, DayOfWeek = 3, LessonIndex = 2 },
                new() { Id = 2, TeacherId = 5, SubjectId = 1, AcademicClassId = 1, DayOfWeek = 1, LessonIndex = 4 },
                new() { Id = 3, TeacherId = 5, SubjectId = 1, AcademicClassId = 1, DayOfWeek = 1, LessonIndex = 1 }
            };

            var rows = ScheduleQueries.BuildTeacherSchedule(
                lessons,
                teacherId: 5,
                dayOfWeek: null,
                academicClassId: null,
                subjectId: null);

            Assert.Equal(new[] { 3, 2, 1 }, rows.Select(x => x.Id).ToArray());
        }

        [Fact]
        public void BuildTeacherSchedule_IgnoresNonPositiveOptionalFilters()
        {
            var lessons = new List<Lesson>
            {
                new() { Id = 1, TeacherId = 10, SubjectId = 2, AcademicClassId = 1, DayOfWeek = 2, LessonIndex = 1 },
                new() { Id = 2, TeacherId = 10, SubjectId = 3, AcademicClassId = 2, DayOfWeek = 5, LessonIndex = 2 },
                new() { Id = 3, TeacherId = 11, SubjectId = 2, AcademicClassId = 1, DayOfWeek = 2, LessonIndex = 3 }
            };

            var rows = ScheduleQueries.BuildTeacherSchedule(
                lessons,
                teacherId: 10,
                dayOfWeek: 0,
                academicClassId: -1,
                subjectId: 0);

            Assert.Equal(2, rows.Count);
            Assert.All(rows, x => Assert.Equal(10, x.TeacherId));
        }

        [Fact]
        public void BuildTeacherSchedule_AppliesSubjectFilter()
        {
            var lessons = new List<Lesson>
            {
                new() { Id = 1, TeacherId = 4, SubjectId = 1, AcademicClassId = 1, DayOfWeek = 2, LessonIndex = 1 },
                new() { Id = 2, TeacherId = 4, SubjectId = 2, AcademicClassId = 2, DayOfWeek = 3, LessonIndex = 1 },
                new() { Id = 3, TeacherId = 4, SubjectId = 1, AcademicClassId = 3, DayOfWeek = 4, LessonIndex = 1 }
            };

            var rows = ScheduleQueries.BuildTeacherSchedule(
                lessons,
                teacherId: 4,
                dayOfWeek: null,
                academicClassId: null,
                subjectId: 2);

            Assert.Single(rows);
            Assert.Equal(2, rows[0].SubjectId);
        }

        [Fact]
        public void BuildClassSchedule_SortsByDayThenLesson()
        {
            var lessons = new List<Lesson>
            {
                new() { Id = 1, TeacherId = 1, SubjectId = 1, AcademicClassId = 7, DayOfWeek = 4, LessonIndex = 2 },
                new() { Id = 2, TeacherId = 1, SubjectId = 1, AcademicClassId = 7, DayOfWeek = 2, LessonIndex = 3 },
                new() { Id = 3, TeacherId = 1, SubjectId = 1, AcademicClassId = 7, DayOfWeek = 2, LessonIndex = 1 }
            };

            var rows = ScheduleQueries.BuildClassSchedule(
                lessons,
                academicClassId: 7,
                dayOfWeek: null);

            Assert.Equal(new[] { 3, 2, 1 }, rows.Select(x => x.Id).ToArray());
        }

        [Fact]
        public void BuildClassSchedule_ReturnsEmpty_WhenNoLessonsForClass()
        {
            var lessons = new List<Lesson>
            {
                new() { Id = 1, TeacherId = 1, SubjectId = 1, AcademicClassId = 1, DayOfWeek = 1, LessonIndex = 1 }
            };

            var rows = ScheduleQueries.BuildClassSchedule(
                lessons,
                academicClassId: 99,
                dayOfWeek: null);

            Assert.Empty(rows);
        }
    }
}
