using SchoolSchedule.Entites;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

namespace SchoolScheduleApp.Core
{
    public sealed class ApiException : Exception
    {
        public HttpStatusCode? StatusCode { get; }

        public ApiException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }

    public static class SchoolApiClient
    {
        private const string FallbackApiBaseUrl = "http://127.0.0.1:8000/";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions RequestJsonOptions = new()
        {
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null
        };

        private static readonly object SyncRoot = new();
        private static HttpClient _httpClient = BuildClient();

        public static string CurrentBaseUrl => _httpClient.BaseAddress?.ToString()?.TrimEnd('/') ?? string.Empty;

        public static void ReconfigureBaseUrl(string? baseUrl = null)
        {
            lock (SyncRoot)
            {
                _httpClient.Dispose();
                _httpClient = BuildClient(baseUrl);
            }
        }

        public static bool CheckHealth()
        {
            return TryCheckHealth(out _);
        }

        public static bool TryCheckHealth(out string diagnostics)
        {
            try
            {
                using var response = Send(HttpMethod.Get, "/health");
                var method = response.RequestMessage?.Method.ToString() ?? "GET";
                var requestUri = response.RequestMessage?.RequestUri?.ToString() ?? "(unknown)";

                if (response.IsSuccessStatusCode)
                {
                    AppLogger.LogInfo(
                        $"API response <- {(int)response.StatusCode} {response.StatusCode} for {method} {requestUri}");
                    diagnostics = $"API is reachable: {CurrentBaseUrl}";
                    return true;
                }

                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var detail = ExtractErrorDetail(content);
                diagnostics =
                    $"API responded with {(int)response.StatusCode} ({response.StatusCode}) at {CurrentBaseUrl}. {detail}";

                AppLogger.LogError($"API health check failed. {diagnostics}");
                return false;
            }
            catch (ApiException ex)
            {
                diagnostics = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                diagnostics = $"Health check failed: {ex.Message}";
                return false;
            }
        }

        public static User Login(string username, string password)
        {
            return SendAndRead<User>(
                HttpMethod.Post,
                "/auth/login",
                new
                {
                    Username = username,
                    Password = password
                });
        }

        public static List<Subject> GetSubjects()
            => SendAndRead<List<Subject>>(HttpMethod.Get, "/subjects");

        public static Subject CreateSubject(string name)
            => SendAndRead<Subject>(HttpMethod.Post, "/subjects", new { Name = name });

        public static Subject UpdateSubject(int id, string name)
            => SendAndRead<Subject>(HttpMethod.Patch, $"/subjects/{id}", new { Name = name });

        public static void DeleteSubject(int id)
            => EnsureSuccess(HttpMethod.Delete, $"/subjects/{id}");

        public static List<Classroom> GetClassrooms()
            => SendAndRead<List<Classroom>>(HttpMethod.Get, "/classrooms");

        public static Classroom CreateClassroom(string number, int capacity, string? type)
            => SendAndRead<Classroom>(HttpMethod.Post, "/classrooms", new
            {
                Number = number,
                Capacity = capacity,
                Type = type
            });

        public static Classroom UpdateClassroom(int id, string number, int capacity, string? type)
            => SendAndRead<Classroom>(HttpMethod.Patch, $"/classrooms/{id}", new
            {
                Number = number,
                Capacity = capacity,
                Type = type
            });

        public static void DeleteClassroom(int id)
            => EnsureSuccess(HttpMethod.Delete, $"/classrooms/{id}");

        public static List<Teacher> GetTeachers()
            => SendAndRead<List<Teacher>>(HttpMethod.Get, "/teachers");

        public static Teacher? GetTeacherById(int id)
            => SendAndReadOrDefault<Teacher>(HttpMethod.Get, $"/teachers/{id}");

        public static Teacher CreateTeacher(Teacher teacher)
            => SendAndRead<Teacher>(HttpMethod.Post, "/teachers", new
            {
                teacher.FullName,
                teacher.SubjectId,
                teacher.ClassroomId
            });

