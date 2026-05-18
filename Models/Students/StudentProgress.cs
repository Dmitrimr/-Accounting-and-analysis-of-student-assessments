namespace WebAPI.Models.Students
{
    public class StudentProgress
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public Student Student { get; set; }

        public int ModuleId { get; set; }
        public string ModuleName { get; set; }        // Название модуля/предмета
        public int CompletedModules { get; set; }     // Пройдено модулей
        public int TotalModules { get; set; }         // Всего модулей
        public double PercentageComplete { get; set; } // Процент выполнения
        public double AverageGrade { get; set; }      // Средний балл

        // Аналитика
        public DateTime? LastUpdated { get; set; }    // Дата последнего обновления
        public string Status { get; set; }            // Статус успеваемости
        public string? Description { get; set; }      // Описание/заметки


        public List<GradeRecord> Grades { get; set; }
    }
}