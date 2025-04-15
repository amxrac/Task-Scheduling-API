using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace TaskSchedulingApi.Models
{
    public class AppUser : IdentityUser
    {
        [MaxLength(100)]
        public required string Name { get; set; }
    }
}
