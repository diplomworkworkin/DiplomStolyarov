using SchoolSchedule.Entites;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SchoolScheduleApp.Core
{
    public class ScheduleGenerateResult
    {
        public int CreatedLessons { get; set; }
        public List<string> Problems { get; set; } = new();
    }

    public static class ScheduleGenerator
    {
        private const int DaysPerWeek = 5;
        private const int LessonsPerShift = 6;

        public static event Action? ScheduleChanged;

        public static ScheduleGenerateResult Generate(string weekStartDate, bool clearOldSchedule = true)
        {
            var result = new ScheduleGenerateResult();
            weekStartDate = string.IsNullOrWhiteSpace(weekStartDate)
                ? AcademicWeekHelper.GetWeekStartKey(0)
                : weekStartDate;

            List<Workload> workloads;
            List<Classroom> rooms;
            List<Lesson> existingLessons;
            Dictionary<int, Teacher> teachersById;
            Dictionary<int, Subject> subjectsById;
            Dictionary<int, AcademicClass> classesById;

            try
            {
                workloads = SchoolApiClient.GetWorkloads();
                rooms = SchoolApiClient.GetClassrooms();
                existingLessons = SchoolApiClient.GetLessons(weekStartDate: weekStartDate);
                teachersById = SchoolApiClient.GetTeachers().ToDictionary(x => x.Id);
                subjectsById = SchoolApiClient.GetSubjects().ToDictionary(x => x.Id);
                classesById = SchoolApiClient.GetAcademicClasses().ToDictionary(x => x.Id);
            }
            catch (Exception ex)
            {
                result.Problems.Add("Не удалось получить данные с API: " + ex.Message);
                return result;
            }

            if (workloads.Count == 0)
            {
                result.Problems.Add("Нет нагрузки (Workloads). Сначала заполните учебную нагрузку.");
                return result;
            }

            if (clearOldSchedule)
            {
                foreach (var lesson in existingLessons)
                {
                    try
                    {
                        SchoolApiClient.DeleteLesson(lesson.Id);
                    }
                    catch (Exception ex)
                    {
                        result.Problems.Add($"Не удалось удалить старый урок #{lesson.Id}: {ex.Message}");
                        return result;
                    }
                }

                existingLessons = new List<Lesson>();
            }

            foreach (var workload in workloads)
            {
                if (workload.Teacher == null && teachersById.TryGetValue(workload.TeacherId, out var teacher))
                {
                    workload.Teacher = teacher;
                }

                if (workload.Subject == null && subjectsById.TryGetValue(workload.SubjectId, out var subject))
                {
                    workload.Subject = subject;
                }

                if (workload.AcademicClass == null && classesById.TryGetValue(workload.AcademicClassId, out var academicClass))
                {
                    workload.AcademicClass = academicClass;
                }
            }

            var busyClass = new HashSet<string>();
            var busyTeacher = new HashSet<string>();
            var busyRoom = new HashSet<string>();

            foreach (var lesson in existingLessons)
            {
                busyClass.Add($"{lesson.AcademicClassId}-{lesson.DayOfWeek}-{lesson.LessonIndex}");
                busyTeacher.Add($"{lesson.TeacherId}-{lesson.DayOfWeek}-{lesson.LessonIndex}");
                if (lesson.ClassroomId.HasValue)
                {
                    busyRoom.Add($"{lesson.ClassroomId.Value}-{lesson.DayOfWeek}-{lesson.LessonIndex}");
                }
            }

            var lessonsToCreate = new List<Lesson>();
            var ordered = workloads.OrderByDescending(w => w.HoursPerWeek).ToList();

            foreach (var w in ordered)
            {
                if (w.AcademicClass == null || w.Subject == null || w.Teacher == null)
                {
                    result.Problems.Add($"Workload #{w.Id}: не хватает связей (Class/Subject/Teacher).");
                    continue;
                }

                if (w.HoursPerWeek <= 0 || w.HoursPerWeek > DaysPerWeek * LessonsPerShift)
                {
                    result.Problems.Add($"Workload #{w.Id}: некорректное HoursPerWeek = {w.HoursPerWeek}.");
                    continue;
                }

                if (w.Teacher.SubjectId != null && w.Teacher.SubjectId.Value != w.SubjectId)
                {
                    result.Problems.Add(
                        $"Нагрузка #{w.Id}: предмет \"{w.Subject.Name}\" не соответствует предмету учителя \"{w.Teacher.FullName}\".");
                    continue;
                }

                int shift = w.AcademicClass.Shift;
                if (shift != 1 && shift != 2)
                {
                    result.Problems.Add($"Класс {w.AcademicClass.Name}: некорректная смена (Shift={shift}).");
                    continue;
                }

                int startIndex = shift == 1 ? 1 : 7;
                int endIndex = shift == 1 ? 6 : 12;

                var classSubjectDayUsed = new HashSet<string>();

                for (int i = 0; i < w.HoursPerWeek; i++)
                {
                    bool ok = TryPlace(
                        w,
                        weekStartDate,
                        startIndex,
                        endIndex,
                        rooms,
                        busyClass,
                        busyTeacher,
                        busyRoom,
                        classSubjectDayUsed,
                        out var lesson);

                    if (!ok || lesson == null)
                    {
                        result.Problems.Add(
                            $"Не удалось поставить: класс {w.AcademicClass.Name}, предмет {w.Subject.Name} (час {i + 1}/{w.HoursPerWeek}).");
                        continue;
                    }

                    lessonsToCreate.Add(lesson);
                }
            }

            if (lessonsToCreate.Count > 0)
            {
                var teacherConflicts = lessonsToCreate
                    .GroupBy(l => new { l.TeacherId, l.DayOfWeek, l.LessonIndex })
                    .Where(g => g.Count() > 1)
                    .ToList();

                var roomConflicts = lessonsToCreate
                    .Where(l => l.ClassroomId != null)
                    .GroupBy(l => new { l.ClassroomId, l.DayOfWeek, l.LessonIndex })
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (teacherConflicts.Count > 0 || roomConflicts.Count > 0)
                {
                    foreach (var c in teacherConflicts)
                    {
                        result.Problems.Add(
                            $"Конфликт: учитель #{c.Key.TeacherId} ведёт 2 урока одновременно (день {c.Key.DayOfWeek}, урок {c.Key.LessonIndex}).");
                    }

                    foreach (var c in roomConflicts)
                    {
                        result.Problems.Add(
                            $"Конфликт: кабинет #{c.Key.ClassroomId} занят 2 уроками одновременно (день {c.Key.DayOfWeek}, урок {c.Key.LessonIndex}).");
                    }

                    result.CreatedLessons = 0;
                    return result;
                }

                foreach (var lesson in lessonsToCreate)
                {
                    try
                    {
                        SchoolApiClient.CreateLesson(lesson);
                        result.CreatedLessons++;
                    }
                    catch (Exception ex)
                    {
                        result.Problems.Add(
                            $"Не удалось сохранить урок (класс {lesson.AcademicClassId}, день {lesson.DayOfWeek}, урок {lesson.LessonIndex}): {ex.Message}");
                    }
                }
            }

            NotifyScheduleChanged();
            return result;
        }

        private static void NotifyScheduleChanged()
        {
            ScheduleChanged?.Invoke();
        }

        private static bool TryPlace(
            Workload w,
            string weekStartDate,
            int startIndex,
            int endIndex,
            List<Classroom> rooms,
            HashSet<string> busyClass,
            HashSet<string> busyTeacher,
            HashSet<string> busyRoom,
            HashSet<string> classSubjectDayUsed,
            out Lesson? lesson)
        {
            lesson = null;

            for (int day = 1; day <= DaysPerWeek; day++)
            {
                for (int idx = startIndex; idx <= endIndex; idx++)
                {
                    string classKey = $"{w.AcademicClassId}-{day}-{idx}";
                    string teacherKey = $"{w.TeacherId}-{day}-{idx}";

                    if (busyClass.Contains(classKey))
                    {
                        continue;
                    }

                    if (busyTeacher.Contains(teacherKey))
                    {
                        continue;
                    }

                    string subDayKey = $"{w.AcademicClassId}-{w.SubjectId}-{day}";
                    if (classSubjectDayUsed.Contains(subDayKey))
                    {
                        continue;
                    }

                    int? roomId = PickRoom(w, rooms, busyRoom, day, idx);

                    busyClass.Add(classKey);
                    busyTeacher.Add(teacherKey);
                    classSubjectDayUsed.Add(subDayKey);

                    if (roomId != null)
                    {
                        busyRoom.Add($"{roomId}-{day}-{idx}");
                    }

                    lesson = new Lesson
                    {
                        WeekStartDate = weekStartDate,
                        DayOfWeek = day,
                        LessonIndex = idx,
                        AcademicClassId = w.AcademicClassId,
                        TeacherId = w.TeacherId,
                        SubjectId = w.SubjectId,
                        ClassroomId = roomId
                    };

                    return true;
                }
            }

            return false;
        }

        private static int? PickRoom(Workload w, List<Classroom> rooms, HashSet<string> busyRoom, int day, int idx)
        {
            int students = w.AcademicClass?.StudentCount ?? 0;

            if (w.Teacher?.ClassroomId != null)
            {
                var ownRoom = rooms.FirstOrDefault(r => r.Id == w.Teacher.ClassroomId.Value);
                if (ownRoom != null)
                {
                    var ownRoomKey = $"{ownRoom.Id}-{day}-{idx}";
                    var roomFits = ownRoom.Capacity <= 0 || students <= 0 || ownRoom.Capacity >= students;
                    if (roomFits && !busyRoom.Contains(ownRoomKey))
                    {
                        return ownRoom.Id;
                    }
                }
            }

            foreach (var r in rooms)
            {
                if (r.Capacity > 0 && students > 0 && r.Capacity < students)
                {
                    continue;
                }

                var roomKey = $"{r.Id}-{day}-{idx}";
                if (busyRoom.Contains(roomKey))
                {
                    continue;
                }

                return r.Id;
            }

            return null;
        }
    }
}
