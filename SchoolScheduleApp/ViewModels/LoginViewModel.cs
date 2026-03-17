using SchoolSchedule.Entites;
using SchoolScheduleApp.Core;
using SchoolScheduleApp.Views;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SchoolScheduleApp.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly AppSettings _settings;
        private string _username = string.Empty;
        private bool _rememberMe;
        private string _initialPassword = string.Empty;
        private string _errorMessage = string.Empty;

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public bool RememberMe
        {
            get => _rememberMe;
            set { _rememberMe = value; OnPropertyChanged(); }
        }

        public string InitialPassword
        {
            get => _initialPassword;
            set { _initialPassword = value; OnPropertyChanged(); }
        }

        public RelayCommand LoginCommand { get; }

        public LoginViewModel()
        {
            _settings = AppSettingsService.Load();
            SchoolApiClient.ReconfigureBaseUrl(_settings.ApiBaseUrl);

            if (_settings.RememberMe)
            {
                Username = _settings.SavedUsername;
                InitialPassword = _settings.SavedPassword;
                RememberMe = true;
            }

            LoginCommand = new RelayCommand(ExecuteLogin);
        }

        private void ExecuteLogin(object? parameter)
        {
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Введите логин и пароль.";
                return;
            }

            try
            {
                if (!SchoolApiClient.TryCheckHealth(out var diagnostics))
                {
                    var baseUrl = string.IsNullOrWhiteSpace(SchoolApiClient.CurrentBaseUrl)
                        ? _settings.ApiBaseUrl
                        : SchoolApiClient.CurrentBaseUrl;

                    ErrorMessage = $"Нет подключения к API ({baseUrl}).";
                    AppLogger.LogError($"Login blocked because API is unavailable. {diagnostics}");
                    return;
                }

                var user = SchoolApiClient.Login(Username.Trim(), password);
                ErrorMessage = string.Empty;

                _settings.RememberMe = RememberMe;
                _settings.SavedUsername = RememberMe ? Username : string.Empty;
                _settings.SavedPassword = RememberMe ? password : string.Empty;
                AppSettingsService.Save(_settings);

                UserSession.SetUser(user);
                AppLogger.LogInfo($"Login success: {user.Username} ({user.Role})");

                Window? nextWindow = user.Role switch
                {
                    UserRole.Admin => new AdminWindow(),
                    UserRole.Teacher => new TeacherWindow(),
                    _ => null
                };

                if (nextWindow == null)
                {
                    ErrorMessage = "Роль пользователя не поддерживается.";
                    return;
                }

                Application.Current.MainWindow = nextWindow;
                ToastService.Show($"Добро пожаловать, {user.FullName}!", "Успех");
                nextWindow.Show();

                foreach (Window window in Application.Current.Windows)
                {
                    if (window.DataContext == this)
                    {
                        window.Close();
                    }
                }
            }
            catch (ApiException apiEx)
            {
                if (apiEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    ErrorMessage = "Неверный логин или пароль.";
                    return;
                }

                var baseUrl = string.IsNullOrWhiteSpace(SchoolApiClient.CurrentBaseUrl)
                    ? _settings.ApiBaseUrl
                    : SchoolApiClient.CurrentBaseUrl;

                AppLogger.LogError("Login API error.", apiEx);
                ErrorMessage = $"Не удалось подключиться к API ({baseUrl}). Подробности в app.log.";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Login unexpected error.", ex);
                ErrorMessage = "Произошла ошибка входа.";
            }
        }
    }
}
