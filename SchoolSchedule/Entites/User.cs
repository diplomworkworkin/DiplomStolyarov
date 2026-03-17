using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchoolSchedule.Entites
{
    public enum UserRole
    {
        Admin = 0,    // Завуч/Админ (полный доступ)
        Teacher = 1,  // Учитель (только просмотр своего)
        Student = 2   // Ученик (только просмотр класса)
    }

    [Table("Users")] // Явное имя таблицы в БД
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } // Логин

        [Required]
        [MaxLength(100)]
        public string Password { get; set; } // Пароль 

        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } // ФИО (например: "Петрова Анна Ивановна")

        // Привязка к учителю (если роль Teacher)
        public int? TeacherId { get; set; }
        public Teacher? Teacher { get; set; }

        // Привязка к классу (если роль Student)
        public int? AcademicClassId { get; set; }
        public AcademicClass? AcademicClass { get; set; }

        public UserRole Role { get; set; }
    }
}
