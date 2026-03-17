using System;
using System.IO;
using System.Text.Json;

namespace SchoolScheduleApp.Core
{
    public static class AppSettingsService
    {
        private static string SettingsPath
            => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return new AppSettings();
                }

                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                Normalize(settings);
                return settings;
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            Normalize(settings);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(SettingsPath, json);
        }

        private static void Normalize(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            {
                settings.ApiBaseUrl = "http://127.0.0.1:8000";
            }

            if (settings.LessonDuration <= 0)
            {
                settings.LessonDuration = 45;
            }

            if (settings.BreakDuration < 0)
            {
                settings.BreakDuration = 10;
            }
        }
    }
}
