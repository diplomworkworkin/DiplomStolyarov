using Microsoft.Win32;
using SchoolSchedule.Entites;
using SchoolScheduleApp.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SchoolScheduleApp.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly AppSettings _settings;
        private readonly UserRole _currentRole;

        public SettingsViewModel()
        {
            _settings = AppSettingsService.Load();
            _currentRole = UserSession.CurrentUser?.Role ?? UserRole.Teacher;

            ThemeManager.SetTheme(_settings.IsDarkTheme);

            _isDarkTheme = _settings.IsDarkTheme;
            _schoolName = _settings.SchoolName;
            _lessonDuration = _settings.LessonDuration;
            _breakDuration = _settings.BreakDuration;
            _startTime = _settings.StartTime;
            _apiBaseUrl = _settings.ApiBaseUrl;

            ExportWeekOptions.Add(new FilterOption { Id = 0, Name = "Текущая неделя" });
            ExportWeekOptions.Add(new FilterOption { Id = 1, Name = "Следующая неделя" });
            SelectedExportWeek = ExportWeekOptions.FirstOrDefault();
            InitializeExportScopeOptions();

            SaveCommand = new RelayCommand(_ => ExecuteSave());
            BackupCommand = new RelayCommand(_ => ExecuteApiCheck());
            BackupDatabaseCommand = new RelayCommand(_ => ExecuteDatabaseBackup(), _ => CanManageAcademicSettings);
            ExportCsvCommand = new RelayCommand(_ => ExecuteExportCsv(), _ => CanExportSchedule);
            ExportWordCommand = new RelayCommand(_ => ExecuteExportWord(), _ => CanExportSchedule);
        }

        public bool CanManageAcademicSettings => _currentRole == UserRole.Admin;
        public bool CanExportSchedule => _currentRole == UserRole.Admin || _currentRole == UserRole.Teacher;
        public bool IsTeacherRole => _currentRole == UserRole.Teacher;

        private bool _isDarkTheme;
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (_isDarkTheme == value)
                {
                    return;
                }

                _isDarkTheme = value;
                OnPropertyChanged();
                ThemeManager.SetTheme(_isDarkTheme);
            }
        }

        private string _schoolName = string.Empty;
        public string SchoolName
        {
            get => _schoolName;
            set { _schoolName = value; OnPropertyChanged(); }
        }

        private int _lessonDuration;
        public int LessonDuration
        {
            get => _lessonDuration;
            set { _lessonDuration = value; OnPropertyChanged(); }
        }

        private int _breakDuration;
        public int BreakDuration
        {
            get => _breakDuration;
            set { _breakDuration = value; OnPropertyChanged(); }
        }

        private string _startTime = string.Empty;
        public string StartTime
        {
            get => _startTime;
            set { _startTime = value; OnPropertyChanged(); }
        }

        private string _apiBaseUrl = string.Empty;
        public string ApiBaseUrl
        {
            get => _apiBaseUrl;
            set { _apiBaseUrl = value; OnPropertyChanged(); }
        }

        public RelayCommand SaveCommand { get; }
        public RelayCommand BackupCommand { get; }
        public RelayCommand BackupDatabaseCommand { get; }
        public RelayCommand ExportCsvCommand { get; }
        public RelayCommand ExportWordCommand { get; }

        public ObservableCollection<ExportScopeOption> ExportScopeOptions { get; } = new();
        public ObservableCollection<FilterOption> ExportWeekOptions { get; } = new();

        private ExportScopeOption? _selectedExportScope;
        public ExportScopeOption? SelectedExportScope
        {
            get => _selectedExportScope;
            set { _selectedExportScope = value; OnPropertyChanged(); }
        }

        private FilterOption? _selectedExportWeek;
        public FilterOption? SelectedExportWeek
        {
            get => _selectedExportWeek;
            set { _selectedExportWeek = value; OnPropertyChanged(); }
        }

        private void InitializeExportScopeOptions()
        {
            ExportScopeOptions.Clear();

            if (_currentRole == UserRole.Admin)
            {
                ExportScopeOptions.Add(new ExportScopeOption
                {
                    Id = 0,
                    Name = "Все классы"
                });
                SelectedExportScope = ExportScopeOptions.FirstOrDefault();
                return;
            }

            if (_currentRole != UserRole.Teacher)
            {
                SelectedExportScope = null;
                return;
            }

            ExportScopeOptions.Add(new ExportScopeOption
            {
                Id = 0,
                Name = "Моё расписание"
            });
            var curatorClasses = GetCuratorClasses();
            for (var i = 0; i < curatorClasses.Count; i++)
            {
                ExportScopeOptions.Add(new ExportScopeOption
                {
                    Id = i + 1,
                    Name = $"Расписание класса {curatorClasses[i].Name}",
                    AcademicClassId = curatorClasses[i].Id
                });
            }

            SelectedExportScope = ExportScopeOptions.FirstOrDefault();
        }

        private static List<AcademicClass> GetCuratorClasses()
        {
            var teacherId = UserSession.CurrentUser?.TeacherId;
            if (!teacherId.HasValue)
            {
                return [];
            }

            try
            {
                return SchoolApiClient.GetAcademicClasses()
                    .Where(x => x.CuratorTeacherId == teacherId.Value)
                    .OrderBy(x => x.Name)
                    .Take(2)
                    .ToList();
            }
            catch
            {
                return [];
            }
        }

        private void ExecuteSave()
        {
            if (CanManageAcademicSettings && string.IsNullOrWhiteSpace(SchoolName))
            {
                ToastService.Show("Название учреждения не может быть пустым.", "Ошибка", true);
                return;
            }

            if (CanManageAcademicSettings && (LessonDuration <= 0 || LessonDuration > _settings.MaxLessonDuration))
            {
                ToastService.Show("Некорректная длительность урока.", "Ошибка", true);
                return;
            }

            if (CanManageAcademicSettings && (BreakDuration < 0 || BreakDuration > _settings.MaxBreakDuration))
            {
                ToastService.Show("Длина перемены должна быть от 0 до 60 минут.", "Ошибка", true);
                return;
            }

            if (CanManageAcademicSettings && (string.IsNullOrWhiteSpace(StartTime) || !TimeSpan.TryParse(StartTime, out _)))
            {
                ToastService.Show("Начало первого урока должно быть в формате ЧЧ:ММ.", "Ошибка", true);
                return;
            }

            if (string.IsNullOrWhiteSpace(ApiBaseUrl))
            {
                ToastService.Show("Укажите адрес API (например http://127.0.0.1:8000).", "Ошибка", true);
                return;
            }

            _settings.IsDarkTheme = IsDarkTheme;
            _settings.ApiBaseUrl = ApiBaseUrl.Trim();

            if (CanManageAcademicSettings)
            {
                _settings.SchoolName = SchoolName.Trim();
                _settings.LessonDuration = LessonDuration;
                _settings.BreakDuration = BreakDuration;
                _settings.StartTime = StartTime.Trim();
            }

            AppSettingsService.Save(_settings);
            SchoolApiClient.ReconfigureBaseUrl(_settings.ApiBaseUrl);
            ToastService.Show("Настройки сохранены.", "Система");
        }

        private void ExecuteApiCheck()
        {
            try
            {
                SchoolApiClient.ReconfigureBaseUrl(ApiBaseUrl);
                var ok = SchoolApiClient.TryCheckHealth(out var diagnostics);
                if (ok)
                {
                    ToastService.Show("API доступен.", "Проверка API");
                }
                else
                {
                    ToastService.Show(diagnostics, "Проверка API", true);
                }
            }
            catch (Exception ex)
            {
                ToastService.Show("Ошибка проверки API: " + ex.Message, "Проверка API", true);
            }
        }

        private void ExecuteDatabaseBackup()
        {
            try
            {
                var sourceDbPath = ResolveDatabasePath();
                if (string.IsNullOrWhiteSpace(sourceDbPath) || !File.Exists(sourceDbPath))
                {
                    ToastService.Show("\u041D\u0435 \u0443\u0434\u0430\u043B\u043E\u0441\u044C \u043D\u0430\u0439\u0442\u0438 \u0444\u0430\u0439\u043B \u0431\u0430\u0437\u044B school_schedule.db.", "\u0411\u044D\u043A\u0430\u043F \u0411\u0414", true);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "SQLite DB (*.db)|*.db|All files (*.*)|*.*",
                    FileName = $"school_schedule_backup_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.db",
                    OverwritePrompt = true
                };

                if (saveDialog.ShowDialog() != true)
                {
                    return;
                }

                File.Copy(sourceDbPath, saveDialog.FileName, true);
                ToastService.Show($"\u0411\u044D\u043A\u0430\u043F \u0441\u043E\u0437\u0434\u0430\u043D: {saveDialog.FileName}", "\u0411\u044D\u043A\u0430\u043F \u0411\u0414");
            }
            catch (Exception ex)
            {
                ToastService.Show("\u041E\u0448\u0438\u0431\u043A\u0430 \u0431\u044D\u043A\u0430\u043F\u0430 \u0411\u0414: " + ex.Message, "\u0411\u044D\u043A\u0430\u043F \u0411\u0414", true);
            }
        }

        private static string ResolveDatabasePath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new List<string>
            {
                Path.Combine(baseDir, "school_schedule.db"),
                Path.Combine(baseDir, "SchoolScheduleAPI", "school_schedule.db")
            };

            var directory = new DirectoryInfo(baseDir);
            for (var i = 0; i < 8 && directory is not null; i++)
            {
                candidates.Add(Path.Combine(directory.FullName, "SchoolScheduleAPI", "school_schedule.db"));
                candidates.Add(Path.Combine(directory.FullName, "school_schedule.db"));
                directory = directory.Parent;
            }

            return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
        }

        private void ExecuteExportCsv()
        {
            ExecuteExport("CSV (*.csv)|*.csv", "schedule_export.csv", ExportToCsv);
        }

        private void ExecuteExportWord()
        {
            ExecuteExport("Word (*.doc)|*.doc", "schedule_export.doc", ExportToWordDoc);
        }

        private void ExecuteExport(
            string filter,
            string defaultFileName,
            Action<string, IReadOnlyCollection<LessonExportRow>, string> exportAction)
        {
            if (!CanExportSchedule)
            {
                ToastService.Show("Экспорт доступен только учителю и администратору.", "Доступ", true);
                return;
            }

            List<LessonExportRow> rows;
            string periodText;
            try
            {
                rows = BuildExportRows(out periodText);
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось получить данные расписания: " + ex.Message, "Экспорт", true);
                return;
            }

            if (rows.Count == 0)
            {
                ToastService.Show("Нет данных для экспорта.", "Экспорт", true);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = defaultFileName,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                EnsureCanOverwrite(dialog.FileName);
                exportAction(dialog.FileName, rows, periodText);
                ToastService.Show($"Файл сохранён: {dialog.FileName}", "Экспорт");
            }
            catch (Exception ex)
            {
                ToastService.Show("Ошибка экспорта: " + ex.Message, "Ошибка", true);
            }
        }

        private List<LessonExportRow> BuildExportRows(out string periodText)
        {
            var weekOffset = SelectedExportWeek?.Id ?? 0;
            var weekStartDate = AcademicWeekHelper.GetWeekStartKey(weekOffset);
            var weekRange = AcademicWeekHelper.GetWeekRange(weekOffset);
            periodText = $"{weekRange.Monday:dd.MM.yyyy} - {weekRange.Friday:dd.MM.yyyy}";
            List<Lesson> lessons;

            if (_currentRole == UserRole.Teacher)
            {
                var teacherId = UserSession.CurrentUser?.TeacherId;
                if (!teacherId.HasValue)
                {
                    return [];
                }

                if (SelectedExportScope?.AcademicClassId is int curatorClassId)
                {
                    lessons = SchoolApiClient.GetLessons(
                        classId: curatorClassId,
                        weekStartDate: weekStartDate,
                        limit: 1000);
                }
                else
                {
                    lessons = SchoolApiClient.GetLessons(
                        teacherId: teacherId.Value,
                        weekStartDate: weekStartDate,
                        limit: 1000);
                }
            }
            else
            {
                lessons = SchoolApiClient.GetLessons(
                    weekStartDate: weekStartDate,
                    limit: 5000);
            }

            return lessons
                .OrderBy(x => x.AcademicClass?.Name)
                .ThenBy(x => x.DayOfWeek)
                .ThenBy(x => x.LessonIndex)
                .Select(x => new LessonExportRow
                {
                    Day = x.DayOfWeek,
                    LessonNumber = x.LessonIndex,
                    ClassName = x.AcademicClass?.Name ?? "-",
                    Subject = x.Subject?.Name ?? "-",
                    Teacher = x.Teacher?.FullName ?? "-",
                    Room = x.Classroom?.Number ?? "-"
                })
                .ToList();
        }

        private static void ExportToCsv(
            string filePath,
            IReadOnlyCollection<LessonExportRow> rows,
            string periodText)
        {
            var byClass = BuildClassTables(rows, periodText);
            var sb = new StringBuilder();

            foreach (var classTable in byClass)
            {
                sb.AppendLine($"Класс:;{EscapeCsv(classTable.ClassName)}");
                sb.AppendLine($"Период:;{EscapeCsv(classTable.PeriodText)}");
                sb.AppendLine();

                var dayHeaders = new List<string> { "№" };
                foreach (var day in classTable.Days)
                {
                    dayHeaders.Add(day);
                    dayHeaders.Add("Ауд.");
                }

                sb.AppendLine(string.Join(';', dayHeaders.Select(EscapeCsv)));

                foreach (var row in classTable.Rows)
                {
                    var line = new List<string> { row.LessonNumber.ToString() };
                    foreach (var cell in row.DayCells)
                    {
                        line.Add(cell.SubjectTeacher);
                        line.Add(cell.Room);
                    }

                    sb.AppendLine(string.Join(';', line.Select(EscapeCsv)));
                }

                sb.AppendLine();
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }

        private static void ExportToWordDoc(
            string filePath,
            IReadOnlyCollection<LessonExportRow> rows,
            string periodText)
        {
            var byClass = BuildClassTables(rows, periodText);
            var html = new StringBuilder();
            html.Append("<html><head><meta charset='utf-8'/><style>");
            html.Append("body{font-family:'Times New Roman';font-size:12pt;} ");
            html.Append("h2{margin:0 0 6px 0;} p{margin:0 0 8px 0;} ");
            html.Append("table{border-collapse:collapse;width:100%;margin:10px 0 24px 0;} ");
            html.Append("th,td{border:1px solid #000;padding:4px;vertical-align:top;} ");
            html.Append("th{text-align:center;background:#f0f0f0;} ");
            html.Append(".n{width:35px;text-align:center;} .room{width:55px;text-align:center;}");
            html.Append("</style></head><body>");

            foreach (var classTable in byClass)
            {
                html.Append($"<h2>{EscapeHtml(classTable.ClassName)}</h2>");
                html.Append($"<p><b>{EscapeHtml(classTable.PeriodText)}</b></p>");
                html.Append("<table><thead><tr>");
                html.Append("<th class='n'>№</th>");
                foreach (var day in classTable.Days)
                {
                    html.Append($"<th>{EscapeHtml(day)}</th><th class='room'>Ауд.</th>");
                }

                html.Append("</tr></thead><tbody>");

                foreach (var row in classTable.Rows)
                {
                    html.Append($"<tr><td class='n'>{row.LessonNumber}</td>");
                    foreach (var cell in row.DayCells)
                    {
                        html.Append($"<td>{EscapeHtml(cell.SubjectTeacher)}</td>");
                        html.Append($"<td class='room'>{EscapeHtml(cell.Room)}</td>");
                    }
                    html.Append("</tr>");
                }

                html.Append("</tbody></table>");
            }

            html.Append("</body></html>");
            File.WriteAllText(filePath, html.ToString(), new UTF8Encoding(true));
        }

        private static List<ClassExportTable> BuildClassTables(IReadOnlyCollection<LessonExportRow> rows, string periodText)
        {
            var minDay = rows.Min(x => x.Day);
            var maxDay = rows.Max(x => x.Day);
            var dayRange = Enumerable.Range(Math.Max(1, minDay), Math.Max(1, maxDay - Math.Max(1, minDay) + 1)).ToList();

            return rows
                .GroupBy(x => x.ClassName)
                .OrderBy(group => group.Key)
                .Select(group =>
                {
                    var maxLesson = Math.Max(1, group.Max(x => x.LessonNumber));
                    var rowsByKey = group.ToDictionary(x => $"{x.Day}-{x.LessonNumber}", x => x);
                    var tableRows = new List<ClassExportRow>();

                    for (var lesson = 1; lesson <= maxLesson; lesson++)
                    {
                        var dayCells = new List<ClassExportCell>();
                        foreach (var day in dayRange)
                        {
                            var key = $"{day}-{lesson}";
                            if (rowsByKey.TryGetValue(key, out var lessonRow))
                            {
                                dayCells.Add(new ClassExportCell
                                {
                                    SubjectTeacher = $"{lessonRow.Subject}\n{lessonRow.Teacher}",
                                    Room = lessonRow.Room
                                });
                            }
                            else
                            {
                                dayCells.Add(new ClassExportCell { SubjectTeacher = string.Empty, Room = string.Empty });
                            }
                        }

                        tableRows.Add(new ClassExportRow
                        {
                            LessonNumber = lesson,
                            DayCells = dayCells
                        });
                    }

                    return new ClassExportTable
                    {
                        ClassName = group.Key,
                        Days = dayRange.Select(DayToText).ToList(),
                        PeriodText = periodText,
                        Rows = tableRows
                    };
                })
                .ToList();
        }

        private static string EscapeCsv(string value)
        {
            var escaped = value.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", " ");
            return $"\"{escaped}\"";
        }

        private static string EscapeHtml(string value)
        {
            var escaped = System.Security.SecurityElement.Escape(value) ?? string.Empty;
            return escaped.Replace("\n", "<br/>");
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
                6 => "Суббота",
                _ => "День"
            };
        }

        private static void EnsureCanOverwrite(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
        }

        public sealed class ExportScopeOption : FilterOption
        {
            public int? AcademicClassId { get; set; }
        }

        private sealed class LessonExportRow
        {
            public int Day { get; set; }
            public int LessonNumber { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string Teacher { get; set; } = string.Empty;
            public string Room { get; set; } = string.Empty;
        }

        private sealed class ClassExportTable
        {
            public string ClassName { get; set; } = string.Empty;
            public List<string> Days { get; set; } = [];
            public string PeriodText { get; set; } = string.Empty;
            public List<ClassExportRow> Rows { get; set; } = [];
        }

        private sealed class ClassExportRow
        {
            public int LessonNumber { get; set; }
            public List<ClassExportCell> DayCells { get; set; } = [];
        }

        private sealed class ClassExportCell
        {
            public string SubjectTeacher { get; set; } = string.Empty;
            public string Room { get; set; } = string.Empty;
        }
    }
}
