using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchoolSchedule.Entites
{
    [Table("Workloads")]
    public class Workload
    {
        [Key]
        public int Id { get; set; }

        // Связи (Внешние ключи)
        public int TeacherId { get; set; }
        public virtual Teacher Teacher { get; set; }

        public int SubjectId { get; set; }
        public virtual Subject Subject { get; set; }

        public int AcademicClassId { get; set; }
        public virtual AcademicClass AcademicClass { get; set; }

        [Required]
        public int HoursPerWeek { get; set; } // Часов в неделю (план)

        [Required]
        public int YearHours { get; set; } // Часов в год (план)

        [NotMapped]
        public int RemainingHours { get; set; } // Текущий остаток годовых часов
    }
}
