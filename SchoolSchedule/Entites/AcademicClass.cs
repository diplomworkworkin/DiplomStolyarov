using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchoolSchedule.Entites
{
    [Table("AcademicClasses")]
    public class AcademicClass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(10)]
        public string Name { get; set; } // "11-А", "5-Б"

        public int StudentCount { get; set; } // Количество учеников

        // Смена (1 или 2). Важно для расписания
        public int Shift { get; set; } = 1;
        public int? CuratorTeacherId { get; set; }
        public virtual Teacher? CuratorTeacher { get; set; }

        public virtual ICollection<Workload> Workloads { get; set; } = new List<Workload>();
    }
}
