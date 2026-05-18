namespace WebAPI.Models.DTO
{
    public class StudentProgressAnalyticsDto
    {
        public int StudentId { get; set; }
        public int ModuleId { get; set; }
        public string ModuleName { get; set; }
        public int CompletedModules { get; set; }
        public int TotalModules { get; set; }
        public double? Grade { get; set; }
        public string? Comment { get; set; }
    }
}