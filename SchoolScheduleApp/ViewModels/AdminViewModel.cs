using SchoolScheduleApp.Core;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SchoolScheduleApp.ViewModels
{
    public class AdminViewModel : ViewModelBase
    {
        private int _teachersCount;
        public int TeachersCount
        {
            get => _teachersCount;
            set { _teachersCount = value; OnPropertyChanged(); }
        }

        private int _studentsCount;
        public int StudentsCount
        {
            get => _studentsCount;
            set { _studentsCount = value; OnPropertyChanged(); }
        }

        private string _scheduleStatus = string.Empty;
        public string ScheduleStatus
        {
            get => _scheduleStatus;
            set { _scheduleStatus = value; OnPropertyChanged(); }
        }

        private Brush _scheduleStatusBrush = Brushes.Red;
        public Brush ScheduleStatusBrush
        {
            get => _scheduleStatusBrush;
            set { _scheduleStatusBrush = value; OnPropertyChanged(); }
        }

        private PointCollection _roomLoadLine = new();
        public PointCollection RoomLoadLine
        {
            get => _roomLoadLine;
            set { _roomLoadLine = value; OnPropertyChanged(); }
        }

        private PointCollection _roomLoadArea = new();
        public PointCollection RoomLoadArea
        {
            get => _roomLoadArea;
            set { _roomLoadArea = value; OnPropertyChanged(); }
        }

        private string _roomLoadPercentText = "0%";
        public string RoomLoadPercentText
        {
            get => _roomLoadPercentText;
            set { _roomLoadPercentText = value; OnPropertyChanged(); }
        }

        private string _roomLoadRangeText = "Диапазон: 0-0%";
        public string RoomLoadRangeText
        {
            get => _roomLoadRangeText;
            set { _roomLoadRangeText = value; OnPropertyChanged(); }
        }

        private string _roomLoadYAxisTopText = "100%";
        public string RoomLoadYAxisTopText
        {
            get => _roomLoadYAxisTopText;
            set { _roomLoadYAxisTopText = value; OnPropertyChanged(); }
        }

        private string _roomLoadYAxisMiddleText = "50%";
        public string RoomLoadYAxisMiddleText
        {
            get => _roomLoadYAxisMiddleText;
            set { _roomLoadYAxisMiddleText = value; OnPropertyChanged(); }
        }

        private string _roomLoadYAxisBottomText = "0%";
        public string RoomLoadYAxisBottomText
        {
            get => _roomLoadYAxisBottomText;
            set { _roomLoadYAxisBottomText = value; OnPropertyChanged(); }
        }

        public RelayCommand RefreshRoomLoadCommand { get; }

        public AdminViewModel()
        {
            RefreshRoomLoadCommand = new RelayCommand(_ =>
            {
                LoadDashboardData();
                LoadRoomLoadChart();
            });

            ScheduleGenerator.ScheduleChanged += OnScheduleChanged;

            LoadDashboardData();
            LoadRoomLoadChart();
        }

        private void OnScheduleChanged()
        {
            LoadDashboardData();
            LoadRoomLoadChart();
        }

        private void LoadDashboardData()
        {
            try
            {
                var teachers = SchoolApiClient.GetTeachers();
                var classes = SchoolApiClient.GetAcademicClasses();
                var lessons = SchoolApiClient.GetLessons();

                TeachersCount = teachers.Count;
                StudentsCount = classes.Count;

                if (lessons.Any())
                {
                    ScheduleStatus = "Готово";
                    ScheduleStatusBrush = Brushes.LimeGreen;
                }
                else
                {
                    ScheduleStatus = "Не готово";
                    ScheduleStatusBrush = Brushes.IndianRed;
                }
            }
            catch (Exception ex)
            {
                ScheduleStatus = "Ошибка API";
                ScheduleStatusBrush = Brushes.IndianRed;
                ToastService.Show("Не удалось загрузить данные панели: " + ex.Message, "Ошибка", true);
            }
        }

        private void LoadRoomLoadChart()
        {
            const double xStart = 70;
            const double xEnd = 780;
            const double yTop = 20;
            const double yBottom = 200;
            const int maxSlotsPerDay = 12;

            try
            {
                var roomsCount = SchoolApiClient.GetClassrooms().Count;
                if (roomsCount == 0)
                {
                    RoomLoadPercentText = "0%";
                    RoomLoadRangeText = "Диапазон: 0-0%";
                    RoomLoadYAxisTopText = "100%";
                    RoomLoadYAxisMiddleText = "50%";
                    RoomLoadYAxisBottomText = "0%";
                    RoomLoadLine = new PointCollection();
                    RoomLoadArea = new PointCollection();
                    return;
                }

                var lessonsByDay = SchoolApiClient.GetLessons()
                    .Where(x => x.ClassroomId != null && x.DayOfWeek >= 1 && x.DayOfWeek <= 5)
                    .GroupBy(x => x.DayOfWeek)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(x => $"{x.ClassroomId}-{x.LessonIndex}").Distinct().Count());

                var percents = new double[5];
                for (var day = 1; day <= 5; day++)
                {
                    var busySlots = lessonsByDay.TryGetValue(day, out var value) ? value : 0;
                    var totalSlots = roomsCount * maxSlotsPerDay;
                    percents[day - 1] = totalSlots > 0 ? (busySlots / (double)totalSlots) * 100.0 : 0;
                }

                var average = percents.Average();
                var min = percents.Min();
                var max = percents.Max();

                RoomLoadPercentText = $"{Math.Round(average, 1)}%";
                RoomLoadRangeText = $"Диапазон: {Math.Round(min, 1)}-{Math.Round(max, 1)}%";

                var scale = BuildAdaptiveScale(min, max);
                RoomLoadYAxisTopText = $"{scale.Max:0.#}%";
                RoomLoadYAxisMiddleText = $"{((scale.Max + scale.Min) / 2.0):0.#}%";
                RoomLoadYAxisBottomText = $"{scale.Min:0.#}%";

                var line = new PointCollection();
                var area = new PointCollection { new Point(xStart, yBottom) };
                var step = (xEnd - xStart) / 4.0;

                for (var i = 0; i < 5; i++)
                {
                    var x = xStart + (step * i);
                    var normalized = (percents[i] - scale.Min) / (scale.Max - scale.Min);
                    normalized = Math.Clamp(normalized, 0, 1);

                    var y = yBottom - normalized * (yBottom - yTop);
                    line.Add(new Point(x, y));
                    area.Add(new Point(x, y));
                }

                area.Add(new Point(xEnd, yBottom));
                RoomLoadLine = line;
                RoomLoadArea = area;
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось загрузить график занятости: " + ex.Message, "Ошибка", true);
            }
        }

        private static (double Min, double Max) BuildAdaptiveScale(double min, double max)
        {
            if (Math.Abs(max - min) < 0.001)
            {
                var center = min;
                return (Math.Max(0, center - 5), Math.Min(100, center + 5));
            }

            var spread = max - min;
            var pad = Math.Max(1.5, spread * 0.25);
            var scaledMin = Math.Max(0, min - pad);
            var scaledMax = Math.Min(100, max + pad);

            if (Math.Abs(scaledMax - scaledMin) < 0.001)
            {
                scaledMax = Math.Min(100, scaledMin + 1);
            }

            return (scaledMin, scaledMax);
        }
    }
}
