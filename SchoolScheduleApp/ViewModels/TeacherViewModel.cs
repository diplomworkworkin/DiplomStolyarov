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
    public class TeacherRow
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string StatusText => IsActive ? "Активен" : "Неактивен";
    }

    public class TeachersViewModel : ViewModelBase
    {
        private ObservableCollection<TeacherRow> _teachersList = new();
        public ObservableCollection<TeacherRow> TeachersList
        {
            get => _teachersList;
            set { _teachersList = value; OnPropertyChanged(); }
        }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }

        public TeachersViewModel()
        {
            AddCommand = new RelayCommand(_ => ExecuteAdd());
            EditCommand = new RelayCommand(o => ExecuteEdit(o as TeacherRow));
            DeleteCommand = new RelayCommand(o => ExecuteDelete(o as TeacherRow));

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var workloads = SchoolApiClient.GetWorkloads();
                var lessons = SchoolApiClient.GetLessons();
                var teachers = SchoolApiClient.GetTeachers();

                var activeTeacherIds = new HashSet<int>(workloads.Select(w => w.TeacherId));
                foreach (var lessonTeacherId in lessons.Select(l => l.TeacherId).Distinct())
                {
                    activeTeacherIds.Add(lessonTeacherId);
                }

                var teacherRows = teachers
                    .OrderBy(t => t.Id)
                    .Select(t => new TeacherRow
                    {
                        Id = t.Id,
                        FullName = t.FullName,
                        SubjectName = t.Subject?.Name ?? "-",
                        IsActive = activeTeacherIds.Contains(t.Id)
                    })
                    .ToList();

                TeachersList = new ObservableCollection<TeacherRow>(teacherRows);
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось загрузить список учителей: " + ex.Message, "Ошибка", true);
            }
        }

        private void ExecuteAdd()
        {
            var wnd = new TeacherEditWindow(new Teacher())
            {
                Owner = Application.Current?.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive && w is not TeacherEditWindow)
                    ?? Application.Current?.MainWindow
            };

            if (wnd.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var createdTeacher = SchoolApiClient.CreateTeacher(wnd.Teacher);

                var user = new User
                {
                    Username = $"teacher{createdTeacher.Id}",
                    Password = $"teacher{createdTeacher.Id}",
                    FullName = createdTeacher.FullName,
                    Role = UserRole.Teacher,
                    TeacherId = createdTeacher.Id
                };
                SchoolApiClient.CreateUser(user);

                LoadData();
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось добавить учителя: " + ex.Message, "Ошибка", true);
            }
        }

        private void ExecuteEdit(TeacherRow? teacher)
        {
            if (teacher == null)
            {
                return;
            }

            try
            {
                var fromApi = SchoolApiClient.GetTeacherById(teacher.Id);
                if (fromApi == null)
                {
                    ToastService.Show("Учитель не найден.", "Ошибка", true);
                    return;
                }

                var editable = new Teacher
                {
                    Id = fromApi.Id,
                    FullName = fromApi.FullName,
                    SubjectId = fromApi.SubjectId,
                    ClassroomId = fromApi.ClassroomId
                };

                var wnd = new TeacherEditWindow(editable)
                {
                    Owner = Application.Current?.Windows.OfType<Window>()
                        .FirstOrDefault(w => w.IsActive && w is not TeacherEditWindow)
                        ?? Application.Current?.MainWindow
                };
                if (wnd.ShowDialog() != true)
                {
                    return;
                }

                SchoolApiClient.UpdateTeacher(editable);

                var relatedUser = SchoolApiClient.GetUsers(role: (int)UserRole.Teacher)
                    .FirstOrDefault(u => u.TeacherId == editable.Id);
                if (relatedUser != null)
                {
                    SchoolApiClient.UpdateUserFullName(relatedUser.Id, editable.FullName);
                }

                LoadData();
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось изменить учителя: " + ex.Message, "Ошибка", true);
            }
        }

        private void ExecuteDelete(TeacherRow? teacher)
        {
            if (teacher == null)
            {
                return;
            }

            var result = MessageBox.Show(
                $"Удалить учителя \"{teacher.FullName}\"?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var relatedUsers = SchoolApiClient.GetUsers(role: (int)UserRole.Teacher)
                    .Where(u => u.TeacherId == teacher.Id)
                    .ToList();

                foreach (var user in relatedUsers)
                {
                    SchoolApiClient.DeleteUser(user.Id);
                }

                SchoolApiClient.DeleteTeacher(teacher.Id);
                LoadData();
            }
            catch (Exception ex)
            {
                ToastService.Show("Не удалось удалить учителя: " + ex.Message, "Ошибка", true);
            }
        }
    }
}
