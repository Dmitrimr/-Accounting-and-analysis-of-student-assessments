namespace WebAPI.Models.DTO
{
    public class StudentProgressRequestDto
    {
        public int StudentId { get; set; }
        public int CompletedModules { get; set; } = 0;
        public string? Description { get; set; } = null;
        // Добавляем недостающие поля, но делаем их nullable для обратной совместимости
        public int? TotalModules { get; set; } = null;
        public int? ModuleId { get; set; } = null;
        public string? ModuleName { get; set; } = null;
    }
}