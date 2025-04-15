using System.ComponentModel.DataAnnotations;

namespace TaskSchedulingApi.DTOs
{
    public class LoginDTO
    {
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }

        [DataType(DataType.Password)]
        public required string Password { get; set; }
    }
}
