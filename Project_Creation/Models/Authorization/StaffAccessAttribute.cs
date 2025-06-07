using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Project_Creation.Models.Entities;
using System.Security.Claims;

namespace Project_Creation.Models.Authorization
{
    public class StaffAccessAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        private readonly StaffAccessLevel _requiredAccess;

        public StaffAccessAttribute(StaffAccessLevel requiredAccess)
        {
            _requiredAccess = requiredAccess;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Skip authorization if action is decorated with [AllowAnonymous] attribute
            if (context.ActionDescriptor.EndpointMetadata.Any(em => em is AllowAnonymousAttribute))
                return;

            // Check if user is authenticated
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Check if user is a BusinessOwner - allow full access
            var role = context.HttpContext.User.FindFirstValue(ClaimTypes.Role);
            if (role == "BusinessOwner")
            {
                return; // Allow access for BusinessOwner
            }

            // For Staff role, check access level
            if (role == "Staff")
            {
                // Get staff access level from claims
                var accessLevelClaim = context.HttpContext.User.FindFirstValue("AccessLevel");
                if (string.IsNullOrEmpty(accessLevelClaim) || 
                    !Enum.TryParse<StaffAccessLevel>(accessLevelClaim, out var staffAccessLevel))
                {
                    context.Result = new ForbidResult();
                    return;
                }

                // Check if staff has required access level
                //if (!staffAccessLevel.HasFlag(_requiredAccess)) // to strick
                if ((staffAccessLevel & _requiredAccess) == 0) // ether nigger
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }
            else
            {
                // If role is neither BusinessOwner nor Staff, forbid access
                context.Result = new ForbidResult();
                return;
            }
        }
    }
} 