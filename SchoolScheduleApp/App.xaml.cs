using SchoolScheduleApp.Core;
using System.Windows;

namespace SchoolScheduleApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var settings = AppSettingsService.Load();
            ThemeManager.SetTheme(settings.IsDarkTheme);
            SchoolApiClient.ReconfigureBaseUrl(settings.ApiBaseUrl);
        }
    }
}
