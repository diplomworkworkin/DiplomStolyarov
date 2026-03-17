using SchoolSchedule.Entites;
using SchoolScheduleApp.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace SchoolScheduleApp.ViewModels
{
    public class WorkloadRow
    {
        public int Id { get; set; }
        public int AcademicClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int HoursPerWeek { get; set; }
        public int YearHours { get; set; }
        public int RemainingHours { get; set; }
    }

    public class WorkloadsViewModel : ViewModelBase
    {
        public ObservableCollection<WorkloadRow> Workloads { get; set; } = new();
        public ObservableCollection<AcademicClass> Classes { get; set; } = new();
        public ObservableCollection<Teacher> Teachers { get; set; } = new();
        public ObservableCollection<Subject> Subjects { get; set; } = new();

        private WorkloadRow? _selectedWorkload;
        public WorkloadRow? SelectedWorkload
        {
            get => _selectedWorkload;
            set
            {
                _selectedWorkload = value;
                OnPropertyChanged();
                FillFormFromSelected();
            }
        }

        private int _formClassId;
        public int FormClassId
        {
            get => _formClassId;
            set { _formClassId = value; OnPropertyChanged(); AutoSetSubjectFromTeacher(); }
        }

        private int _formTeacherId;
        public int FormTeacherId
        {
            get => _formTeacherId;
            set { _formTeacherId = value; OnPropertyChanged(); AutoSetSubjectFromTeacher(); }
        }

        private int _formSubjectId;
        public int FormSubjectId
        {
            get => _formSubjectId;
            set { _formSubjectId = value; OnPropertyChanged(); }
        }

        private int _formHoursPerWeek = 1;
        public int FormHoursPerWeek
        {
            get => _formHoursPerWeek;
            set
            {
                _formHoursPerWeek = value;
                OnPropertyChanged();
                if (_editingId == 0)
                {
                    FormYearHours = Math.Max(1, value * 34);
                }
                else
                {
                    UpdateRemainingHoursText();
                }
            }
        }

        private int _formYearHours = 34;
        public int FormYearHours
        {
            get => _formYearHours;
            set
            {
                _formYearHours = value;
                OnPropertyChanged();
                UpdateRemainingHoursText();
            }
        }

        private string _formRemainingHoursText = "Остаток: 0";
        public string FormRemainingHoursText
        {
            get => _formRemainingHoursText;
            set { _formRemainingHoursText = value; OnPropertyChanged(); }
        }

        private int _usedHoursFromSelection;

        private int _editingId;

        public RelayCommand NewCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand RefreshCommand { get; }

        public WorkloadsViewModel()
        {
            NewCommand = new RelayCommand(_ => ClearForm());
            SaveCommand = new RelayCommand(_ => SaveWorkload());
            DeleteCommand = new RelayCommand(_ => DeleteWorkload(), _ => SelectedWorkload != null);
            RefreshCommand = new RelayCommand(_ => LoadAll());

            LoadAll();
            ClearForm();
        }

        private void LoadAll()
        {
            try
            {
                Classes = new ObservableCollection<AcademicClass>(
                    SchoolApiClient.GetAcademicClasses().OrderBy(x => x.Name).ToList());
                Teachers = new ObservableCollection<Teacher>(
                    SchoolApiClient.GetTeachers().OrderBy(x => x.FullName).ToList());
                Subjects = new ObservableCollection<Subject>(
                    SchoolApiClient.GetSubjects().OrderBy(x => x.Name).ToList());

                OnPropertyChanged(nameof(Classes));
                OnPropertyChanged(nameof(Teachers));
                OnPropertyChanged(nameof(Subjects));

                var list = SchoolApiClient.GetWorkloads()
                    .OrderBy(w => w.AcademicClass?.Name)
                    .ThenBy(w => w.Subject?.Name)
                    .ToList();

                Workloads = new ObservableCollection<WorkloadRow>(
                    list.Select(w => new WorkloadRow
                    {
                        Id = w.Id,
                        AcademicClassId = w.AcademicClassId,
                        ClassName = w.AcademicClass?.Name ?? "",
                        TeacherId = w.TeacherId,
                        TeacherName = w.Teacher?.FullName ?? "",
                        SubjectId = w.SubjectId,
                        SubjectName = w.Subject?.Name ?? "",
                        HoursPerWeek = w.HoursPerWeek,
                        YearHours = w.YearHours,
                        RemainingHours = w.RemainingHours
                    }));

                OnPropertyChanged(nameof(Workloads));
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось загрузить нагрузки: " + ex.Message, "Ошибка", true);
            }
        }

        private void FillFormFromSelected()
        {
            if (SelectedWorkload == null)
            {
                return;
            }

            _editingId = SelectedWorkload.Id;
            FormClassId = SelectedWorkload.AcademicClassId;
            FormTeacherId = SelectedWorkload.TeacherId;
            FormSubjectId = SelectedWorkload.SubjectId;
            FormHoursPerWeek = SelectedWorkload.HoursPerWeek;
            FormYearHours = SelectedWorkload.YearHours;
            _usedHoursFromSelection = Math.Max(0, SelectedWorkload.YearHours - SelectedWorkload.RemainingHours);
            UpdateRemainingHoursText();
        }

        private void ClearForm()
        {
            _editingId = 0;
            SelectedWorkload = null;
            _usedHoursFromSelection = 0;

            FormClassId = Classes.FirstOrDefault()?.Id ?? 0;
            FormTeacherId = Teachers.FirstOrDefault()?.Id ?? 0;
            AutoSetSubjectFromTeacher();
            FormHoursPerWeek = 1;
            FormYearHours = 34;
            UpdateRemainingHoursText();
        }

        private void AutoSetSubjectFromTeacher()
        {
            var t = Teachers.FirstOrDefault(x => x.Id == FormTeacherId);
            if (t != null && t.SubjectId.HasValue)
            {
                FormSubjectId = t.SubjectId.Value;
            }
            else if (FormSubjectId == 0)
            {
                FormSubjectId = Subjects.FirstOrDefault()?.Id ?? 0;
            }
        }

        private void UpdateRemainingHoursText()
        {
            var remaining = Math.Max(0, FormYearHours - _usedHoursFromSelection);
            FormRemainingHoursText = $"Остаток: {remaining}";
        }

        private void SaveWorkload()
        {
            if (FormClassId <= 0)
            {
                ToastService.Show("Выберите класс.", "Ошибка", true);
                return;
            }

            if (FormTeacherId <= 0)
            {
                ToastService.Show("Выберите учителя.", "Ошибка", true);
                return;
            }

            if (FormSubjectId <= 0)
            {
                ToastService.Show("Выберите предмет.", "Ошибка", true);
                return;
            }

            if (FormHoursPerWeek < 1 || FormHoursPerWeek > 10)
            {
                ToastService.Show("Часов в неделю должно быть от 1 до 10.", "Ошибка", true);
                return;
            }

            if (FormYearHours < 1)
            {
                ToastService.Show("Годовых часов должно быть больше 0.", "Ошибка", true);
                return;
            }

            if (FormYearHours < FormHoursPerWeek)
            {
                ToastService.Show("Годовых часов не может быть меньше, чем часов в неделю.", "Ошибка", true);
                return;
            }

            var teacher = Teachers.FirstOrDefault(t => t.Id == FormTeacherId);
            if (teacher != null && teacher.SubjectId.HasValue && teacher.SubjectId.Value != FormSubjectId)
            {
                ToastService.Show("Выбранный предмет не соответствует предмету учителя.", "Ошибка", true);
                return;
            }

            var duplicate = Workloads.Any(w =>
                w.AcademicClassId == FormClassId
                && w.SubjectId == FormSubjectId
                && w.Id != _editingId);
            if (duplicate)
            {
                ToastService.Show("Для этого класса нагрузка по этому предмету уже существует.", "Ошибка", true);
                return;
            }

            try
            {
                var entity = new Workload
                {
                    Id = _editingId,
                    AcademicClassId = FormClassId,
                    TeacherId = FormTeacherId,
                    SubjectId = FormSubjectId,
                    HoursPerWeek = FormHoursPerWeek,
                    YearHours = FormYearHours
                };

                if (_editingId == 0)
                {
                    SchoolApiClient.CreateWorkload(entity);
                }
                else
                {
                    SchoolApiClient.UpdateWorkload(entity);
                }

                LoadAll();
                ClearForm();
                ToastService.Show("Нагрузка сохранена.", "Успешно");
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось сохранить нагрузку: " + ex.Message, "Ошибка", true);
            }
        }

        private void DeleteWorkload()
        {
            if (SelectedWorkload == null)
            {
                return;
            }

            var ok = MessageBox.Show(
                $"Удалить нагрузку: {SelectedWorkload.ClassName} / {SelectedWorkload.SubjectName}?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (ok != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                SchoolApiClient.DeleteWorkload(SelectedWorkload.Id);
                LoadAll();
                ClearForm();
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось удалить нагрузку: " + ex.Message, "Ошибка", true);
            }
        }
    }
}
