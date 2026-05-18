using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebAPI.DbContexts;
using WebAPI.Models.DTO;
using WebAPI.Models.Students;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("students")]
    [Authorize]
    public class StudentsController : ControllerBase
    {
        private ApplicationContext _context;

        public StudentsController()
        {
            _context = new ApplicationContext();
        }

        // GET: students
        [HttpGet]
        [Authorize(Roles = "Admin, Teacher")]
        public IActionResult GetStudents()
        {
            var currentUserRole = User.Claims.FirstOrDefault(c => c.Type == ClaimsIdentity.DefaultRoleClaimType)?.Value;
            var currentUserLogin = User.Identity.Name;

            var query = _context.Students
                .Include(s => s.StudentProgress)
                .ThenInclude(sp => sp.Grades)
                .Include(s => s.Course)
                .AsQueryable();

            // Если учитель - показывает только своих студентов
            if (currentUserRole == "Teacher")
            {
                var teacher = _context.Teachers
                    .FirstOrDefault(t => t.User.Login == currentUserLogin);

                if (teacher != null)
                {
                    var teacherCourses = _context.Courses
                        .Where(c => c.TeacherId == teacher.Id)
                        .Select(c => c.Id)
                        .ToList();

                    query = query.Where(s => teacherCourses.Contains(s.CourseId ?? 0));
                }
            }

            return Ok(query.ToList());
        }

        // GET: students/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin, Teacher")]
        public IActionResult GetStudentById(int id)
        {
            var currentUserRole = User.Claims.FirstOrDefault(c => c.Type == ClaimsIdentity.DefaultRoleClaimType)?.Value;
            var currentUserLogin = User.Identity.Name;

            var student = _context.Students
                .Include(s => s.StudentProgress)
                .ThenInclude(sp => sp.Grades)
                .Include(s => s.Course)
                .FirstOrDefault(s => s.Id == id);

            if (student == null)
                return BadRequest("Student not found");

            // Если учитель - проверяем, что студент из его курса
            if (currentUserRole == "Teacher")
            {
                var teacher = _context.Teachers
                    .FirstOrDefault(t => t.User.Login == currentUserLogin);

                if (teacher != null)
                {
                    var teacherCourses = _context.Courses
                        .Where(c => c.TeacherId == teacher.Id)
                        .Select(c => c.Id)
                        .ToList();

                    if (!teacherCourses.Contains(student.CourseId ?? 0))
                        return Forbid("You can only view students from your courses");
                }
            }

            return Ok(student);
        }

        // GET: students/{id}/analytics
        [HttpGet("{id}/analytics")]
        [Authorize(Roles = "Admin, Teacher, Student")]
        public IActionResult GetStudentAnalytics(int id)
        {
            var currentUserRole = User.Claims.FirstOrDefault(c => c.Type == ClaimsIdentity.DefaultRoleClaimType)?.Value;
            var currentUserLogin = User.Identity.Name;

            // Если студент - проверяем, что запрашивает свои данные
            if (currentUserRole == "Student")
            {
                var student = _context.Students
                    .FirstOrDefault(s => s.User.Login == currentUserLogin);

                if (student == null || student.Id != id)
                    return Forbid("You can only view your own analytics");
            }

            // Если учитель - проверяем, что студент из его курса
            if (currentUserRole == "Teacher")
            {
                var teacher = _context.Teachers
                    .FirstOrDefault(t => t.User.Login == currentUserLogin);

                if (teacher != null)
                {
                    var teacherCourses = _context.Courses
                        .Where(c => c.TeacherId == teacher.Id)
                        .Select(c => c.Id)
                        .ToList();

                    var student = _context.Students.FirstOrDefault(s => s.Id == id);
                    if (student != null && !teacherCourses.Contains(student.CourseId ?? 0))
                        return Forbid("You can only view analytics of students from your courses");
                }
            }

            var studentData = _context.Students
                .Include(s => s.StudentProgress)
                .ThenInclude(sp => sp.Grades)
                .FirstOrDefault(s => s.Id == id);

            if (studentData == null)
                return BadRequest("Student not found");

            var analytics = new
            {
                StudentId = studentData.Id,
                StudentName = $"{studentData.LastName} {studentData.FirstName} {studentData.MiddleName}",
                Email = studentData.Email,
                OverallAverageGrade = studentData.StudentProgress != null && studentData.StudentProgress.Any()
                    ? studentData.StudentProgress
                        .Where(sp => sp.Grades != null && sp.Grades.Any())
                        .SelectMany(sp => sp.Grades)
                        .Average(g => g.Grade)
                    : 0,
                TotalModulesCompleted = studentData.StudentProgress?.Sum(sp => sp.CompletedModules) ?? 0,
                TotalModulesOverall = studentData.StudentProgress?.Sum(sp => sp.TotalModules) ?? 0,
                OverallCompletionRate = studentData.StudentProgress != null && studentData.StudentProgress.Any()
                    ? studentData.StudentProgress.Average(sp => sp.PercentageComplete)
                    : 0,
                RiskLevel = GetStudentRiskLevel(studentData),
                ModuleProgress = studentData.StudentProgress?.Select(sp => new
                {
                    sp.ModuleId,
                    sp.ModuleName,
                    sp.CompletedModules,
                    sp.TotalModules,
                    sp.PercentageComplete,
                    sp.AverageGrade,
                    sp.Status,
                    sp.LastUpdated,
                    RecentGrades = sp.Grades?
                        .OrderByDescending(g => g.Date)
                        .Take(3)
                        .Select(g => new { g.Grade, g.Date, g.Comment })
                }),
                PerformanceTrend = GetPerformanceTrend(studentData),
                MonthlyProgress = GetMonthlyProgress(studentData),
                Recommendations = GenerateRecommendations(studentData),
                ComparisonWithCourseAverage = GetCourseComparison(studentData)
            };

            return Ok(analytics);
        }

        // GET: students/analytics/overview
        [HttpGet("analytics/overview")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetOverallAnalytics()
        {
            var students = _context.Students
                .Include(s => s.StudentProgress)
                .ThenInclude(sp => sp.Grades)
                .ToList();

            var studentsWithProgress = students.Where(s => s.StudentProgress != null && s.StudentProgress.Any()).ToList();

            double averageOverallGrade = 0;
            if (studentsWithProgress.Any())
            {
                var allGrades = new List<double>();
                foreach (var student in studentsWithProgress)
                {
                    if (student.StudentProgress != null)
                    {
                        foreach (var progress in student.StudentProgress)
                        {
                            if (progress.Grades != null && progress.Grades.Any())
                            {
                                foreach (var grade in progress.Grades)
                                {
                                    allGrades.Add(grade.Grade);
                                }
                            }
                        }
                    }
                }
                averageOverallGrade = allGrades.Any() ? allGrades.Average() : 0;
            }

            var overview = new
            {
                TotalStudents = students.Count,
                StudentsWithProgress = studentsWithProgress.Count,
                StudentsWithoutProgress = students.Count - studentsWithProgress.Count,
                AverageOverallGrade = averageOverallGrade,
                AverageCompletionRate = studentsWithProgress.Any()
                    ? studentsWithProgress
                        .SelectMany(s => s.StudentProgress)
                        .Average(sp => sp.PercentageComplete)
                    : 0,
                RiskLevelDistribution = new
                {
                    AtRisk = students.Count(s => GetStudentRiskLevel(s) == "At Risk"),
                    Poor = students.Count(s => GetStudentRiskLevel(s) == "Poor"),
                    Average = students.Count(s => GetStudentRiskLevel(s) == "Average"),
                    Good = students.Count(s => GetStudentRiskLevel(s) == "Good"),
                    Excellent = students.Count(s => GetStudentRiskLevel(s) == "Excellent")
                },
                TopPerformers = students
                    .OrderByDescending(s => GetStudentAverageGrade(s))
                    .Take(10)
                    .Select((s, index) => new
                    {
                        Rank = index + 1,
                        StudentId = s.Id,
                        Name = $"{s.LastName} {s.FirstName}",
                        AverageGrade = GetStudentAverageGrade(s),
                        CompletionRate = s.StudentProgress != null && s.StudentProgress.Any()
                            ? s.StudentProgress.Average(sp => sp.PercentageComplete)
                            : 0
                    }),
                StudentsAtRisk = students
                    .Where(s => GetStudentRiskLevel(s) == "At Risk")
                    .Select(s => new
                    {
                        StudentId = s.Id,
                        Name = $"{s.LastName} {s.FirstName}",
                        AverageGrade = GetStudentAverageGrade(s),
                        CompletionRate = s.StudentProgress != null && s.StudentProgress.Any()
                            ? s.StudentProgress.Average(sp => sp.PercentageComplete)
                            : 0
                    }),
                CourseDistribution = _context.Courses
                    .Include(c => c.Students)
                    .ThenInclude(s => s.StudentProgress)
                    .ThenInclude(sp => sp.Grades)
                    .Select(c => new
                    {
                        CourseId = c.Id,
                        CourseName = c.Name,
                        StudentCount = c.Students != null ? c.Students.Count : 0,
                        AverageGradePerCourse = CalculateCourseAverageGrade(c.Students)
                    }),
                GradeDistribution = GetGradeDistribution(students),
                MonthlyTrend = GetMonthlyTrend(students)
            };

            return Ok(overview);
        }

        // POST: students/studentprogress/analytics
        [HttpPost("studentprogress/analytics")]
        [Authorize(Roles = "Admin, Teacher")]
        public IActionResult UpdateProgressWithAnalytics([FromBody] StudentProgressAnalyticsDto progressDto)
        {
            var currentUserRole = User.Claims.FirstOrDefault(c => c.Type == ClaimsIdentity.DefaultRoleClaimType)?.Value;
            var currentUserLogin = User.Identity.Name;

            // Если учитель - проверяем, что студент из его курса
            if (currentUserRole == "Teacher")
            {
                var teacher = _context.Teachers
                    .FirstOrDefault(t => t.User.Login == currentUserLogin);

                if (teacher != null)
                {
                    var students = _context.Students
                        .Include(s => s.Course)
                        .FirstOrDefault(s => s.Id == progressDto.StudentId);

                    if (students == null || students.Course?.TeacherId != teacher.Id)
                        return Forbid("You can only update progress of students from your courses");
                }
            }

            var student = _context.Students
                .Include(s => s.StudentProgress)
                .ThenInclude(sp => sp.Grades)
                .FirstOrDefault(s => s.Id == progressDto.StudentId);

            if (student == null)
                return BadRequest("Student not found");

            var progress = student.StudentProgress?
                .FirstOrDefault(sp => sp.ModuleId == progressDto.ModuleId);

            if (progress == null)
            {
                progress = new StudentProgress
                {
                    StudentId = progressDto.StudentId,
                    ModuleId = progressDto.ModuleId,
                    ModuleName = progressDto.ModuleName,
                    TotalModules = progressDto.TotalModules,
                    CompletedModules = 0,
                    PercentageComplete = 0,
                    AverageGrade = 0,
                    Status = "Average",
                    LastUpdated = DateTime.UtcNow
                };

                if (student.StudentProgress == null)
                    student.StudentProgress = new List<StudentProgress>();

                student.StudentProgress.Add(progress);
            }

            progress.CompletedModules = progressDto.CompletedModules;
            progress.TotalModules = progressDto.TotalModules;
            progress.PercentageComplete = progressDto.TotalModules > 0
                ? (double)progressDto.CompletedModules / progressDto.TotalModules * 100
                : 0;

            if (progressDto.Grade.HasValue)
            {
                if (progress.Grades == null)
                    progress.Grades = new List<GradeRecord>();

                progress.Grades.Add(new GradeRecord
                {
                    Subject = progressDto.ModuleName,
                    Grade = progressDto.Grade.Value,
                    Date = DateTime.UtcNow,
                    Comment = progressDto.Comment
                });

                progress.AverageGrade = progress.Grades.Average(g => g.Grade);
            }

            progress.Status = DetermineStatus(progress.PercentageComplete, progress.AverageGrade);
            progress.LastUpdated = DateTime.UtcNow;

            _context.SaveChanges();

            return Ok(new
            {
                Id = progress.Id,
                Status = progress.Status,
                AverageGrade = progress.AverageGrade,
                PercentageComplete = progress.PercentageComplete,
                TotalModulesCompleted = progress.CompletedModules,
                Message = $"Progress updated successfully. Student is currently {progress.Status}"
            });
        }

        // GET: students/analytics/export/csv
        [HttpGet("analytics/export/csv")]
        [Authorize(Roles = "Admin")]
        public IActionResult ExportAnalyticsToCsv()
        {
            var students = _context.Students
                .Include(s => s.StudentProgress)
                .ThenInclude(sp => sp.Grades)
                .ToList();

            var csvLines = new List<string>
            {
                "StudentId,LastName,FirstName,MiddleName,Email,AverageGrade,CompletionRate,RiskLevel,TotalModulesCompleted,TotalModulesOverall,Recommendations"
            };

            foreach (var student in students)
            {
                var avgGrade = GetStudentAverageGrade(student);
                var completionRate = student.StudentProgress?.Average(sp => sp.PercentageComplete) ?? 0;
                var totalCompleted = student.StudentProgress?.Sum(sp => sp.CompletedModules) ?? 0;
                var totalOverall = student.StudentProgress?.Sum(sp => sp.TotalModules) ?? 0;
                var riskLevel = GetStudentRiskLevel(student);
                var recommendations = string.Join("; ", GenerateRecommendations(student));

                var safeRecommendations = $"\"{recommendations}\"";

                csvLines.Add($"{student.Id},{student.LastName},{student.FirstName},{student.MiddleName},{student.Email},{avgGrade:F2},{completionRate:F2},{riskLevel},{totalCompleted},{totalOverall},{safeRecommendations}");
            }

            var csvBytes = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", csvLines));
            var fileName = $"analytics_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(csvBytes, "text/csv; charset=utf-8", fileName);
        }

        // GET: students/analytics/export/json
        [HttpGet("analytics/export/json")]
        [Authorize(Roles = "Admin")]
        public IActionResult ExportAnalyticsToJson()
        {
            var students = _context.Students
                .Include(s => s.StudentProgress)
                .ThenInclude(sp => sp.Grades)
                .ToList();

            var exportData = students.Select(s => new
            {
                s.Id,
                s.FirstName,
                s.LastName,
                s.MiddleName,
                s.Email,
                s.PhoneNumber,
                s.BirthDate,
                Analytics = new
                {
                    AverageGrade = GetStudentAverageGrade(s),
                    CompletionRate = s.StudentProgress?.Average(sp => sp.PercentageComplete) ?? 0,
                    RiskLevel = GetStudentRiskLevel(s),
                    TotalModulesCompleted = s.StudentProgress?.Sum(sp => sp.CompletedModules) ?? 0,
                    Recommendations = GenerateRecommendations(s)
                },
                ProgressDetails = s.StudentProgress?.Select(sp => new
                {
                    sp.ModuleName,
                    sp.CompletedModules,
                    sp.TotalModules,
                    sp.PercentageComplete,
                    sp.AverageGrade,
                    sp.Status,
                    sp.LastUpdated,
                    Grades = sp.Grades?.Select(g => new { g.Grade, g.Date, g.Comment })
                })
            });

            return Ok(exportData);
        }

        // GET: students/analytics/student/{id}/grades
        [HttpGet("analytics/student/{id}/grades")]
        [Authorize(Roles = "Admin, Teacher, Student")]
        public IActionResult GetStudentGrades(int id)
        {
            var currentUserRole = User.Claims.FirstOrDefault(c => c.Type == ClaimsIdentity.DefaultRoleClaimType)?.Value;
            var currentUserLogin = User.Identity.Name;

            // Если студент - проверяем, что запрашивает свои оценки
            if (currentUserRole == "Student")
            {
                var student = _context.Students
                    .FirstOrDefault(s => s.User.Login == currentUserLogin);

                if (student == null || student.Id != id)
                    return Forbid("You can only view your own grades");
            }

            var studentData = _context.Students
                .Include(s => s.StudentProgress)
                .ThenInclude(sp => sp.Grades)
                .FirstOrDefault(s => s.Id == id);

            if (studentData == null)
                return BadRequest("Student not found");

            var allGrades = studentData.StudentProgress?
                .SelectMany(sp => sp.Grades ?? new List<GradeRecord>())
                .OrderByDescending(g => g.Date)
                .Select(g => new
                {
                    g.Subject,
                    g.Grade,
                    g.Date,
                    g.Comment
                })
                .ToList();

            var statistics = new
            {
                TotalGrades = allGrades?.Count ?? 0,
                AverageGrade = allGrades?.Average(g => g.Grade) ?? 0,
                HighestGrade = allGrades?.Max(g => g.Grade) ?? 0,
                LowestGrade = allGrades?.Min(g => g.Grade) ?? 0,
                GradesBySubject = allGrades?
                    .GroupBy(g => g.Subject)
                    .Select(g => new
                    {
                        Subject = g.Key,
                        Average = g.Average(gr => gr.Grade),
                        Count = g.Count(),
                        Highest = g.Max(gr => gr.Grade),
                        Lowest = g.Min(gr => gr.Grade)
                    }),
                RecentGrades = allGrades?.Take(10)
            };

            return Ok(statistics);
        }

        // POST: students
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult CreateStudent([FromBody] StudentRequestDto studentDto)
        {
            var course = _context.Courses.Include(s => s.Students)
                .FirstOrDefault(c => c.Id == studentDto.CourseId);

            if (course == null)
                return BadRequest("Course not found");

            var _student = _context.Students.Add(new Student
            {
                BirthDate = studentDto.BirthDate,
                Email = studentDto.Email,
                FirstName = studentDto.FirstName,
                LastName = studentDto.LastName,
                MiddleName = studentDto.MiddleName,
                PhoneNumber = studentDto.PhoneNumber,
                CourseId = studentDto.CourseId,
                StudentProgress = new List<StudentProgress>()
            });

            _context.SaveChanges();

            if (course.Students == null)
            {
                course.Students = new List<Student>();  // <-- ИСПРАВЛЕНО
            }

            course.Students.Add(_student.Entity);
            _context.SaveChanges();

            return Ok(new
            {
                StudentId = _student.Entity.Id,
                Message = "Student created successfully"
            });
        }
        // PUT: students
        [HttpPut]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateStudent([FromBody] Student student)
        {
            var _student = _context.Students.FirstOrDefault(s => s.Id == student.Id);
            if (_student != null)
            {
                _student.BirthDate = student.BirthDate;
                _student.Email = student.Email;
                _student.FirstName = student.FirstName;
                _student.LastName = student.LastName;
                _student.MiddleName = student.MiddleName;
                _student.PhoneNumber = student.PhoneNumber;
                _context.SaveChanges();
                return Ok(new { Message = "Student updated successfully" });
            }
            return BadRequest("Student not found");
        }

        // DELETE: students/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteStudent(int id)
        {
            var _student = _context.Students
                .Include(s => s.StudentProgress)
                .ThenInclude(sp => sp.Grades)
                .FirstOrDefault(s => s.Id == id);

            if (_student != null)
            {
                _context.Students.Remove(_student);
                _context.SaveChanges();
                return Ok(new { Message = "Student deleted successfully" });
            }
            return BadRequest("Student not found");
        }

        // POST: students/studentprogress
        [HttpPost("studentprogress")]
        [Authorize(Roles = "Admin, Teacher")]
        public IActionResult AddStudentProgress([FromBody] StudentProgressRequestDto studentProgress)
        {
            var _student = _context.Students.Include(s => s.StudentProgress)
                .FirstOrDefault(s => s.Id == studentProgress.StudentId);

            if (_student != null)
            {
                if (_student.StudentProgress == null)
                {
                    _student.StudentProgress = new List<StudentProgress>();
                }

                var newProgress = new StudentProgress
                {
                    StudentId = studentProgress.StudentId,
                    Description = studentProgress.Description,
                    CompletedModules = studentProgress.CompletedModules,
                    TotalModules = studentProgress.TotalModules ?? 100,
                    ModuleId = studentProgress.ModuleId ?? 0,
                    ModuleName = studentProgress.ModuleName ?? "General",
                    PercentageComplete = studentProgress.TotalModules.HasValue && studentProgress.TotalModules.Value > 0
                        ? (double)studentProgress.CompletedModules / studentProgress.TotalModules.Value * 100
                        : 0,
                    AverageGrade = 0,
                    Status = "Average",
                    LastUpdated = DateTime.UtcNow,
                    Grades = new List<GradeRecord>()
                };

                _student.StudentProgress.Add(newProgress);
                _context.SaveChanges();

                return Ok(new { Id = newProgress.Id, Message = "Progress added successfully" });
            }
            return BadRequest("Student not found");
        }

        // DELETE: students/studentprogress
        [HttpDelete("studentprogress")]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteStudentProgress(int id)
        {
            var _studentProgress = _context.StudentProgresses
                .Include(sp => sp.Grades)
                .FirstOrDefault(sp => sp.Id == id);

            if (_studentProgress != null)
            {
                _context.StudentProgresses.Remove(_studentProgress);
                _context.SaveChanges();
                return Ok(new { Message = "Student progress deleted successfully" });
            }
            return BadRequest("Student progress entry not found");
        }

        // ============= ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДЛЯ АНАЛИТИКИ =============

        private string DetermineStatus(double percentageComplete, double averageGrade)
        {
            if (percentageComplete >= 90 && averageGrade >= 4.5)
                return "Excellent";
            if (percentageComplete >= 75 && averageGrade >= 3.5)
                return "Good";
            if (percentageComplete >= 60 && averageGrade >= 3.0)
                return "Average";
            if (percentageComplete >= 40 || averageGrade >= 2.0)
                return "Poor";
            return "At Risk";
        }

        private double CalculateCourseAverageGrade(ICollection<Student> students)
        {
            if (students == null || !students.Any())
                return 0;

            var allGrades = new List<double>();
            foreach (var student in students)
            {
                if (student.StudentProgress != null)
                {
                    foreach (var progress in student.StudentProgress)
                    {
                        if (progress.Grades != null && progress.Grades.Any())
                        {
                            foreach (var grade in progress.Grades)
                            {
                                allGrades.Add(grade.Grade);
                            }
                        }
                    }
                }
            }

            return allGrades.Any() ? allGrades.Average() : 0;
        }

        private string GetStudentRiskLevel(Student student)
        {
            if (student.StudentProgress == null || !student.StudentProgress.Any())
                return "No Data";

            var avgGrade = GetStudentAverageGrade(student);
            var completionRate = student.StudentProgress.Average(sp => sp.PercentageComplete);

            if (completionRate < 30 || avgGrade < 2.0)
                return "At Risk";
            if (completionRate < 50 || avgGrade < 3.0)
                return "Poor";
            if (completionRate < 70 || avgGrade < 3.5)
                return "Average";
            if (completionRate >= 90 && avgGrade >= 4.5)
                return "Excellent";
            if (completionRate >= 70 && avgGrade >= 3.5)
                return "Good";

            return "Average";
        }

        private object GenerateRecommendations(Student student)
        {
            var recommendations = new List<string>();

            if (student.StudentProgress == null || !student.StudentProgress.Any())
            {
                recommendations.Add("Необходимо начать отслеживание прогресса студента");
                recommendations.Add("Добавьте информацию о модулях и оценках");
                return recommendations;
            }

            var avgGrade = GetStudentAverageGrade(student);
            var completionRate = student.StudentProgress.Average(sp => sp.PercentageComplete);
            var totalModules = student.StudentProgress.Sum(sp => sp.TotalModules);
            var completedModules = student.StudentProgress.Sum(sp => sp.CompletedModules);

            if (completionRate < 50)
                recommendations.Add("Рекомендуется увеличить нагрузку и посещаемость занятий");

            if (avgGrade < 3.0)
                recommendations.Add("Требуются дополнительные консультации с преподавателями");

            if (completionRate < 70 && avgGrade < 3.5)
                recommendations.Add("Рекомендуется составить индивидуальный план обучения");

            if (completedModules < totalModules * 0.3)
                recommendations.Add("Студент значительно отстает от программы. Требуется срочное вмешательство");

            if (student.StudentProgress != null)
            {
                var weakSubjects = student.StudentProgress
                    .Where(sp => sp.AverageGrade < 3.0 && sp.AverageGrade > 0)
                    .Select(sp => sp.ModuleName);

                if (weakSubjects.Any())
                    recommendations.Add($"Обратить внимание на предметы: {string.Join(", ", weakSubjects)}");

                var excellentSubjects = student.StudentProgress
                    .Where(sp => sp.AverageGrade >= 4.5)
                    .Select(sp => sp.ModuleName);

                if (excellentSubjects.Any())
                    recommendations.Add($"Поощрить за успехи в предметах: {string.Join(", ", excellentSubjects)}");
            }

            var lastUpdate = student.StudentProgress.Max(sp => sp.LastUpdated);
            if (lastUpdate.HasValue && lastUpdate.Value < DateTime.UtcNow.AddMonths(-1))
                recommendations.Add("Давно не обновлялся прогресс. Требуется актуализация данных");

            if (!recommendations.Any())
                recommendations.Add("Продолжать обучение в текущем темпе, успеваемость хорошая");

            return recommendations;
        }

        private object GetPerformanceTrend(Student student)
        {
            if (student.StudentProgress == null || !student.StudentProgress.Any())
                return new { Trend = "No Data", Message = "Недостаточно данных для анализа тренда" };

            var allGrades = student.StudentProgress
                .Where(sp => sp.Grades != null && sp.Grades.Any())
                .SelectMany(sp => sp.Grades)
                .OrderBy(g => g.Date)
                .ToList();

            if (allGrades.Count < 3)
                return new { Trend = "Insufficient Data", Message = "Требуется минимум 3 оценки для анализа тренда" };

            var recentGrades = allGrades.TakeLast(5).ToList();
            var olderGrades = allGrades.Take(3).ToList();

            var recentAvg = recentGrades.Average(g => g.Grade);
            var olderAvg = olderGrades.Average(g => g.Grade);

            if (recentAvg > olderAvg + 0.5)
                return new { Trend = "Improving", Message = "Успеваемость улучшается", Improvement = recentAvg - olderAvg };
            if (recentAvg < olderAvg - 0.5)
                return new { Trend = "Declining", Message = "Успеваемость снижается", Decline = olderAvg - recentAvg };

            return new { Trend = "Stable", Message = "Успеваемость стабильна" };
        }

        private object GetMonthlyProgress(Student student)
        {
            if (student.StudentProgress == null || !student.StudentProgress.Any())
                return new List<object>();

            var monthlyData = student.StudentProgress
                .Where(sp => sp.Grades != null && sp.Grades.Any())
                .SelectMany(sp => sp.Grades)
                .GroupBy(g => new { g.Date.Year, g.Date.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                    AverageGrade = g.Average(gr => gr.Grade),
                    TotalGrades = g.Count()
                })
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();

            return monthlyData;
        }

        private object GetCourseComparison(Student student)
        {
            if (student.StudentProgress == null || !student.StudentProgress.Any())
                return new { Message = "Недостаточно данных для сравнения" };

            var studentAvg = GetStudentAverageGrade(student);

            var allStudents = _context.Students
                .Include(s => s.StudentProgress)
                .ThenInclude(sp => sp.Grades)
                .ToList();

            var courseAvg = allStudents
                .Where(s => s.StudentProgress != null && s.StudentProgress.Any())
                .SelectMany(s => s.StudentProgress)
                .Where(sp => sp.Grades != null && sp.Grades.Any())
                .SelectMany(sp => sp.Grades)
                .DefaultIfEmpty()
                .Average(g => g != null ? g.Grade : 0);

            var difference = studentAvg - courseAvg;
            var percentile = allStudents.Count(s => GetStudentAverageGrade(s) < studentAvg) / (double)allStudents.Count * 100;

            return new
            {
                StudentAverage = studentAvg,
                CourseAverage = courseAvg,
                Difference = difference,
                Percentile = percentile,
                Performance = difference > 0 ? "Above Average" : difference < 0 ? "Below Average" : "Average",
                Message = difference > 0
                    ? $"Студент показывает результаты выше среднего на {difference:F2} балла"
                    : difference < 0
                        ? $"Студент отстает от среднего показателя на {Math.Abs(difference):F2} балла"
                        : "Студент находится на среднем уровне"
            };
        }

        private double GetStudentAverageGrade(Student student)
        {
            if (student.StudentProgress == null || !student.StudentProgress.Any())
                return 0;

            var allGrades = new List<double>();
            foreach (var progress in student.StudentProgress)
            {
                if (progress.Grades != null && progress.Grades.Any())
                {
                    foreach (var grade in progress.Grades)
                    {
                        allGrades.Add(grade.Grade);
                    }
                }
            }

            return allGrades.Any() ? allGrades.Average() : 0;
        }

        private object GetGradeDistribution(List<Student> students)
        {
            var allGrades = new List<double>();
            foreach (var student in students)
            {
                if (student.StudentProgress != null)
                {
                    foreach (var progress in student.StudentProgress)
                    {
                        if (progress.Grades != null && progress.Grades.Any())
                        {
                            foreach (var grade in progress.Grades)
                            {
                                allGrades.Add(grade.Grade);
                            }
                        }
                    }
                }
            }

            return new
            {
                Excellent = allGrades.Count(g => g >= 4.5),
                Good = allGrades.Count(g => g >= 3.5 && g < 4.5),
                Satisfactory = allGrades.Count(g => g >= 3.0 && g < 3.5),
                Poor = allGrades.Count(g => g >= 2.0 && g < 3.0),
                Failing = allGrades.Count(g => g < 2.0),
                Total = allGrades.Count
            };
        }

        private object GetMonthlyTrend(List<Student> students)
        {
            var monthlyData = new Dictionary<string, List<double>>();

            foreach (var student in students)
            {
                if (student.StudentProgress != null)
                {
                    foreach (var progress in student.StudentProgress)
                    {
                        if (progress.Grades != null && progress.Grades.Any())
                        {
                            foreach (var grade in progress.Grades)
                            {
                                var monthKey = grade.Date.ToString("MMMM yyyy");
                                if (!monthlyData.ContainsKey(monthKey))
                                    monthlyData[monthKey] = new List<double>();
                                monthlyData[monthKey].Add(grade.Grade);
                            }
                        }
                    }
                }
            }

            var result = monthlyData
                .Select(m => new
                {
                    Month = m.Key,
                    AverageGrade = m.Value.Average(),
                    TotalGrades = m.Value.Count
                })
                .OrderBy(m => m.Month) 
                .TakeLast(6)
                .ToList();

            return result;
        }
    }
}