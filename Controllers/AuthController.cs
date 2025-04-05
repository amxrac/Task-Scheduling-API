using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Task_Scheduling_API.Data;
using Task_Scheduling_API.Models;
using Task_Scheduling_API.DTOs;
using Task_Scheduling_API.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

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
        private readonly TokenGenerator _tokenGenerator;

        public AuthController(AppDbContext context, UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ILogger<AuthController> logger, IEmailService emailService, TokenGenerator tokenGenerator)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailService = emailService;
            _tokenGenerator = tokenGenerator;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO model)
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

            AppUser? user = null;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                user = new AppUser
                {
                    Name = model.Name,
                    Email = model.Email,
                    UserName = model.Email
                };

                var createResult = await _userManager.CreateAsync(user, model.Password);

                if (!createResult.Succeeded)
                    throw new Exception($"User creation failed: {string.Join(", ", createResult.Errors)}");

                await _userManager.AddToRoleAsync(user, "User");

                await VerifyEmail(user.Email);

                await transaction.CommitAsync();

                _logger.LogInformation("User account {Email} successfully registered at {Timestamp}", user.Email, DateTime.UtcNow);
                return StatusCode(StatusCodes.Status201Created, new
                {
                    message = "Account registered successfully. Verification email sent.",
                    name = user.Name,
                    email = user.Email,
                    role = "user"
                });
            }
            catch (ApplicationException ex) when (ex.InnerException is SmtpException)
            {
                _logger.LogError(ex, "Verification email failed to send for email {Email}", model.Email);

                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Registration failed due to email error. Please try again later." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User creation failed for email {Email}", model.Email);

                await transaction.RollbackAsync();

                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "User registration failed. Please try again later, and ensure to use a valid email." });
            }
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

            _logger.LogInformation("Email verified successfully: {UserId}, {Email}", user.Id, email);
            return Ok(new { message = "Email verified successfully. Please login to continue." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO model)
        {
            _logger.LogInformation("Login attempt for email: {Email}", model.Email);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid login model state for {Email}", model.Email);
                return BadRequest(new { message = "An error occurred during registration", error = "Please try again later." });
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                _logger.LogWarning("Login attempt with invalid email {Email}", model.Email);
                return Conflict(new { message = "Email not found. Please register to continue." });
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("Login attempt with unconfirmed email: {Email}", model.Email);
                return Conflict(new { message = "Email not confirmed. Please check your email for confirmation link and try again." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed login attempt (invalid password) for email: {Email}.", user.Email);
                return Unauthorized(new { message = "Invalid email or password. Try again later." });
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Account locked for {Email}", user.Email);
                return Unauthorized(new { message = "Account locked. Try again later." });
            }

            var token = await _tokenGenerator.GenerateToken(user);
            var roles = await _userManager.GetRolesAsync(user);

            _logger.LogInformation("User logged in successfully: {Email}", user.Email);
            return Ok(new 
            { 
                message = "Login successful" ,
                token = token,
                user = new
                {
                    name = user.Name,
                    email = user.Email,
                    role = roles.FirstOrDefault(),
                }
            });


        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;

            _logger.LogInformation("Retrieving profile details for {Email}", userEmail);

            if (string.IsNullOrEmpty(userEmail))
            {
                _logger.LogError("Email claim missing in JWT for user {UserId}", 
                         User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return Unauthorized("Invalid token claims.");
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                _logger.LogWarning("Profile access attempted with valid token but user not found");
                return NotFound(new { message = "User not found." });
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("Unverified user {Email} attempted to access profile", userEmail);
                return Conflict(new
                {
                    message = "Email verification required. Please check your email and click on the verification link to continue."
                });
            }

            _logger.LogInformation("User {Email} accessed their profile", userEmail);

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new 
            { 
                message = "User profile retrieved successfully.",
                user = new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    role = roles.FirstOrDefault()
                }
            });

        }

        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUsers()
        {
            _logger.LogInformation("Retrieving users");
            var users = await _userManager.Users.Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
            }).ToListAsync();

            var usersWithRoles = new List<object>();

            foreach (var user in users)
            {
                var userEntity = await _userManager.FindByIdAsync(user.Id);
                var roles = await _userManager.GetRolesAsync(userEntity);

                usersWithRoles.Add(new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    role = roles
                });
            }

            _logger.LogInformation("Users retrieved successfully");
            return Ok(new
            {
                message = "Users retrieved successfuly.",
                users = usersWithRoles
            });
        }

        [HttpPost("register-user")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RegisterUser([FromBody] AdminRegisterDTO model)
        {
            var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value;

            _logger.LogInformation("Registration attempt for {Email} by {Admin}", model.Email, adminEmail);

            if (!new[] { "user", "admin" }.Contains(model.Role.ToLower()))
            {
                _logger.LogWarning("Invalid role entered: {Role} for {Email}", model.Role, model.Email);
                return BadRequest(new { message = "Invalid role entered. Allowed roles: User, Admin" });
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("Registration attempt for existing user: {Email}", model.Email);
                return Conflict(new { message = "Email already registered." });
            }

            AppUser? user = null;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                user = new AppUser
                {
                    Name = model.Name,
                    Email = model.Email,
                    UserName = model.Email
                };

                var createResult = await _userManager.CreateAsync(user, model.Password);

                if (!createResult.Succeeded)
                    throw new Exception($"User creation failed: {string.Join(", ", createResult.Errors)}");

                var roleResult = await _userManager.AddToRoleAsync(user, model.Role);

                if (!roleResult.Succeeded)
                    throw new Exception($"Role assignment failed: {string.Join(", ", roleResult.Errors)}");

                await VerifyEmail(user.Email);

                await transaction.CommitAsync();

                _logger.LogInformation("Successfully registered user {UserId} at {Timestamp}", user.Id, DateTime.UtcNow);
                return StatusCode(StatusCodes.Status201Created, new
                {
                    message = "User created successfully. Verification email sent.",
                    name = user.Name,
                    email = user.Email,
                    role = model.Role.ToLower()
                });
            }
            catch (ApplicationException ex) when (ex.InnerException is SmtpException)
            {
                _logger.LogError(ex, "Verification email failed to send for email {Email}", model.Email);

                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Registration failed due to email error. Please try again later." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User creation failed for email {Email}", model.Email);

                await transaction.RollbackAsync();

                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "User registration failed. Please try again later, and ensure to use a valid email." });
            }
        }

    }
}
