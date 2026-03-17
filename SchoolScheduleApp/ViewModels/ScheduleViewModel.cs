using SchoolSchedule.Entites;
using SchoolScheduleApp.Core;
using SchoolScheduleApp.Views.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace SchoolScheduleApp.ViewModels
{
    public class LessonRow
    {
        public string Day { get; set; } = string.Empty;
        public int DayOfWeek { get; set; }
        public int LessonIndex { get; set; }
        public string TimeRange { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
    }

    public class LessonSlot
    {
        public int DisplayIndex { get; set; }
        public int RealLessonIndex { get; set; }
        public bool HasLesson { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
    }

    public class WeekOptionItem
    {
        public int Offset { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class ScheduleViewModel : ViewModelBase
    {
        public RelayCommand AutoGenerateScheduleCommand { get; }

        public ObservableCollection<AcademicClass> Classes { get; set; } = new();
        public ObservableCollection<LessonSlot> DayGrid { get; set; } = new();
        public ObservableCollection<LessonRow> ScheduleTable { get; set; } = new();
        public ObservableCollection<WeekOptionItem> WeekOptions { get; } = new();

        private readonly List<string> _lastGenerationProblems = new();
        private string _weekRangeText = string.Empty;
        private int _selectedWeekOffset;
        private int _selectedDay = 1;
        private int _selectedDayTabIndex;
        private int _selectedClassId;

        public string WeekRangeText
        {
            get => _weekRangeText;
            set
            {
                _weekRangeText = value;
                OnPropertyChanged();
            }
        }

        public int SelectedWeekOffset
        {
            get => _selectedWeekOffset;
            set
            {
                if (_selectedWeekOffset == value)
                {
                    return;
                }

                _selectedWeekOffset = value;
                OnPropertyChanged();
                RefreshData();
            }
        }

        public string SelectedWeekStartDate => AcademicWeekHelper.GetWeekStartKey(SelectedWeekOffset);

        public int SelectedDay
        {
            get => _selectedDay;
            set
            {
                if (_selectedDay == value)
                {
                    return;
                }

                _selectedDay = value;
                OnPropertyChanged();

                var idx = _selectedDay - 1;
                if (idx < 0) idx = 0;
                if (idx > 4) idx = 4;

                if (_selectedDayTabIndex != idx)
                {
                    _selectedDayTabIndex = idx;
                    OnPropertyChanged(nameof(SelectedDayTabIndex));
                }

                RefreshData();
            }
        }

        public int SelectedDayTabIndex
        {
            get => _selectedDayTabIndex;
            set
            {
                if (_selectedDayTabIndex == value)
                {
                    return;
                }

                _selectedDayTabIndex = value;
                OnPropertyChanged();

                var day = _selectedDayTabIndex + 1;
                if (day < 1) day = 1;
                if (day > 5) day = 5;

                if (_selectedDay != day)
                {
                    _selectedDay = day;
                    OnPropertyChanged(nameof(SelectedDay));
                    RefreshData();
                }
            }
        }

        public int SelectedClassId
        {
            get => _selectedClassId;
            set
            {
                _selectedClassId = value;
                OnPropertyChanged();
                RefreshData();
            }
        }

        public ScheduleViewModel()
        {
            AutoGenerateScheduleCommand = new RelayCommand(_ => ExecuteAutoGenerate());

            WeekOptions.Add(new WeekOptionItem { Offset = 0, Name = "Текущая неделя" });
            WeekOptions.Add(new WeekOptionItem { Offset = 1, Name = "Следующая неделя" });
            SelectedWeekOffset = 0;

            UpdateWeekRange();
            LoadClasses();
        }

        public void RefreshData()
        {
            UpdateWeekRange();
            LoadSchedule();
            LoadDayGrid();
        }

        private void UpdateWeekRange()
        {
            WeekRangeText = AcademicWeekHelper.GetWeekText(SelectedWeekOffset, "Неделя: ");
        }

        private void LoadClasses()
        {
            try
            {
                Classes = new ObservableCollection<AcademicClass>(
                    SchoolApiClient.GetAcademicClasses().OrderBy(c => c.Name).ToList());
                OnPropertyChanged(nameof(Classes));

                if (Classes.Count > 0 && SelectedClassId <= 0)
                {
                    SelectedClassId = Classes[0].Id;
                }
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось загрузить классы: " + ex.Message, "Ошибка", true);
            }
        }

        private void ExecuteAutoGenerate()
        {
            var confirm = MessageBox.Show(
                "Автоматически составить расписание для выбранной недели?\nСтарое расписание этой недели будет удалено.",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            var weekStartDate = SelectedWeekStartDate;
            var result = ScheduleGenerator.Generate(weekStartDate, clearOldSchedule: true);

            _lastGenerationProblems.Clear();
            _lastGenerationProblems.AddRange(result.Problems);

            RefreshData();

            int lessonsForSelectedClass = 0;
            try
            {
                lessonsForSelectedClass = SchoolApiClient.GetLessons(
                    classId: SelectedClassId,
                    weekStartDate: weekStartDate).Count;
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось посчитать уроки после генерации: " + ex.Message, "Ошибка", true);
            }

            var message =
                $"Создано уроков: {result.CreatedLessons}. Для выбранного класса: {lessonsForSelectedClass}.";

            if (lessonsForSelectedClass == 0)
            {
                message += " Нагрузка для выбранного класса не задана - расписание может быть пустым.";
            }

            if (result.Problems.Count > 0)
            {
                message += $" Обнаружено проблем: {result.Problems.Count}.";
                ToastService.Show(
                    message,
                    "Результат",
                    false,
                    "Показать конфликты",
                    ShowLastGenerationProblems);
                return;
            }

            message += " Генерация завершена.";
            ToastService.Show(message, "Результат");
        }

        private void ShowLastGenerationProblems()
        {
            if (_lastGenerationProblems.Count == 0)
            {
                ToastService.Show("Конфликтов нет.", "Результат");
                return;
            }

            var owner = GetDialogHostWindow();

            var dialog = new GenerationProblemsWindow(_lastGenerationProblems);
            if (owner != null && owner != dialog)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialog.ShowDialog();
        }

        private static Window? GetDialogHostWindow()
        {
            var windows = Application.Current?.Windows
                .OfType<Window>()
                .Where(w =>
                    w.IsVisible
                    && w.WindowState != WindowState.Minimized
                    && w is not ToastNotificationWindow
                    && w is not GenerationProblemsWindow)
                .ToList();

            if (windows == null || windows.Count == 0)
            {
                return Application.Current?.MainWindow;
            }

            var active = windows.FirstOrDefault(w => w.IsActive);
            if (active != null)
            {
                return active;
            }

            return windows
                .Where(w => w.Owner == null)
                .OrderByDescending(w => w.ActualWidth * w.ActualHeight)
                .FirstOrDefault() ?? windows[0];
        }

        private void LoadSchedule()
        {
            ScheduleTable.Clear();
            if (SelectedClassId <= 0)
            {
                return;
            }

            try
            {
                var lessons = SchoolApiClient.GetLessons(
                        classId: SelectedClassId,
                        dayOfWeek: SelectedDay,
                        weekStartDate: SelectedWeekStartDate)
                    .OrderBy(x => x.LessonIndex)
                    .ToList();

                foreach (var lesson in lessons)
                {
                    ScheduleTable.Add(new LessonRow
                    {
                        Day = DayToText(lesson.DayOfWeek),
                        DayOfWeek = lesson.DayOfWeek,
                        LessonIndex = lesson.LessonIndex,
                        TimeRange = SchedulePresentationHelper.LessonIndexToTimeRange(lesson.LessonIndex),
                        Subject = lesson.Subject?.Name ?? string.Empty,
                        Teacher = lesson.Teacher?.FullName ?? string.Empty,
                        Classroom = lesson.Classroom?.Number ?? "-"
                    });
                }
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось загрузить расписание: " + ex.Message, "Ошибка", true);
            }
        }

        private void LoadDayGrid()
        {
            DayGrid.Clear();
            if (SelectedClassId <= 0)
            {
                return;
            }

            var selectedClass = Classes.FirstOrDefault(c => c.Id == SelectedClassId);
            int shift = selectedClass?.Shift ?? 1;

            int start = shift == 1 ? 1 : 7;
            int end = shift == 1 ? 6 : 12;

            try
            {
                var lessons = SchoolApiClient.GetLessons(
                    classId: SelectedClassId,
                    dayOfWeek: SelectedDay,
                    weekStartDate: SelectedWeekStartDate);

                int displayIndex = 1;
                for (int idx = start; idx <= end; idx++)
                {
                    var lesson = lessons.FirstOrDefault(x => x.LessonIndex == idx);

                    DayGrid.Add(new LessonSlot
                    {
                        DisplayIndex = displayIndex,
                        RealLessonIndex = idx,
                        HasLesson = lesson != null,
                        Subject = lesson?.Subject?.Name ?? "Нет урока",
                        Teacher = lesson?.Teacher?.FullName ?? string.Empty,
                        Classroom = lesson?.Classroom?.Number ?? "-"
                    });

                    displayIndex++;
                }

                OnPropertyChanged(nameof(DayGrid));
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось загрузить сетку уроков: " + ex.Message, "Ошибка", true);
            }
        }

        private static string DayToText(int day)
        {
            return day switch
            {
                1 => "Понедельник",
                2 => "Вторник",
                3 => "Среда",
                4 => "Четверг",
                5 => "Пятница",
                _ => string.Empty
            };
        }
    }
}