        public static Teacher UpdateTeacher(Teacher teacher)
            => SendAndRead<Teacher>(HttpMethod.Patch, $"/teachers/{teacher.Id}", new
            {
                teacher.FullName,
                teacher.SubjectId,
                teacher.ClassroomId
            });

        public static void DeleteTeacher(int id)
            => EnsureSuccess(HttpMethod.Delete, $"/teachers/{id}");

        public static List<AcademicClass> GetAcademicClasses()
            => SendAndRead<List<AcademicClass>>(HttpMethod.Get, "/classes");

        public static AcademicClass? GetAcademicClassById(int id)
            => SendAndReadOrDefault<AcademicClass>(HttpMethod.Get, $"/classes/{id}");

        public static AcademicClass CreateAcademicClass(AcademicClass academicClass)
            => SendAndRead<AcademicClass>(HttpMethod.Post, "/classes", new
            {
                academicClass.Name,
                academicClass.StudentCount,
                academicClass.Shift,
                academicClass.CuratorTeacherId
            });

        public static AcademicClass UpdateAcademicClass(AcademicClass academicClass)
            => SendAndRead<AcademicClass>(HttpMethod.Patch, $"/classes/{academicClass.Id}", new
            {
                academicClass.Name,
                academicClass.StudentCount,
                academicClass.Shift,
                academicClass.CuratorTeacherId
            });

        public static void DeleteAcademicClass(int id)
            => EnsureSuccess(HttpMethod.Delete, $"/classes/{id}");

        public static List<Workload> GetWorkloads(int? teacherId = null, int? classId = null, int? subjectId = null)
        {
            var query = BuildQuery("/workloads", new Dictionary<string, object?>
            {
                ["teacher_id"] = teacherId,
                ["class_id"] = classId,
                ["subject_id"] = subjectId
            });
            return SendAndRead<List<Workload>>(HttpMethod.Get, query);
        }

        public static Workload CreateWorkload(Workload workload)
            => SendAndRead<Workload>(HttpMethod.Post, "/workloads", new
            {
                workload.TeacherId,
                workload.SubjectId,
                AcademicClassId = workload.AcademicClassId,
                workload.HoursPerWeek,
                workload.YearHours
            });

        public static Workload UpdateWorkload(Workload workload)
            => SendAndRead<Workload>(HttpMethod.Patch, $"/workloads/{workload.Id}", new
            {
                workload.TeacherId,
                workload.SubjectId,
                AcademicClassId = workload.AcademicClassId,
                workload.HoursPerWeek,
                workload.YearHours
            });

        public static void DeleteWorkload(int id)
            => EnsureSuccess(HttpMethod.Delete, $"/workloads/{id}");

        public static List<Lesson> GetLessons(
            int? classId = null,
            int? teacherId = null,
            int? dayOfWeek = null,
            string? weekStartDate = null)
        {
            var query = BuildQuery("/lessons", new Dictionary<string, object?>
            {
                ["class_id"] = classId,
                ["teacher_id"] = teacherId,
                ["day_of_week"] = dayOfWeek,
                ["week_start"] = weekStartDate
            });
            return SendAndRead<List<Lesson>>(HttpMethod.Get, query);
        }

        public static Lesson? GetLessonById(int id)
            => SendAndReadOrDefault<Lesson>(HttpMethod.Get, $"/lessons/{id}");

        public static Lesson CreateLesson(Lesson lesson)
            => SendAndRead<Lesson>(HttpMethod.Post, "/lessons", new
            {
                lesson.WeekStartDate,
                lesson.DayOfWeek,
                lesson.LessonIndex,
                lesson.TeacherId,
                lesson.SubjectId,
                AcademicClassId = lesson.AcademicClassId,
                lesson.ClassroomId
            });

        public static Lesson UpdateLesson(Lesson lesson)
            => SendAndRead<Lesson>(HttpMethod.Patch, $"/lessons/{lesson.Id}", new
            {
                lesson.WeekStartDate,
                lesson.DayOfWeek,
                lesson.LessonIndex,
                lesson.TeacherId,
                lesson.SubjectId,
                AcademicClassId = lesson.AcademicClassId,
                lesson.ClassroomId
            });

