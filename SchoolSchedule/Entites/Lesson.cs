using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchoolSchedule.Entites
{
    [Table("Lessons")]
    public class Lesson
    {
        [Key]
        public int Id { get; set; }

        // Координаты в сетке расписания
        public string WeekStartDate { get; set; } = string.Empty;
        public int DayOfWeek { get; set; } 
        public int LessonIndex { get; set; } 

        // Ссылки на сущности
        public int TeacherId { get; set; }
        public virtual Teacher Teacher { get; set; }

        public int SubjectId { get; set; }
        public virtual Subject Subject { get; set; }

        public int AcademicClassId { get; set; }
        public virtual AcademicClass AcademicClass { get; set; }

        public int? ClassroomId { get; set; } 
        public virtual Classroom Classroom { get; set; }
    }
}
