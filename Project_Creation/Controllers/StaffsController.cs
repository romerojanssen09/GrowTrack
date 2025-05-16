using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using Project_Creation.Data;
using Project_Creation.Models.Entities;

namespace Project_Creation.Controllers
{
    public class StaffsController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly AuthDbContext _context;
        private readonly ILogger<StaffsController> _logger;

        public StaffsController(AuthDbContext context, ILogger<StaffsController> logger, IEmailService emailService)
        {
            _emailService = emailService;
            _logger = logger;
            _context = context;
        }

        // GET: Staffs
        public async Task<IActionResult> Index()
        {
            return View(await _context.Staff.ToListAsync());
        }

        // GET: Staffs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var staff = await _context.Staff
                .FirstOrDefaultAsync(m => m.Id == id);
            if (staff == null)
            {
                return NotFound();
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_DetailsPartial", staff);
            }

            return View(staff);
        }

        // POST: Staffs/Create
        [HttpPost]
        //[ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StaffName,StaffSEmail,StaffPhone,StaffAccessLevel")] Staff staff)
        {
            if (ModelState.IsValid)
            {
                // Check if the email already exists in the database
                var existingStaff = await _context.Staff
                    .FirstOrDefaultAsync(s => s.StaffSEmail == staff.StaffSEmail);
                if (existingStaff != null)
                {
                    ModelState.AddModelError("StaffSEmail", "Email already exists.");
                    return BadRequest(ModelState);
                }

                // Check if the email already exists in the database
                var existingUsers = await _context.Users
                    .FirstOrDefaultAsync(s => s.Email == staff.StaffSEmail);
                if (existingStaff != null)
                {
                    ModelState.AddModelError("UsersEmail", "Email already exists.");
                    return BadRequest(ModelState);
                }

                await _emailService.SendEmail2(
                    GetUserDataById(GetCurrentUserId(), "Email"),
                    GetUserDataById(GetCurrentUserId(), "BusinessName"),
                    staff.StaffSEmail,
                    "Staff Account",
                    $"Your account has been created \nclick this link to create your password:{staff.Link}",
                    true);

                staff.IsActive = AccountStatus.Pending;
                staff.CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"));
                _context.Add(staff);
                await _context.SaveChangesAsync();
                return Ok();
            }

            return BadRequest(ModelState);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return 0;
            }

            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            return 0;
        }

        private string GetUserDataById(int userId, string columnName)
        {
            return _context.Users
                .Where(user => user.Id == userId)
                .Select(user => EF.Property<string>(user, columnName))
                .FirstOrDefault()
                ?? "Unknown Data";
        }

        // GET: Staffs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var staff = await _context.Staff.FindAsync(id);
            if (staff == null)
            {
                return NotFound();
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_EditPartial", staff);
            }

            return View(staff);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, [Bind("Id,StaffName,StaffSEmail,StaffPhone,IsActive,StaffAccessLevel")] Staff staff)
        {
            if (id != staff.Id)
            {
                return NotFound();
            }
            var created = staff.CreatedAt;

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if the email already exists in the database
                    var existingStaff = await _context.Staff
                        .FirstOrDefaultAsync(s => s.StaffSEmail == staff.StaffSEmail);
                    if (existingStaff != null)
                    {
                        ModelState.AddModelError("StaffSEmail", "Email already exists.");
                        return BadRequest(ModelState);
                    }
                    staff.CreatedAt = created;
                    staff.UpdatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"));
                    _context.Update(staff);
                    await _context.SaveChangesAsync();
                    return Ok();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StaffExists(staff.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return BadRequest(ModelState);
        }

        // POST: Staffs/Delete/5
        [HttpPost, ActionName("Delete")]
        //[ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var staff = await _context.Staff.FindAsync(id);
            if (staff != null)
            {
                _context.Staff.Remove(staff);
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }

        private bool StaffExists(int id)
        {
            return _context.Staff.Any(e => e.Id == id);
        }
    }
}