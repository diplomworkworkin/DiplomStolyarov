using SchoolSchedule.Entites;
using SchoolScheduleApp.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SchoolScheduleApp.ViewModels
{
    public class ClassScheduleRow
    {
        public string Day { get; set; } = string.Empty;
        public string TimeRange { get; set; } = string.Empty;
        public int LessonIndex { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
    }

    public class ClassScheduleViewModel : ViewModelBase
    {
        public ObservableCollection<FilterOption> DayOptions { get; } = new();
        public ObservableCollection<FilterOption> WeekOptions { get; } = new();
        public ObservableCollection<FilterOption> ClassOptions { get; } = new();
        public ObservableCollection<ClassScheduleRow> ScheduleRows { get; } = new();

        private readonly int? _fixedClassId;
        private readonly string _fixedClassName;
        private readonly IReadOnlyList<FilterOption>? _allowedClasses;
        private bool _isInitializing;

        private FilterOption? _selectedDay;
        public FilterOption? SelectedDay
        {
            get => _selectedDay;
            set
            {
                _selectedDay = value;
                OnPropertyChanged();
                if (!_isInitializing)
                {
                    LoadSchedule();
                }
            }
        }

        private FilterOption? _selectedWeek;
        public FilterOption? SelectedWeek
        {
            get => _selectedWeek;
            set
            {
                _selectedWeek = value;
                OnPropertyChanged();
                if (!_isInitializing)
                {
                    LoadSchedule();
                }
            }
        }

        private FilterOption? _selectedClass;
        public FilterOption? SelectedClass
        {
            get => _selectedClass;
            set
            {
                _selectedClass = value;
                OnPropertyChanged();
                if (!_isInitializing)
                {
                    LoadSchedule();
                }
            }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        private string _weekRangeText = string.Empty;
        public string WeekRangeText
        {
            get => _weekRangeText;
            set { _weekRangeText = value; OnPropertyChanged(); }
        }

        private bool _isClassSelectionEnabled = true;
        public bool IsClassSelectionEnabled
        {
            get => _isClassSelectionEnabled;
            set { _isClassSelectionEnabled = value; OnPropertyChanged(); }
        }

        public ClassScheduleViewModel()
            : this(null, null, null)
        {
        }

        public ClassScheduleViewModel(int? fixedClassId, string? fixedClassName)
            : this(fixedClassId, fixedClassName, null)
        {
        }

        public ClassScheduleViewModel(int? fixedClassId, string? fixedClassName, IReadOnlyList<FilterOption>? allowedClasses)
        {
            _fixedClassId = fixedClassId;
            _fixedClassName = fixedClassName ?? string.Empty;
            _allowedClasses = allowedClasses?
                .Where(x => x.Id > 0)
                .DistinctBy(x => x.Id)
                .OrderBy(x => x.Name)
                .Select(x => new FilterOption { Id = x.Id, Name = x.Name })
                .ToList();

            IsClassSelectionEnabled = !_fixedClassId.HasValue;

            _isInitializing = true;
            BuildDefaultFilters();
            LoadOptions();
            _isInitializing = false;
            LoadSchedule();
        }

        private string SelectedWeekStartDate => AcademicWeekHelper.GetWeekStartKey(SelectedWeek?.Id ?? 0);

        private void UpdateWeekRange()
        {
            WeekRangeText = AcademicWeekHelper.GetWeekText(SelectedWeek?.Id ?? 0);
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
            UpdateWeekRange();
        }

        private void LoadOptions()
        {
            try
            {
                List<FilterOption> classes;
                if (_allowedClasses != null && _allowedClasses.Count > 0)
                {
                    classes = _allowedClasses
                        .Select(x => new FilterOption { Id = x.Id, Name = x.Name })
                        .ToList();
                }
                else
                {
                    classes = SchoolApiClient.GetAcademicClasses()
                        .OrderBy(x => x.Name)
                        .Select(x => new FilterOption { Id = x.Id, Name = x.Name })
                        .ToList();
                }

                ClassOptions.Clear();
                if (!_fixedClassId.HasValue && (_allowedClasses == null || _allowedClasses.Count == 0))
                {
                    ClassOptions.Add(new FilterOption { Id = 0, Name = "Все классы" });
                }

                foreach (var cls in classes.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
                {
                    ClassOptions.Add(cls);
                }

                if (_fixedClassId.HasValue)
                {
                    SelectedClass = ClassOptions.FirstOrDefault(x => x.Id == _fixedClassId.Value);
                    if (SelectedClass == null)
                    {
                        var fallbackName = string.IsNullOrWhiteSpace(_fixedClassName)
                            ? $"Класс {_fixedClassId.Value}"
                            : _fixedClassName;

                        var fixedClassOption = new FilterOption
                        {
                            Id = _fixedClassId.Value,
                            Name = fallbackName
                        };

                        ClassOptions.Add(fixedClassOption);
                        SelectedClass = fixedClassOption;
                    }

                    IsClassSelectionEnabled = false;
                    return;
                }

                if (_allowedClasses != null && _allowedClasses.Count > 0)
                {
                    IsClassSelectionEnabled = _allowedClasses.Count > 1;
                    SelectedClass = ClassOptions.FirstOrDefault();
                    return;
                }

                var user = UserSession.CurrentUser;
                if (user?.Role == UserRole.Student && user.AcademicClassId.HasValue)
                {
                    SelectedClass = ClassOptions.FirstOrDefault(x => x.Id == user.AcademicClassId.Value)
                        ?? ClassOptions.FirstOrDefault();
                }
                else
                {
                    SelectedClass = ClassOptions.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Не удалось загрузить список классов: " + ex.Message;
            }
        }

        private void LoadSchedule()
        {
            UpdateWeekRange();
            ScheduleRows.Clear();

            if (SelectedClass == null || SelectedClass.Id == 0)
            {
                ErrorMessage = "Выберите класс.";
                return;
            }

            try
            {
                ErrorMessage = string.Empty;
                var lessons = SchoolApiClient.GetLessons(
                    classId: SelectedClass.Id,
                    dayOfWeek: SelectedDay?.Id > 0 ? SelectedDay.Id : null,
                    weekStartDate: SelectedWeekStartDate);

                var sorted = ScheduleQueries.BuildClassSchedule(
                    lessons,
                    SelectedClass.Id,
                    SelectedDay?.Id);

                foreach (var lesson in sorted)
                {
                    ScheduleRows.Add(new ClassScheduleRow
                    {
                        Day = SchedulePresentationHelper.DayToText(lesson.DayOfWeek),
                        TimeRange = SchedulePresentationHelper.LessonIndexToTimeRange(lesson.LessonIndex),
                        LessonIndex = lesson.LessonIndex,
                        Subject = lesson.Subject?.Name ?? string.Empty,
                        Teacher = lesson.Teacher?.FullName ?? string.Empty,
                        Classroom = lesson.Classroom?.Number ?? "-"
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Не удалось загрузить расписание: " + ex.Message;
            }
        }
    }
}
