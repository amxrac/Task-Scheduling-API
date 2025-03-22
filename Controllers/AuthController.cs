using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Task_Scheduling_API.Data;
using Task_Scheduling_API.Models;
using Task_Scheduling_API.DTOs;

namespace Task_Scheduling_API.Controllers
{
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AppDbContext context, UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ILogger<AuthController> logger)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDTO model)
        {
            _logger.LogInformation("Registration attempt for email: {Email}", model.Email);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid registration model state for {Email}", model.Email);
                return StatusCode(StatusCodes.Status400BadRequest, new {message = "An error occured during registration", error = "Please try again later"});
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("Registration attempt with existing email {Email}", model.Email);
                return Conflict(new { message = "Email already registered" });
            }

            AppUser user = new()
            {
                Name = model.Name,
                Email = model.Email,
                UserName = model.Email,
            };

            var result = await _userManager.CreateAsync(user, model.Password!);
            if (!result.Succeeded)
            {
                _logger.LogWarning("User registration failed for {Email}: {Errors}", model.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                return BadRequest(new { message = "User registration failed.", errors = result.Errors.Select(e => e.Description) });

            }

            await _userManager.AddToRoleAsync(user, "User");
            _logger.LogInformation("User registered successfully: {UserId}, {Email}", user.Id, user.Email);
            return StatusCode(StatusCodes.Status201Created, new { message = "User created successfully.", email = user.Email, role = "User" });
        }
    }
}
