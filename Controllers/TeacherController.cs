using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.DbContexts;
using WebAPI.Models.DTO;
using WebAPI.Models.Teachers;
using WebAPI.Models.Users;
using WebAPI.Utils;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class TeacherController : ControllerBase
    {
        private readonly ApplicationContext _context;

        public TeacherController(ApplicationContext context)
        {
            _context = context;
        }

        // GET: api/teacher
        [HttpGet]
        public async Task<IActionResult> GetTeachers()
        {
            var teachers = await _context.Teachers
                .Include(t => t.User)
                .Include(t => t.Courses)
                .ToListAsync();
            return Ok(teachers);
        }

        // GET: api/teacher/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTeacher(int id)
        {
            var teacher = await _context.Teachers
                .Include(t => t.User)
                .Include(t => t.Courses)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (teacher == null)
                return NotFound("Teacher not found");

            return Ok(teacher);
        }

        // POST: api/teacher/register
        [HttpPost("register")]
        public async Task<IActionResult> RegisterTeacher([FromBody] RegisterTeacherDto registerDto)
        {
            // Проверяем, существует ли пользователь
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Login == registerDto.Login);

            if (existingUser != null)
                return BadRequest("User with this login already exists");

            // Создаем пользователя
            var user = new User
            {
                Login = registerDto.Login,
                Password = AuthUtils.HashPassword(registerDto.Password),
                Role = "Teacher",
                FullName = $"{registerDto.LastName} {registerDto.FirstName} {registerDto.MiddleName}".Trim(),
                Email = registerDto.Email,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Создаем преподавателя
            var teacher = new Teacher
            {
                UserId = user.Id,
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                MiddleName = registerDto.MiddleName,
                Specialization = registerDto.Specialization,
                PhoneNumber = registerDto.PhoneNumber,
                Email = registerDto.Email
            };

            _context.Teachers.Add(teacher);
            await _context.SaveChangesAsync();

            return Ok(new { TeacherId = teacher.Id, Message = "Teacher registered successfully" });
        }

        // PUT: api/teacher/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTeacher(int id, [FromBody] UpdateTeacherDto updateDto)
        {
            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (teacher == null)
                return NotFound("Teacher not found");

            teacher.FirstName = updateDto.FirstName;
            teacher.LastName = updateDto.LastName;
            teacher.MiddleName = updateDto.MiddleName;
            teacher.Specialization = updateDto.Specialization;
            teacher.PhoneNumber = updateDto.PhoneNumber;
            teacher.Email = updateDto.Email;
            teacher.User.FullName = $"{updateDto.LastName} {updateDto.FirstName} {updateDto.MiddleName}";
            teacher.User.Email = updateDto.Email;

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Teacher updated successfully" });
        }

        // DELETE: api/teacher/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTeacher(int id)
        {
            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (teacher == null)
                return NotFound("Teacher not found");

            _context.Teachers.Remove(teacher);
            _context.Users.Remove(teacher.User);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Teacher deleted successfully" });
        }

        // GET: api/teacher/{id}/courses
        [HttpGet("{id}/courses")]
        public async Task<IActionResult> GetTeacherCourses(int id)
        {
            var courses = await _context.Courses
                .Where(c => c.TeacherId == id)
                .Include(c => c.Students)
                .ToListAsync();

            return Ok(courses);
        }
    }
}