        public static void DeleteLesson(int id)
            => EnsureSuccess(HttpMethod.Delete, $"/lessons/{id}");

        public static List<User> GetUsers(int? role = null)
        {
            var query = BuildQuery("/users", new Dictionary<string, object?>
            {
                ["role"] = role
            });
            return SendAndRead<List<User>>(HttpMethod.Get, query);
        }

        public static User CreateUser(User user)
            => SendAndRead<User>(HttpMethod.Post, "/users", new
            {
                user.Username,
                user.Password,
                user.FullName,
                Role = (int)user.Role,
                user.TeacherId,
                user.AcademicClassId
            });

        public static User UpdateUser(User user)
        {
            var payload = new Dictionary<string, object?>
            {
                ["Username"] = user.Username,
                ["FullName"] = user.FullName,
                ["Role"] = (int)user.Role,
                ["TeacherId"] = user.TeacherId,
                ["AcademicClassId"] = user.AcademicClassId
            };

            if (!string.IsNullOrWhiteSpace(user.Password))
            {
                payload["Password"] = user.Password;
            }

            return SendAndRead<User>(HttpMethod.Patch, $"/users/{user.Id}", payload);
        }

        public static User UpdateUserFullName(int userId, string fullName)
            => SendAndRead<User>(HttpMethod.Patch, $"/users/{userId}", new { FullName = fullName });

        public static void DeleteUser(int id)
            => EnsureSuccess(HttpMethod.Delete, $"/users/{id}");

        private static HttpClient BuildClient(string? baseUrl = null)
        {
            var settings = AppSettingsService.Load();
            var normalizedUrl = NormalizeBaseUrl(baseUrl ?? settings.ApiBaseUrl);

            return new HttpClient
            {
                BaseAddress = new Uri(normalizedUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        private static string NormalizeBaseUrl(string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return FallbackApiBaseUrl;
            }

            var value = baseUrl.Trim();
            if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                value = "http://" + value;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                AppLogger.LogError($"Invalid ApiBaseUrl '{baseUrl}'. Fallback to {FallbackApiBaseUrl}");
                return FallbackApiBaseUrl;
            }

            var builder = new UriBuilder(uri)
            {
                Query = string.Empty,
                Fragment = string.Empty
            };

            if (string.Equals(builder.Host, "0.0.0.0", StringComparison.OrdinalIgnoreCase))
            {
                builder.Host = "127.0.0.1";
            }
            else if (string.Equals(builder.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                // Avoid localhost -> ::1 resolution issues when API listens only on IPv4.
                builder.Host = "127.0.0.1";
            }

            if (string.IsNullOrEmpty(builder.Path))
            {
                builder.Path = "/";
            }
            else if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
            {
                builder.Path += "/";
            }

            return builder.Uri.ToString();
        }

        private static HttpResponseMessage Send(HttpMethod method, string path, object? body = null)
        {
            var requestUri = BuildRequestUri(path);

            try
            {
                using var request = new HttpRequestMessage(method, requestUri);
                if (body != null)
                {
                    request.Content = JsonContent.Create(body, options: RequestJsonOptions);
                }

                AppLogger.LogInfo($"API request -> {method} {requestUri}");
                return _httpClient.Send(request);
            }
            catch (TaskCanceledException ex)
            {
                var message =
                    $"API request timeout for {method} {requestUri}. Timeout: {_httpClient.Timeout.TotalSeconds:0} sec.";
                AppLogger.LogError(message, ex);
                throw new ApiException(message, null, ex);
            }
            catch (HttpRequestException ex)
            {
                var message = BuildRequestFailureMessage(method, requestUri, ex);
                AppLogger.LogError(message, ex);
                throw new ApiException(message, null, ex);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected API request error for {method} {requestUri}: {ex.Message}";
                AppLogger.LogError(message, ex);
                throw new ApiException(message, null, ex);
            }
        }

        private static T SendAndRead<T>(HttpMethod method, string path, object? body = null)
        {
            using var response = Send(method, path, body);
            EnsureSuccess(response);

            try
            {
                var payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var value = JsonSerializer.Deserialize<T>(payload, JsonOptions);
                if (value == null)
                {
                    throw new ApiException("API returned an empty response.");
                }

                return value;
            }
            catch (ApiException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ApiException($"Failed to parse API response: {ex.Message}", response.StatusCode, ex);
            }
        }

        private static T? SendAndReadOrDefault<T>(HttpMethod method, string path)
        {
            using var response = Send(method, path);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            EnsureSuccess(response);
            var payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<T>(payload, JsonOptions);
        }

        private static void EnsureSuccess(HttpMethod method, string path)
        {
            using var response = Send(method, path);
            EnsureSuccess(response);
        }

        private static void EnsureSuccess(HttpResponseMessage response)
        {
            var method = response.RequestMessage?.Method.ToString() ?? "UNKNOWN";
            var requestUri = response.RequestMessage?.RequestUri?.ToString() ?? "(unknown)";

            if (response.IsSuccessStatusCode)
            {
                AppLogger.LogInfo($"API response <- {(int)response.StatusCode} {response.StatusCode} for {method} {requestUri}");
                return;
            }

            var errorContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var detail = ExtractErrorDetail(errorContent);
            AppLogger.LogError(
                $"API response <- {(int)response.StatusCode} {response.StatusCode} for {method} {requestUri}. {detail}");

            throw new ApiException(
                $"API returned {(int)response.StatusCode} ({response.StatusCode}). {detail}",
                response.StatusCode);
        }

        private static string ExtractErrorDetail(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return "No additional error details.";
            }

            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("detail", out var detailElement))
                {
                    return detailElement.ValueKind == JsonValueKind.String
                        ? detailElement.GetString() ?? "No additional error details."
                        : detailElement.ToString();
                }
            }
            catch
            {
                // Ignore parse errors and return raw content.
            }

