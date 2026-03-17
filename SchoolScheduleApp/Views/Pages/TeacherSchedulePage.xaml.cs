using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace SchoolScheduleApp.Views.Pages
{
    public partial class TeacherSchedulePage : Page
    {
        public TeacherSchedulePage()
        {
            InitializeComponent();
        }

        private void ScheduleGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Opacity = 0;
            var anim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            e.Row.BeginAnimation(OpacityProperty, anim);
        }
    }
}
