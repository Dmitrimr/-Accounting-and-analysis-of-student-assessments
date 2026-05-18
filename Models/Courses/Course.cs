using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebAPI.Models.Students;
using WebAPI.Models.Teachers;

namespace WebAPI.Models.Courses
{
    public class Course
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string? Description { get; set; }

        public int? TeacherId { get; set; }

        [ForeignKey("TeacherId")]
        public Teacher? Teacher { get; set; }
        public virtual ICollection<Student> Students { get; set; } = new List<Student>();
    }
}