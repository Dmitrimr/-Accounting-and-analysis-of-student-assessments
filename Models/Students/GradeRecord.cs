using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.Models.Students
{
    public class GradeRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int StudentProgressId { get; set; }

        [ForeignKey("StudentProgressId")]
        public virtual StudentProgress StudentProgress { get; set; }

        public string Subject { get; set; }
        public double Grade { get; set; }
        public DateTime Date { get; set; }
        public string? Comment { get; set; }
    }
}