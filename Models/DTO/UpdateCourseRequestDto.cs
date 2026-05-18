namespace WebAPI.Models.DTO
{
    public class UpdateCourseRequestDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
    }
}