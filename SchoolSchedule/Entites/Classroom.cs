using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolSchedule.Entites
{
    [Table("Classrooms")]
    public class Classroom
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Number { get; set; } = string.Empty; // "101", "205А", "Актовый зал"

        public int Capacity { get; set; } // Вместимость (человек)

        [MaxLength(50)]
        public string? Type { get; set; } // "Обычный", "Компьютерный", "Спортзал"

        public override string ToString()
            => string.IsNullOrWhiteSpace(Type) ? Number : $"{Number} ({Type})";
    }
}
