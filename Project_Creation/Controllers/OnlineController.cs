using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Project_Creation.Data;
using Project_Creation.Models.Entities;

namespace Project_Creation.Controllers
{
    public class OnlineController : Controller
    {
        private readonly AuthDbContext _context;
        public OnlineController(AuthDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Status(string status)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            if (status == "Online")
                user.IsOnline = Users.OnlineStatus.Online;
            else if (status == "Away")
                user.IsOnline = Users.OnlineStatus.Away;
            else if (status == "Offline")
                user.IsOnline = Users.OnlineStatus.Offline;
            else
                return BadRequest("Invalid status.");

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
