using System;
using System.Collections.Generic;

namespace WebAPI.Models.Students
{
    public class StudentProgress
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public Student Student { get; set; }

        public int ModuleId { get; set; }
        public string ModuleName { get; set; }
        public int CompletedModules { get; set; }
        public int TotalModules { get; set; }
        public double PercentageComplete { get; set; }
        public double AverageGrade { get; set; }

        public DateTime? LastUpdated { get; set; }
        public string Status { get; set; }
        public string? Description { get; set; }

        public List<GradeRecord> Grades { get; set; }
    }
}