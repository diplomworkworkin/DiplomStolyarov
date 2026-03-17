using SchoolSchedule.Entites;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SchoolScheduleApp.Core
{
    public enum MessageCategory
    {
        LessonReplacement = 0,
        ScheduleChange = 1
    }

    public enum MessageStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public enum ReplacementMode
    {
        AddMyLesson = 0,
        ReplaceMyLesson = 1
    }

    public class ChatMessage
    {
        public UserRole SenderRole { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
    }

    public class MessageThread
    {
        public Guid Id { get; set; }
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public MessageCategory Category { get; set; }
        public MessageStatus Status { get; set; } = MessageStatus.Pending;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ReplacementMode? ReplacementMode { get; set; }
        public int? ReplacementTeacherId { get; set; }
        public string? ReplacementTeacherName { get; set; }

        public int? TargetClassId { get; set; }
        public string? TargetClassName { get; set; }
        public int? TargetDayOfWeek { get; set; }
        public int? TargetLessonIndex { get; set; }

        public bool IsReadByAdmin { get; set; }
        public bool IsReadByTeacher { get; set; }

        public List<ChatMessage> Messages { get; set; } = new();
    }

    public static class MessageRequestService
    {
        private static readonly object Locker = new();
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private static string StoragePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "message_requests.json");

        public static IReadOnlyList<MessageThread> GetForAdmin()
        {
            lock (Locker)
            {
                return LoadUnsafe().OrderByDescending(x => x.UpdatedAt).ToList();
            }
        }

        public static IReadOnlyList<MessageThread> GetForTeacher(int teacherId)
        {
            lock (Locker)
            {
                return LoadUnsafe()
                    .Where(x => x.TeacherId == teacherId)
                    .OrderByDescending(x => x.UpdatedAt)
                    .ToList();
            }
        }

        public static int GetUnreadAdminCount()
        {
            lock (Locker)
            {
                return LoadUnsafe().Count(x => !x.IsReadByAdmin);
            }
        }

        public static int GetUnreadTeacherCount(int teacherId)
        {
            lock (Locker)
            {
                return LoadUnsafe().Count(x => x.TeacherId == teacherId && !x.IsReadByTeacher);
            }
        }

        public static void MarkAdminRead()
        {
            lock (Locker)
            {
                var items = LoadUnsafe();
                var changed = false;

                foreach (var item in items.Where(x => !x.IsReadByAdmin))
                {
                    item.IsReadByAdmin = true;
                    changed = true;
                }

                if (changed)
                {
                    SaveUnsafe(items);
                }
            }
        }

        public static void MarkTeacherRead(int teacherId)
        {
            lock (Locker)
            {
                var items = LoadUnsafe();
                var changed = false;

                foreach (var item in items.Where(x => x.TeacherId == teacherId && !x.IsReadByTeacher))
                {
                    item.IsReadByTeacher = true;
                    changed = true;
                }

                if (changed)
                {
                    SaveUnsafe(items);
                }
            }
        }

        public static Guid CreateThreadFromTeacher(
            User user,
            MessageCategory category,
            string text,
            int? classId,
            string? className,
            int? dayOfWeek,
            int? lessonIndex,
            ReplacementMode? replacementMode,
            int? replacementTeacherId,
            string? replacementTeacherName)
        {
            if (user.TeacherId == null)
            {
                throw new InvalidOperationException("Только учитель может создавать заявку.");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Сообщение не может быть пустым.");
            }

            lock (Locker)
            {
                var now = DateTime.Now;
                var thread = new MessageThread
                {
                    Id = Guid.NewGuid(),
                    TeacherId = user.TeacherId.Value,
                    TeacherName = user.FullName,
                    Category = category,
                    CreatedAt = now,
                    UpdatedAt = now,
                    TargetClassId = classId,
                    TargetClassName = className,
                    TargetDayOfWeek = dayOfWeek,
                    TargetLessonIndex = lessonIndex,
                    ReplacementMode = replacementMode,
                    ReplacementTeacherId = replacementTeacherId,
                    ReplacementTeacherName = replacementTeacherName,
                    IsReadByAdmin = false,
                    IsReadByTeacher = true,
                    Messages = new List<ChatMessage>
                    {
                        new()
                        {
                            SenderRole = UserRole.Teacher,
                            SenderName = user.FullName,
                            Text = text.Trim(),
                            SentAt = now
                        }
                    }
                };

                var items = LoadUnsafe();
                items.Add(thread);
                SaveUnsafe(items);
                return thread.Id;
            }
        }

        public static bool AddTeacherMessage(Guid threadId, User user, string text)
        {
            if (user.TeacherId == null || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            lock (Locker)
            {
                var items = LoadUnsafe();
                var thread = items.FirstOrDefault(x => x.Id == threadId && x.TeacherId == user.TeacherId.Value);
                if (thread == null)
                {
                    return false;
                }

                thread.Messages.Add(new ChatMessage
                {
                    SenderRole = UserRole.Teacher,
                    SenderName = user.FullName,
                    Text = text.Trim(),
                    SentAt = DateTime.Now
                });

                thread.UpdatedAt = DateTime.Now;
                thread.IsReadByAdmin = false;
                thread.IsReadByTeacher = true;
                SaveUnsafe(items);
                return true;
            }
        }

        public static bool AddAdminMessage(Guid threadId, string adminName, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            lock (Locker)
            {
                var items = LoadUnsafe();
                var thread = items.FirstOrDefault(x => x.Id == threadId);
                if (thread == null)
                {
                    return false;
                }

                thread.Messages.Add(new ChatMessage
                {
                    SenderRole = UserRole.Admin,
                    SenderName = string.IsNullOrWhiteSpace(adminName) ? "Администратор" : adminName.Trim(),
                    Text = text.Trim(),
                    SentAt = DateTime.Now
                });

                thread.UpdatedAt = DateTime.Now;
                thread.IsReadByAdmin = true;
                thread.IsReadByTeacher = false;
                SaveUnsafe(items);
                return true;
            }
        }

        public static bool UpdateStatus(Guid threadId, MessageStatus status)
        {
            lock (Locker)
            {
                var items = LoadUnsafe();
                var thread = items.FirstOrDefault(x => x.Id == threadId);
                if (thread == null)
                {
                    return false;
                }

                thread.Status = status;
                thread.UpdatedAt = DateTime.Now;
                thread.IsReadByAdmin = true;
                thread.IsReadByTeacher = false;
                SaveUnsafe(items);
                return true;
            }
        }

        private static List<MessageThread> LoadUnsafe()
        {
            if (!File.Exists(StoragePath))
            {
                return new List<MessageThread>();
            }

            var json = File.ReadAllText(StoragePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<MessageThread>();
            }

            return JsonSerializer.Deserialize<List<MessageThread>>(json, JsonOptions) ?? new List<MessageThread>();
        }

        private static void SaveUnsafe(List<MessageThread> data)
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(StoragePath, json);
        }
    }
}
