namespace WebAPI.Models.DTO
{
    public class RegisterAdminDto
    {
        public string Login { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
    }
}