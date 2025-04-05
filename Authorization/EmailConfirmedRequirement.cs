using Microsoft.AspNetCore.Authorization;

namespace Task_Scheduling_API.Authorization
{
    public class EmailConfirmedRequirement : IAuthorizationRequirement
    {
        public EmailConfirmedRequirement() { }
    }
}
