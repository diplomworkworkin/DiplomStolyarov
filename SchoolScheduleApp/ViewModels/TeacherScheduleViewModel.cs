using SchoolSchedule.Entites;
using SchoolScheduleApp.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SchoolScheduleApp.ViewModels
{
    public class FilterOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class TeacherScheduleRow
    {
        public string Day { get; set; } = "";
        public string TimeRange { get; set; } = "";
        public int LessonIndex { get; set; }
        public string AcademicClass { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Classroom { get; set; } = "";
        public string Type { get; set; } = "-";
    }

    public class TeacherScheduleViewModel : ViewModelBase
    {
        public ObservableCollection<FilterOption> DayOptions { get; } = new();
        public ObservableCollection<FilterOption> WeekOptions { get; } = new();
        public ObservableCollection<FilterOption> ClassOptions { get; } = new();
        public ObservableCollection<FilterOption> SubjectOptions { get; } = new();
        public ObservableCollection<TeacherScheduleRow> ScheduleRows { get; } = new();

        private FilterOption? _selectedDay;
        public FilterOption? SelectedDay
        {
            get => _selectedDay;
            set { _selectedDay = value; OnPropertyChanged(); LoadSchedule(); }
        }

        private FilterOption? _selectedWeek;
        public FilterOption? SelectedWeek
        {
            get => _selectedWeek;
            set
            {
                _selectedWeek = value;
                OnPropertyChanged();
                WeekRangeText = GetSelectedWeekRange();
                LoadSchedule();
            }
        }

        private FilterOption? _selectedClass;
        public FilterOption? SelectedClass
        {
            get => _selectedClass;
            set { _selectedClass = value; OnPropertyChanged(); LoadSchedule(); }
        }

        private FilterOption? _selectedSubject;
        public FilterOption? SelectedSubject
        {
            get => _selectedSubject;
            set { _selectedSubject = value; OnPropertyChanged(); LoadSchedule(); }
        }

        private string _weekRangeText = "";
        public string WeekRangeText
        {
            get => _weekRangeText;
            set { _weekRangeText = value; OnPropertyChanged(); }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public RelayCommand ResetFiltersCommand { get; }

        public TeacherScheduleViewModel()
        {
            ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
            BuildDefaultFilters();
            LoadOptions();
            LoadSchedule();
        }

        private void BuildDefaultFilters()
        {
            DayOptions.Add(new FilterOption { Id = 0, Name = "Все дни" });
            DayOptions.Add(new FilterOption { Id = 1, Name = "Понедельник" });
            DayOptions.Add(new FilterOption { Id = 2, Name = "Вторник" });
            DayOptions.Add(new FilterOption { Id = 3, Name = "Среда" });
            DayOptions.Add(new FilterOption { Id = 4, Name = "Четверг" });
            DayOptions.Add(new FilterOption { Id = 5, Name = "Пятница" });

            WeekOptions.Add(new FilterOption { Id = 0, Name = "Текущая неделя" });
            WeekOptions.Add(new FilterOption { Id = 1, Name = "Следующая неделя" });

            SelectedDay = DayOptions.FirstOrDefault();
            SelectedWeek = WeekOptions.FirstOrDefault();
        }

        private void LoadOptions()
        {
            var user = UserSession.CurrentUser;
            if (user == null || user.Role != UserRole.Teacher || user.TeacherId == null)
            {
                ErrorMessage = "Нет привязки учителя к учетной записи.";
                return;
            }

            try
            {
                var lessons = SchoolApiClient.GetLessons(
                    teacherId: user.TeacherId.Value,
                    weekStartDate: SelectedWeekStartDate);

                var classItems = lessons
                    .Select(x => x.AcademicClass)
                    .Where(x => x != null)
                    .DistinctBy(x => x!.Id)
                    .OrderBy(x => x!.Name)
                    .ToList();

                ClassOptions.Clear();
                ClassOptions.Add(new FilterOption { Id = 0, Name = "Все классы" });
                foreach (var cls in classItems)
                {
                    ClassOptions.Add(new FilterOption { Id = cls!.Id, Name = cls.Name });
                }

                var subjects = lessons
                    .Select(x => x.Subject)
                    .Where(x => x != null)
                    .DistinctBy(x => x!.Id)
                    .OrderBy(x => x!.Name)
                    .ToList();

                SubjectOptions.Clear();
                SubjectOptions.Add(new FilterOption { Id = 0, Name = "Все предметы" });
                foreach (var subj in subjects)
                {
                    SubjectOptions.Add(new FilterOption { Id = subj!.Id, Name = subj.Name });
                }

                SelectedClass = ClassOptions.FirstOrDefault();
                SelectedSubject = SubjectOptions.FirstOrDefault();
                WeekRangeText = GetSelectedWeekRange();
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Не удалось загрузить фильтры расписания: " + ex.Message;
            }
        }

        private void LoadSchedule()
        {
            WeekRangeText = GetSelectedWeekRange();
            ScheduleRows.Clear();

            var user = UserSession.CurrentUser;
            if (user == null || user.Role != UserRole.Teacher || user.TeacherId == null)
            {
                ErrorMessage = "Роль учителя не подтверждена.";
                return;
            }

            try
            {
                ErrorMessage = string.Empty;
                var lessons = SchoolApiClient.GetLessons(
                    teacherId: user.TeacherId.Value,
                    dayOfWeek: SelectedDay?.Id > 0 ? SelectedDay.Id : null,
                    weekStartDate: SelectedWeekStartDate);

                var filtered = ScheduleQueries.BuildTeacherSchedule(
                    lessons,
                    user.TeacherId.Value,
                    SelectedDay?.Id,
                    SelectedClass?.Id,
                    SelectedSubject?.Id);

                foreach (var l in filtered)
                {
                    ScheduleRows.Add(new TeacherScheduleRow
                    {
                        Day = SchedulePresentationHelper.DayToText(l.DayOfWeek),
                        TimeRange = SchedulePresentationHelper.LessonIndexToTimeRange(l.LessonIndex),
                        LessonIndex = l.LessonIndex,
                        AcademicClass = l.AcademicClass?.Name ?? "",
                        Subject = l.Subject?.Name ?? "",
                        Classroom = l.Classroom?.Number ?? "-",
                        Type = string.IsNullOrWhiteSpace(l.Classroom?.Type) ? "-" : l.Classroom!.Type!
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Не удалось загрузить расписание: " + ex.Message;
            }
        }

        private void ResetFilters()
        {
            SelectedDay = DayOptions.FirstOrDefault();
            SelectedWeek = WeekOptions.FirstOrDefault();
            SelectedClass = ClassOptions.FirstOrDefault();
            SelectedSubject = SubjectOptions.FirstOrDefault();
        }

        private string SelectedWeekStartDate => AcademicWeekHelper.GetWeekStartKey(SelectedWeek?.Id ?? 0);

        private string GetSelectedWeekRange()
        {
            var (monday, friday) = AcademicWeekHelper.GetWeekRange(SelectedWeek?.Id ?? 0);
            return $"{monday:dd.MM.yyyy} - {friday:dd.MM.yyyy}";
        }
    }
}
