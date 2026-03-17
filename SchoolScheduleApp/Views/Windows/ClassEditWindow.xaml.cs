using SchoolSchedule.Entites;
using SchoolScheduleApp.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchoolScheduleApp.Views.Windows
{
    public partial class ClassEditWindow : Window
    {
        public AcademicClass AcademicClass { get; private set; }
        public ObservableCollection<Teacher> Teachers { get; private set; } = new();

        public ClassEditWindow(AcademicClass? cls)
        {
            InitializeComponent();

            AcademicClass = cls ?? new AcademicClass { Shift = 1, StudentCount = 1 };
            LoadTeachersForCurator();
            DataContext = this;
        }

        private void LoadTeachersForCurator()
        {
            var currentClassId = AcademicClass.Id;

            var classes = SchoolApiClient.GetAcademicClasses();
            var busyTeacherIds = classes
                .Where(c => c.CuratorTeacherId != null && c.Id != currentClassId)
                .Select(c => c.CuratorTeacherId!.Value)
                .ToHashSet();

            var teachers = SchoolApiClient.GetTeachers()
                .Where(t => !busyTeacherIds.Contains(t.Id))
                .OrderBy(t => t.FullName)
                .ToList();

            Teachers = new ObservableCollection<Teacher>(teachers);
        }

        private void BtnClearCurator_Click(object sender, RoutedEventArgs e)
        {
            AcademicClass.CuratorTeacherId = null;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            AcademicClass.Name = (AcademicClass.Name ?? string.Empty)
                .Trim()
                .ToUpper()
                .Replace("–", "-");

            const string pattern = @"^(?:[1-9]|1[0-1])-[А-ЯЁ]$";
            if (!Regex.IsMatch(AcademicClass.Name, pattern))
            {
                ToastService.Show("Название должно быть в формате: 1-А, 2-Б, ... 11-В.", "Ошибка", true);
                return;
            }

            if (AcademicClass.StudentCount < 1 || AcademicClass.StudentCount > 30)
            {
                ToastService.Show("Количество учеников должно быть от 1 до 30.", "Ошибка", true);
                return;
            }

            if (AcademicClass.Shift != 1 && AcademicClass.Shift != 2)
            {
                ToastService.Show("Смена должна быть 1 или 2.", "Ошибка", true);
                return;
            }

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TbStudentCount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!Regex.IsMatch(e.Text, @"^\d+$"))
            {
                e.Handled = true;
                return;
            }

            var tb = (TextBox)sender;
            string newText = tb.Text.Remove(tb.SelectionStart, tb.SelectionLength)
                .Insert(tb.SelectionStart, e.Text);

            if (int.TryParse(newText, out int value))
            {
                e.Handled = value > 30;
            }
        }

        private void TbStudentCount_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            string text = e.DataObject.GetData(DataFormats.Text)?.ToString() ?? string.Empty;
            if (!Regex.IsMatch(text, @"^\d+$"))
            {
                e.CancelCommand();
                return;
            }

            if (int.TryParse(text, out int value) && value > 30)
            {
                e.CancelCommand();
            }
        }
    }
}
