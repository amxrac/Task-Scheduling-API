using Microsoft.AspNetCore.Identity;
using Task_Scheduling_API.Data;
using Task_Scheduling_API.Models;

namespace Task_Scheduling_API.Data.Seeders
{
    public class AdminSeeder
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _config;
        private readonly ILogger<AdminSeeder> _logger;


        public AdminSeeder(UserManager<AppUser> userManager, IConfiguration config, ILogger<AdminSeeder> logger)
        {
            _userManager = userManager;
            _config = config;
            _logger = logger;
        }

        public async Task<bool> SeedAdminAsync()
        {
            try
            {
                var adminEmail = _config["AdminConfig:Email"];
                var adminPassword = _config["AdminConfig:Password"];

                if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
                {
                    _logger.LogWarning("Admin seeding failed");
                    return false;
                }

                if (await _userManager.FindByEmailAsync(adminEmail) != null)
                {
                    _logger.LogInformation($"Admin user {adminEmail} already exists");
                    return true;
                }

                var adminUser = new AppUser
                {
                    Name = "System Admin",
                    UserName = adminEmail,
                    Email = adminEmail,
                };

                var result = await _userManager.CreateAsync(adminUser, adminPassword);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError($"Failed to create admin user: {errors}");
                    return false;
                }
                await _userManager.AddToRoleAsync(adminUser, "Admin");
                _logger.LogInformation("Admin User seeded successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during admin user seeding");
                return false;
            }
        }
    }
}