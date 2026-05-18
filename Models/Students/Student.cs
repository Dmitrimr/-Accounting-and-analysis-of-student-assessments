using System.ComponentModel.DataAnnotations.Schema;
using WebAPI.Models.Courses;
using WebAPI.Models.Users;

namespace WebAPI.Models.Students
{
    public class Student
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MiddleName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime BirthDate { get; set; }

        public int? CourseId { get; set; }

        [ForeignKey("CourseId")]
        public Course Course { get; set; }

        public List<StudentProgress> StudentProgress { get; set; }
    }
}