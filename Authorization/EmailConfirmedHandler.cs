using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using Task_Scheduling_API.Models;

namespace Task_Scheduling_API.Authorization
{
    public class EmailConfirmedHandler : AuthorizationHandler<EmailConfirmedRequirement>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<EmailConfirmedHandler> _logger;

        public EmailConfirmedHandler(UserManager<AppUser> userManager, ILogger<EmailConfirmedHandler> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, EmailConfirmedRequirement requirement)
        {
            if (!context.User.Identity.IsAuthenticated)
            {
                return;
            }
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return;
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user != null && user.EmailConfirmed)
                {
                    context.Succeed(requirement);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email confirmation authorization for {userId}", userId);
                return;
            }

        }
    }
}
