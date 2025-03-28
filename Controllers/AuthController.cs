using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Task_Scheduling_API.Data;
using Task_Scheduling_API.Models;
using Task_Scheduling_API.DTOs;
using Task_Scheduling_API.Services;

namespace Task_Scheduling_API.Controllers
{
    [ApiController]
    [Route("api/[Controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ILogger<AuthController> _logger;
        private readonly IEmailService _emailService;

        public AuthController(AppDbContext context, UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ILogger<AuthController> logger, IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDTO model)
        {
            _logger.LogInformation("Registration attempt for email: {Email}", model.Email);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid registration model state for {Email}", model.Email);
                return BadRequest(new { message = "An error occurred during registration", error = "Please try again later." });
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

            try
            {
                await VerifyEmail(user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send verification email to {Email}. {ex}", user.Email, ex);
            }
            await _userManager.AddToRoleAsync(user, "User");
            _logger.LogInformation("User registered successfully: {UserId}, {Email}", user.Id, user.Email);
            return StatusCode(StatusCodes.Status201Created, new { 
                message = "Account registered successfully. Please verify your email before logging in.", 
                email = user.Email, role = "User",
            });


        }

        private async Task VerifyEmail(string email)
        {
            _logger.LogInformation("Attempting verification for {Email}", email);
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                _logger.LogWarning("Email does not exist {Email}", email);
                throw new ArgumentException("User not found");
            }

            if (user.EmailConfirmed)
            {
                _logger.LogInformation("Email {Email} has already been confirmed.", email);
                return;
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var verificationLink = $"{Request.Scheme}://{Request.Host}/api/auth/confirm-email?email={email}&token={Uri.EscapeDataString(token)}";

            var subject = "Verify Your Email";
            var body = $"Click the following link to verify your email. This link is valid for 12 hours: <a href='{verificationLink}'>Verify Email</a>";

            await _emailService.SendEmailAsync(email, subject, body);
            _logger.LogInformation("Verification email sent to {Email}", email);
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string email, string token)
        {
            _logger.LogInformation("Email confirmation attempt for {Email}", email);

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Email or token missing");
                return BadRequest(new { message = "Email or token is missing" });
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                _logger.LogWarning("Email confirmation for invalid user {Email}", email);
                return BadRequest(new { message = "Invalid email." });
            }

            if (user.EmailConfirmed)
            {
                _logger.LogWarning("Email confirmation attempted, but email already verified for user {Email}", email);
                return BadRequest(new { message = "Email already verified. Please login to continue." });
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);

            if (!result.Succeeded)
            {
                _logger.LogError("Email confirmation failed. {UserId}, {Email}, {Errors}",
                    user.Id, email, string.Join(", ", result.Errors.Select(e => e.Description)));
                return BadRequest(new { message = "Email confirmation failed. Invalid token or email" });
            }

            await _userManager.UpdateAsync(user);
            await _userManager.UpdateSecurityStampAsync(user);

            _logger.LogInformation("Email verified successfully: {UserId}, {Email}", user.Id, email);
            return Ok(new { message = "Email verified successfully. Please login to continue." });
        }

    }
}
