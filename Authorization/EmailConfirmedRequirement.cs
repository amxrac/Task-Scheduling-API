using Microsoft.AspNetCore.Authorization;

namespace TaskSchedulingApi.Authorization
{
    public class EmailConfirmedRequirement : IAuthorizationRequirement
    {
        public EmailConfirmedRequirement() { }
    }
}
