using WebAPI.Models.Students;

namespace WebAPI.Models.DTO
{
    public class StudentRequestDto
    {
        public int CourseId { get; set; }
        public string FirstName { get; set; } // имя
        public string LastName { get; set; } // фамилия
        public string MiddleName { get; set; } // отчество
        public string Email { get; set; } // эл. почта
        public string PhoneNumber { get; set; } // номер телефона
        public DateTime BirthDate { get; set; } // дата рождения
    }
}