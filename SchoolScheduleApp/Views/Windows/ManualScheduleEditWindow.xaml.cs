using SchoolSchedule.Entites;
using SchoolScheduleApp.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SchoolScheduleApp.Views.Windows
{
    public partial class ManualScheduleEditWindow : Window
    {
        public class EditableLessonRow : ViewModelBase
        {
            private int _lessonIndex;
            private int _subjectId;
            private int _teacherId;
            private int? _classroomId;
            private ObservableCollection<Teacher> _availableTeachers = new();

            public int Id { get; set; }

            public int LessonIndex
            {
                get => _lessonIndex;
                set { _lessonIndex = value; OnPropertyChanged(); }
            }

            public int SubjectId
            {
                get => _subjectId;
                set { _subjectId = value; OnPropertyChanged(); }
            }

            public int TeacherId
            {
                get => _teacherId;
                set
                {
                    _teacherId = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TeacherName));
                }
            }

            public int? ClassroomId
            {
                get => _classroomId;
                set { _classroomId = value; OnPropertyChanged(); }
            }

            public ObservableCollection<Teacher> AvailableTeachers
            {
                get => _availableTeachers;
                set
                {
                    _availableTeachers = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TeacherName));
                }
            }

            public string TeacherName => AvailableTeachers.FirstOrDefault(t => t.Id == TeacherId)?.FullName ?? "—";
        }

        private readonly int _classId;
        private readonly int _dayOfWeek;
        private readonly string _weekStartDate;

        public ObservableCollection<EditableLessonRow> Lessons { get; } = new();
        public ObservableCollection<Subject> Subjects { get; } = new();
        public ObservableCollection<Teacher> Teachers { get; } = new();
        public ObservableCollection<Classroom> Classrooms { get; } = new();

        public ManualScheduleEditWindow(int classId, int dayOfWeek, string weekStartDate)
        {
            InitializeComponent();
            _classId = classId;
            _dayOfWeek = dayOfWeek;
            _weekStartDate = weekStartDate;
            DataContext = this;

            LoadDictionaries();
            BindComboColumns();
            LoadLessons();
            RefreshTeacherOptionsForAllRows();
        }

        private void BindComboColumns()
        {
            SubjectColumn.ItemsSource = Subjects;
            ClassroomColumn.ItemsSource = Classrooms;
        }

        private void LoadDictionaries()
        {
            Subjects.Clear();
            foreach (var subject in SchoolApiClient.GetSubjects().OrderBy(x => x.Name))
            {
                Subjects.Add(subject);
            }

            Teachers.Clear();
            foreach (var teacher in SchoolApiClient.GetTeachers().OrderBy(x => x.FullName))
            {
                Teachers.Add(teacher);
            }

            Classrooms.Clear();
            foreach (var classroom in SchoolApiClient.GetClassrooms().OrderBy(x => x.Number))
            {
                Classrooms.Add(classroom);
            }
        }

        private void LoadLessons()
        {
            var lessons = SchoolApiClient.GetLessons(
                    classId: _classId,
                    dayOfWeek: _dayOfWeek,
                    weekStartDate: _weekStartDate)
                .OrderBy(x => x.LessonIndex)
                .ToList();

            Lessons.Clear();
            foreach (var lesson in lessons)
            {
                Lessons.Add(new EditableLessonRow
                {
                    Id = lesson.Id,
                    LessonIndex = lesson.LessonIndex,
                    SubjectId = lesson.SubjectId,
                    TeacherId = lesson.TeacherId,
                    ClassroomId = lesson.ClassroomId
                });
            }
        }

        private void RefreshTeacherOptionsForAllRows()
        {
            foreach (var row in Lessons)
            {
                var filteredTeachers = Teachers
                    .Where(t => !t.SubjectId.HasValue || t.SubjectId.Value == row.SubjectId)
                    .ToList();

                row.AvailableTeachers = new ObservableCollection<Teacher>(filteredTeachers);

                if (filteredTeachers.All(t => t.Id != row.TeacherId))
                {
                    row.TeacherId = filteredTeachers.FirstOrDefault()?.Id ?? 0;
                }
            }
        }

        private void LessonsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                new Action(RefreshTeacherOptionsForAllRows),
                DispatcherPriority.Background);
        }

        private void BtnAddLesson_Click(object sender, RoutedEventArgs e)
        {
            var nextIndex = Lessons.Count == 0 ? 1 : Lessons.Max(x => x.LessonIndex) + 1;
            var defaultSubjectId = Subjects.FirstOrDefault()?.Id ?? 0;
            var defaultTeachers = Teachers
                .Where(t => !t.SubjectId.HasValue || t.SubjectId.Value == defaultSubjectId)
                .ToList();

            Lessons.Add(new EditableLessonRow
            {
                LessonIndex = nextIndex,
                SubjectId = defaultSubjectId,
                TeacherId = defaultTeachers.FirstOrDefault()?.Id ?? 0,
                ClassroomId = Classrooms.FirstOrDefault()?.Id,
                AvailableTeachers = new ObservableCollection<Teacher>(defaultTeachers)
            });
        }

        private void BtnDeleteLesson_Click(object sender, RoutedEventArgs e)
        {
            if (LessonsGrid.SelectedItem is not EditableLessonRow row)
            {
                ToastService.Show("Выберите урок для удаления.", "Удаление");
                return;
            }

            Lessons.Remove(row);
        }

        private string? ValidateRows(
            AcademicClass? currentClass,
            IReadOnlyDictionary<int, Subject> subjectsById,
            IReadOnlyDictionary<int, Teacher> teachersById,
            IReadOnlyDictionary<int, Classroom> classroomsById,
            IReadOnlyCollection<Lesson> dayLessons)
        {
            if (Lessons.Count == 0)
            {
                return "Добавьте хотя бы один урок.";
            }

            if (Lessons.Any(x => x.SubjectId <= 0 || x.TeacherId <= 0 || x.LessonIndex <= 0))
            {
                return "У каждого урока должен быть корректный номер, предмет и учитель.";
            }

            if (Lessons.GroupBy(x => x.LessonIndex).Any(g => g.Count() > 1))
            {
                return "Номер урока должен быть уникальным внутри дня.";
            }

            var shift = currentClass?.Shift ?? 1;
            var minLessonIndex = shift == 2 ? 7 : 1;
            var maxLessonIndex = shift == 2 ? 12 : 6;

            if (Lessons.Any(x => x.LessonIndex < minLessonIndex || x.LessonIndex > maxLessonIndex))
            {
                return shift == 2
                    ? "Для 2-й смены можно ставить только уроки 7..12."
                    : "Для 1-й смены можно ставить только уроки 1..6.";
            }

            foreach (var row in Lessons)
            {
                if (!subjectsById.ContainsKey(row.SubjectId))
                {
                    return $"Урок {row.LessonIndex}: выбранный предмет не найден.";
                }

                if (!teachersById.TryGetValue(row.TeacherId, out var teacher))
                {
                    return $"Урок {row.LessonIndex}: выбранный учитель не найден.";
                }

                if (teacher.SubjectId.HasValue && teacher.SubjectId.Value != row.SubjectId)
                {
                    var teacherSubjectName = subjectsById.ContainsKey(teacher.SubjectId.Value)
                        ? subjectsById[teacher.SubjectId.Value].Name
                        : "(не задан)";
                    var lessonSubjectName = subjectsById[row.SubjectId].Name;
                    return $"Урок {row.LessonIndex}: учитель \"{teacher.FullName}\" ведёт \"{teacherSubjectName}\", нельзя назначить \"{lessonSubjectName}\".";
                }

                if (row.ClassroomId.HasValue && !classroomsById.ContainsKey(row.ClassroomId.Value))
                {
                    return $"Урок {row.LessonIndex}: выбранный кабинет не найден.";
                }
            }

            var editedIds = Lessons.Where(x => x.Id > 0).Select(x => x.Id).ToHashSet();
            var conflicts = new StringBuilder();

            foreach (var row in Lessons)
            {
                var teacherConflictExists = dayLessons.Any(l =>
                    l.Id != row.Id
                    && !editedIds.Contains(l.Id)
                    && l.LessonIndex == row.LessonIndex
                    && l.TeacherId == row.TeacherId);

                if (teacherConflictExists)
                {
                    conflicts.AppendLine($"• Урок {row.LessonIndex}: у учителя уже есть занятие в это время.");
                }

                if (row.ClassroomId.HasValue)
                {
                    var roomConflictExists = dayLessons.Any(l =>
                        l.Id != row.Id
                        && !editedIds.Contains(l.Id)
                        && l.LessonIndex == row.LessonIndex
                        && l.ClassroomId == row.ClassroomId.Value);

                    if (roomConflictExists)
                    {
                        conflicts.AppendLine($"• Урок {row.LessonIndex}: кабинет уже занят в это время.");
                    }
                }
            }

            if (conflicts.Length > 0)
            {
                return "Найдены конфликты:\n" + conflicts;
            }

            return null;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentClass = SchoolApiClient.GetAcademicClassById(_classId);
                var subjectsById = SchoolApiClient.GetSubjects().ToDictionary(x => x.Id, x => x);
                var teachersById = SchoolApiClient.GetTeachers().ToDictionary(x => x.Id, x => x);
                var classroomsById = SchoolApiClient.GetClassrooms().ToDictionary(x => x.Id, x => x);
                var dayLessons = SchoolApiClient.GetLessons(
                    dayOfWeek: _dayOfWeek,
                    weekStartDate: _weekStartDate);

                var validationError = ValidateRows(currentClass, subjectsById, teachersById, classroomsById, dayLessons);
                if (validationError != null)
                {
                    ToastService.Show(validationError, "Проверка", true);
                    return;
                }

                var existing = SchoolApiClient.GetLessons(
                    classId: _classId,
                    dayOfWeek: _dayOfWeek,
                    weekStartDate: _weekStartDate);
                var incomingIds = Lessons.Where(x => x.Id > 0).Select(x => x.Id).ToHashSet();
                var toDelete = existing.Where(x => !incomingIds.Contains(x.Id)).ToList();
                foreach (var lesson in toDelete)
                {
                    SchoolApiClient.DeleteLesson(lesson.Id);
                }

                foreach (var row in Lessons)
                {
                    if (row.Id > 0)
                    {
                        var entity = existing.First(x => x.Id == row.Id);
                        entity.WeekStartDate = _weekStartDate;
                        entity.LessonIndex = row.LessonIndex;
                        entity.SubjectId = row.SubjectId;
                        entity.TeacherId = row.TeacherId;
                        entity.ClassroomId = row.ClassroomId;
                        SchoolApiClient.UpdateLesson(entity);
                    }
                    else
                    {
                        var entity = new Lesson
                        {
                            WeekStartDate = _weekStartDate,
                            AcademicClassId = _classId,
                            DayOfWeek = _dayOfWeek,
                            LessonIndex = row.LessonIndex,
                            SubjectId = row.SubjectId,
                            TeacherId = row.TeacherId,
                            ClassroomId = row.ClassroomId
                        };
                        SchoolApiClient.CreateLesson(entity);
                    }
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось сохранить изменения. " + ex.Message, "Ошибка", true);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
