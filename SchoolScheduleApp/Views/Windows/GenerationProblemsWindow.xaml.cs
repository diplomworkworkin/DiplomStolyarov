using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SchoolScheduleApp.Views.Windows
{
    public partial class GenerationProblemsWindow : Window
    {
        private readonly List<string> _problems;

        public GenerationProblemsWindow(IEnumerable<string> problems)
        {
            InitializeComponent();

            _problems = problems
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            ProblemsList.ItemsSource = _problems;
        }

        private void CopyAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_problems.Count == 0)
            {
                return;
            }

            Clipboard.SetText(string.Join(Environment.NewLine, _problems));
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
