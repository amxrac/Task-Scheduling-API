using System.ComponentModel.DataAnnotations;

namespace Task_Scheduling_API.DTOs
{
    public class LoginDTO
    {
        [Required]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
