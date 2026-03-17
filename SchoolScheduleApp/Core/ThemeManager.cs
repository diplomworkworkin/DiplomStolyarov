using MaterialDesignThemes.Wpf;
using System;
using System.Linq;
using System.Windows;

namespace SchoolScheduleApp.Core
{
    public static class ThemeManager
    {
        public static void SetTheme(bool isDark)
        {
            // 1) Меняем базовую тему MaterialDesign (Dark/Light)
            //    Это нужно, чтобы ComboBox/DataGrid/Popup нормально переключались.
            var paletteHelper = new PaletteHelper();
            var mdTheme = paletteHelper.GetTheme();
            mdTheme.SetBaseTheme(isDark ? Theme.Dark : Theme.Light);
            paletteHelper.SetTheme(mdTheme);

            // 2) Подменяем нашу тему (Themes/DarkTheme.xaml или Themes/LightTheme.xaml)
            //    ВАЖНО: больше не удаляем словарь по индексу 0 (там лежит MaterialDesign)
            var themeName = isDark ? "DarkTheme" : "LightTheme";
            var uri = new Uri($"Themes/{themeName}.xaml", UriKind.Relative);
            var newThemeDict = new ResourceDictionary { Source = uri };

            var merged = Application.Current.Resources.MergedDictionaries;
            var oldTheme = merged.FirstOrDefault(d => d.Source != null &&
                                                      (d.Source.OriginalString.EndsWith("Themes/DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                                                       d.Source.OriginalString.EndsWith("Themes/LightTheme.xaml", StringComparison.OrdinalIgnoreCase)));

            if (oldTheme != null)
            {
                var index = merged.IndexOf(oldTheme);
                merged[index] = newThemeDict;
            }
            else
            {
                // если по какой-то причине темы не было — просто добавляем в конец
                merged.Add(newThemeDict);
            }
        }
    }
}
