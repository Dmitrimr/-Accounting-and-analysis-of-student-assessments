using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.DbContexts;
using WebAPI.Models.Courses;

using WebAPI.Models.DTO;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class CoursesController : ControllerBase
    {
        private ApplicationContext _context;

        public CoursesController()
        {
            _context = new ApplicationContext();
        }

        [HttpGet]
                public IEnumerable<Course> GetCourses()
        {
            return _context.Courses
                .Include(c => c.Students).ThenInclude(e => e.StudentProgress)
                .ToList();
        }

        [HttpPost("course")]
        [Authorize(Roles = "Admin")]
        public IActionResult AddCourse([FromBody] AddCourseRequestDto addCourseDto)
        {
            var course = _context.Courses.Add(new Course { Name = addCourseDto.Name, Description = addCourseDto.Description});
            _context.SaveChanges();
            return Ok(course.Entity?.Id ?? 0);
        }

        [HttpPut("course")]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateCourse([FromBody] UpdateCourseRequestDto updateCourseDto)
        {
            var course = _context.Courses.FirstOrDefault(c => c.Id == updateCourseDto.Id);
            if (course != null)
            {
                course.Name = updateCourseDto.Name;
                course.Description = updateCourseDto.Description;
                _context.SaveChanges();
                return Ok();
            }
            return BadRequest("Course not found");
        }

        [HttpDelete("course")]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteCourse(int id)
        {
            var course = _context.Courses.FirstOrDefault(c => c.Id == id);
            if (course != null)
            {
                _context.Courses.Remove(course);
                _context.SaveChanges();
                return Ok();
            }
            return BadRequest("Course not found");
        }
    }
}