using SchoolSchedule.Entites;
using SchoolScheduleApp.Core;
using SchoolScheduleApp.Views.Windows;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace SchoolScheduleApp.ViewModels
{
    public class StudentsViewModel : ViewModelBase
    {
        private ObservableCollection<AcademicClass> _classesList = new();
        public ObservableCollection<AcademicClass> ClassesList
        {
            get => _classesList;
            set { _classesList = value; OnPropertyChanged(); }
        }

        private AcademicClass? _selectedClass;
        public AcademicClass? SelectedClass
        {
            get => _selectedClass;
            set { _selectedClass = value; OnPropertyChanged(); }
        }

        public RelayCommand AddClassCommand { get; }
        public RelayCommand EditClassCommand { get; }
        public RelayCommand DeleteClassCommand { get; }
        public RelayCommand RefreshClassesCommand { get; }

        public StudentsViewModel()
        {
            AddClassCommand = new RelayCommand(_ => ExecuteAddClass());
            EditClassCommand = new RelayCommand(ExecuteEditClass, CanEditOrDelete);
            DeleteClassCommand = new RelayCommand(ExecuteDeleteClass, CanEditOrDelete);
            RefreshClassesCommand = new RelayCommand(_ => LoadData());

            LoadData();
        }

        private bool CanEditOrDelete(object? obj)
        {
            var ac = obj as AcademicClass ?? SelectedClass;
            return ac != null;
        }

        private void LoadData()
        {
            try
            {
                var classes = SchoolApiClient.GetAcademicClasses()
                    .OrderBy(c => c.Id)
                    .ToList();

                ClassesList = new ObservableCollection<AcademicClass>(classes);
            }
            catch (Exception ex)
            {
                ToastService.Show("РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РіСЂСѓР·РёС‚СЊ РєР»Р°СЃСЃС‹: " + ex.Message, "РћС€РёР±РєР°", true);
            }
        }

        private void ExecuteAddClass()
        {
            var wnd = new ClassEditWindow(null)
            {
                Owner = Application.Current?.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive && w is not ClassEditWindow)
                    ?? Application.Current?.MainWindow
            };

            if (wnd.ShowDialog() != true)
            {
                return;
            }

            var newClass = wnd.AcademicClass;

            try
            {
                var existing = SchoolApiClient.GetAcademicClasses();

                if (existing.Any(c => string.Equals(c.Name, newClass.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    ToastService.Show("РљР»Р°СЃСЃ СЃ С‚Р°РєРёРј РЅР°Р·РІР°РЅРёРµРј СѓР¶Рµ СЃСѓС‰РµСЃС‚РІСѓРµС‚.", "РћС€РёР±РєР°", true);
                    return;
                }

                if (newClass.CuratorTeacherId != null)
                {
                    var curatorCount = existing.Count(c => c.CuratorTeacherId == newClass.CuratorTeacherId);
                    if (curatorCount >= 2)
                    {
                        ToastService.Show("Этот учитель уже назначен классным руководителем двух классов.", "Ошибка", true);
                        return;
                    }
                }

                var created = SchoolApiClient.CreateAcademicClass(newClass);
                LoadData();
                SelectedClass = ClassesList.FirstOrDefault(x => x.Id == created.Id);
            }
            catch (Exception ex)
            {
                ToastService.Show("РќРµ СѓРґР°Р»РѕСЃСЊ РґРѕР±Р°РІРёС‚СЊ РєР»Р°СЃСЃ: " + ex.Message, "РћС€РёР±РєР°", true);
            }
        }

        private void ExecuteEditClass(object? obj)
        {
            var ac = obj as AcademicClass ?? SelectedClass;
            if (ac == null)
            {
                return;
            }

            var editable = new AcademicClass
            {
                Id = ac.Id,
                Name = ac.Name,
                StudentCount = ac.StudentCount,
                Shift = ac.Shift,
                CuratorTeacherId = ac.CuratorTeacherId
            };

            var wnd = new ClassEditWindow(editable)
            {
                Owner = Application.Current?.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive && w is not ClassEditWindow)
                    ?? Application.Current?.MainWindow
            };

            if (wnd.ShowDialog() != true)
            {
                return;
            }

            var updated = wnd.AcademicClass;

            try
            {
                var existing = SchoolApiClient.GetAcademicClasses();

                var nameExists = existing.Any(c =>
                    c.Id != updated.Id
                    && string.Equals(c.Name, updated.Name, StringComparison.OrdinalIgnoreCase));
                if (nameExists)
                {
                    ToastService.Show("РљР»Р°СЃСЃ СЃ С‚Р°РєРёРј РЅР°Р·РІР°РЅРёРµРј СѓР¶Рµ СЃСѓС‰РµСЃС‚РІСѓРµС‚.", "РћС€РёР±РєР°", true);
                    return;
                }

                if (updated.CuratorTeacherId != null)
                {
                    var curatorCount = existing.Count(c =>
                        c.Id != updated.Id
                        && c.CuratorTeacherId == updated.CuratorTeacherId);

                    if (curatorCount >= 2)
                    {
                        ToastService.Show("Этот учитель уже назначен классным руководителем двух классов.", "Ошибка", true);
                        return;
                    }
                }

                SchoolApiClient.UpdateAcademicClass(updated);
                LoadData();
                SelectedClass = ClassesList.FirstOrDefault(x => x.Id == updated.Id);
            }
            catch (Exception ex)
            {
                ToastService.Show("РќРµ СѓРґР°Р»РѕСЃСЊ РёР·РјРµРЅРёС‚СЊ РєР»Р°СЃСЃ: " + ex.Message, "РћС€РёР±РєР°", true);
            }
        }

        private void ExecuteDeleteClass(object? obj)
        {
            var ac = obj as AcademicClass ?? SelectedClass;
            if (ac == null)
            {
                return;
            }

            try
            {
                var hasWorkloads = SchoolApiClient.GetWorkloads(classId: ac.Id).Any();
                if (hasWorkloads)
                {
                    ToastService.Show("РќРµР»СЊР·СЏ СѓРґР°Р»РёС‚СЊ РєР»Р°СЃСЃ: РґР»СЏ РЅРµРіРѕ СѓР¶Рµ Р·Р°РґР°РЅР° РЅР°РіСЂСѓР·РєР°.", "РћС€РёР±РєР°", true);
                    return;
                }

                var result = MessageBox.Show(
                    $"РЈРґР°Р»РёС‚СЊ РєР»Р°СЃСЃ \"{ac.Name}\"?",
                    "РџРѕРґС‚РІРµСЂР¶РґРµРЅРёРµ",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                SchoolApiClient.DeleteAcademicClass(ac.Id);

                LoadData();
                SelectedClass = null;
            }
            catch (Exception ex)
            {
                ToastService.Show("РќРµ СѓРґР°Р»РѕСЃСЊ СѓРґР°Р»РёС‚СЊ РєР»Р°СЃСЃ: " + ex.Message, "РћС€РёР±РєР°", true);
            }
        }
    }
}
