using System.ComponentModel.DataAnnotations;

namespace TaskSchedulingApi.DTOs
{
    public class AdminRegisterDTO
    {
        public required string Name { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public required string Email { get; set; }

        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one digit.")]
        public required string Password { get; set; }
        public required string Role { get; set; }
    }
}