            return content;
        }

        private static string BuildQuery(string path, IDictionary<string, object?> parameters)
        {
            var queryParts = parameters
                .Where(x => x.Value != null)
                .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!.ToString()!)}")
                .ToList();

            if (queryParts.Count == 0)
            {
                return path;
            }

            return $"{path}?{string.Join("&", queryParts)}";
        }

        private static Uri BuildRequestUri(string path)
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
            {
                return absoluteUri;
            }

            var baseUri = _httpClient.BaseAddress ?? new Uri(FallbackApiBaseUrl);
            var relativePath = path.StartsWith("/", StringComparison.Ordinal) ? path[1..] : path;
            return new Uri(baseUri, relativePath);
        }

        private static string BuildRequestFailureMessage(HttpMethod method, Uri requestUri, HttpRequestException ex)
        {
            var hint = BuildHostHint(requestUri);
            if (ex.InnerException is SocketException socketEx)
            {
                var socketReason = socketEx.SocketErrorCode switch
                {
                    SocketError.ConnectionRefused => "Connection refused. API server is not running or the port is wrong.",
                    SocketError.HostNotFound => "Host name was not found.",
                    SocketError.TimedOut => "Connection timed out.",
                    SocketError.NetworkUnreachable => "Network is unreachable.",
                    SocketError.AddressNotAvailable => "IP/host is not available on this machine.",
                    _ => $"Socket error: {socketEx.SocketErrorCode}."
                };

                return $"Cannot reach API for {method} {requestUri}. {socketReason} {hint}".Trim();
            }

            return $"Cannot reach API for {method} {requestUri}. {ex.Message} {hint}".Trim();
        }

        private static string BuildHostHint(Uri requestUri)
        {
            if (string.Equals(requestUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return "If Swagger works but WPF does not, try ApiBaseUrl=http://127.0.0.1:8000.";
            }

            if (string.Equals(requestUri.Host, "0.0.0.0", StringComparison.OrdinalIgnoreCase))
            {
                return "Do not use 0.0.0.0 in client settings. Use 127.0.0.1 or a real server IP.";
            }

            return string.Empty;
        }
    }
}
