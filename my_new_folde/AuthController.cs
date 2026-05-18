using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WebAPI.DbContexts;
using WebAPI.Models.Courses;
using WebAPI.Models.DTO;
using WebAPI.Models.Students;
using WebAPI.Models.Teachers;
using WebAPI.Models.Users;
using WebAPI.Utils; 

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationContext _context;

        public AuthController(ApplicationContext context)
        {
            _context = context;
        }

        // Регистрация студента
        [HttpPost("/register/student")]
        public async Task<IActionResult> RegisterStudent([FromBody] RegisterStudentDto registerDto)
        {
            // Проверяем, существует ли пользователь с таким логином
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Login == registerDto.Login);
            if (existingUser != null)
                return BadRequest(new { error = "User with this login already exists" });

            // Проверяем, существует ли пользователь с такой почтой
            var existingEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == registerDto.Email);
            if (existingEmail != null)
                return BadRequest(new { error = "User with this email already exists" });

            // Создаем пользователя
            var newUser = new User
            {
                Login = registerDto.Login,
                Password = AuthUtils.HashPassword(registerDto.Password),
                Role = "Student",
                FullName = $"{registerDto.LastName} {registerDto.FirstName} {registerDto.MiddleName}".Trim(),
                Email = registerDto.Email,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Создаем студента
            var student = new Student
            {
                UserId = newUser.Id,
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                MiddleName = registerDto.MiddleName,
                Email = registerDto.Email,
                PhoneNumber = registerDto.PhoneNumber,
                BirthDate = registerDto.BirthDate,
                CourseId = registerDto.CourseId,  // <-- ДОБАВИТЬ ЭТУ СТРОКУ
                StudentProgress = new List<StudentProgress>()
            };
            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                StudentId = student.Id,
                Message = "Student registered successfully",
                Login = newUser.Login,
                Role = newUser.Role
            });
        }

        // Регистрация преподавателя (только для админа)
        [HttpPost("/register/teacher")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RegisterTeacher([FromBody] RegisterTeacherDto registerDto)
        {
            // Проверяем, существует ли пользователь с таким логином
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Login == registerDto.Login);
            if (existingUser != null)
                return BadRequest(new { error = "User with this login already exists" });

            // Проверяем, существует ли пользователь с такой почтой
            var existingEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == registerDto.Email);
            if (existingEmail != null)
                return BadRequest(new { error = "User with this email already exists" });

            // Создаем пользователя
            var newUser = new User
            {
                Login = registerDto.Login,
                Password = AuthUtils.HashPassword(registerDto.Password),
                Role = "Teacher",
                FullName = $"{registerDto.LastName} {registerDto.FirstName} {registerDto.MiddleName}".Trim(),
                Email = registerDto.Email,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Создаем преподавателя
            var teacher = new Teacher
            {
                UserId = newUser.Id,
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                MiddleName = registerDto.MiddleName,
                Specialization = registerDto.Specialization,
                PhoneNumber = registerDto.PhoneNumber,
                Email = registerDto.Email,
                Courses = new List<Course>()
            };

            _context.Teachers.Add(teacher);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                TeacherId = teacher.Id,
                Message = "Teacher registered successfully",
                Login = newUser.Login,
                Role = newUser.Role
            });
        }

        [HttpPost("/register/admin")] 
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RegisterAdmin([FromBody] RegisterAdminDto registerDto)
        {
            // Проверяем, существует ли пользователь с таким логином
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Login == registerDto.Login);
            if (existingUser != null)
                return BadRequest(new { error = "User with this login already exists" });

            // Проверяем, существует ли пользователь с такой почтой
            var existingEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == registerDto.Email);
            if (existingEmail != null)
                return BadRequest(new { error = "User with this email already exists" });

            // Создаем пользователя-администратора
            var newUser = new User
            {
                Login = registerDto.Login,
                Password = AuthUtils.HashPassword(registerDto.Password),
                Role = "Admin",
                FullName = registerDto.FullName,
                Email = registerDto.Email,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                UserId = newUser.Id,
                Message = "Admin registered successfully",
                Login = newUser.Login,
                Role = newUser.Role
            });
        }


        // Логин (общий для всех ролей)
        [HttpPost("/login")]
        public IActionResult Login([FromBody] LoginRequestDto loginRequest)
        {
            var identity = GetIdentity(loginRequest.Login, loginRequest.Password);
            if (identity == null)
            {
                return BadRequest(new { errorText = "Invalid username or password!" });
            }

            var now = DateTime.UtcNow;

            var jwt = new JwtSecurityToken(
                issuer: AuthOptions.ISSUER,
                audience: AuthOptions.AUDIENCE,
                notBefore: now,
                claims: identity.Claims,
                expires: now.Add(TimeSpan.FromMinutes(AuthOptions.LIFETIME)),
                signingCredentials: new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256));

            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

            var response = new
            {
                access_token = encodedJwt,
                username = identity.Name,
                role = identity.Claims.FirstOrDefault(c => c.Type.Contains("role"))?.Value
            };

            return Ok(response);
        }

        private ClaimsIdentity GetIdentity(string username, string password)
        {
            var user = _context.Users.FirstOrDefault(u => u.Login == username);
            if (user == null || !AuthUtils.VerifyPassword(password, user.Password))
            {
                return null;
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimsIdentity.DefaultNameClaimType, user.Login),
                new Claim(ClaimsIdentity.DefaultRoleClaimType, user.Role),
                new Claim("UserId", user.Id.ToString()),
                new Claim("Email", user.Email ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, "Token",
                ClaimsIdentity.DefaultNameClaimType,
                ClaimsIdentity.DefaultRoleClaimType);

            return claimsIdentity;
        }
    }
}