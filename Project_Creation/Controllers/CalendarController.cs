using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Project_Creation.Data;
using Project_Creation.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Project_Creation.DTO;

namespace Project_Creation.Controllers
{
    [Authorize]
    public class CalendarController : Controller
    {
        private readonly AuthDbContext _context;

        public CalendarController(AuthDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetTasks()
        {
            try
            {
                // Ensure we have a valid userId
                if (!User.Identity.IsAuthenticated || !User.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
                {
                    return Unauthorized(new { success = false, message = "User not authorized or missing ID claim" });
                }

                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var tasks = await _context.Calendar
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

                var taskDTOs = tasks.Select(t => new CalendarTaskDTO
                {
                    Id = t.Id,
                    Title = t.Title,
                    Date = t.Date.ToString("yyyy-MM-dd"),
                    Time = t.Time.HasValue ? t.Time.Value.ToString("HH:mm") : null,
                    Priority = t.Priority.ToString(),
                    Notes = t.Notes,
                    IsCompleted = t.IsCompleted,
                    CreatedAt = t.CreatedAt
                }).ToList();

                return Json(taskDTOs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
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

        [HttpPost]
        public async Task<IActionResult> AddTask([FromBody] CalendarTaskDTO taskDTO)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                //bool calendarExists = await _context.Calendar
                //    .AnyAsync(u => u.UserId == GetCurrentUserId() && u.Id == taskDTO.Id);
                //if (calendarExists)
                //{
                //    return View();
                //}


                // Ensure we have a valid userId
                if (!User.Identity.IsAuthenticated || !User.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
                {
                    return Unauthorized(new { success = false, message = "User not authorized or missing ID claim" });
                }

                // Parse priority enum
                if (!Enum.TryParse<Priority>(taskDTO.Priority, out var priority))
                {
                    return BadRequest(new { success = false, message = "Invalid priority value" });
                }

                // Parse date
                if (!DateOnly.TryParse(taskDTO.Date, out var date))
                {
                    return BadRequest(new { success = false, message = "Invalid date format" });
                }

                // Parse time if provided
                TimeOnly? time = null;
                if (!string.IsNullOrEmpty(taskDTO.Time))
                {
                    if (!TimeOnly.TryParse(taskDTO.Time, out var parsedTime))
                    {
                        return BadRequest(new { success = false, message = "Invalid time format" });
                    }
                    time = parsedTime;
                }

                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                var task = new Calendar
                {
                    Title = taskDTO.Title,
                    Date = date,
                    Time = time,
                    Priority = priority,
                    Notes = taskDTO.Notes,
                    IsCompleted = taskDTO.IsCompleted,
                    UserId = userId,
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"))
                };

                await _context.Calendar.AddAsync(task);
                await _context.SaveChangesAsync();

                // Convert back to DTO for response
                taskDTO.Id = task.Id;
                taskDTO.CreatedAt = task.CreatedAt;

                return Json(new { success = true, task = taskDTO });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateTask([FromBody] CalendarTaskDTO taskDTO)
        {
            try
            {
                // Ensure we have a valid userId
                if (!User.Identity.IsAuthenticated || !User.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
                {
                    return Unauthorized(new { success = false, message = "User not authorized or missing ID claim" });
                }

                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var existingTask = await _context.Calendar
                    .FirstOrDefaultAsync(c => c.Id == taskDTO.Id && c.UserId == userId);

                if (existingTask == null)
                {
                    return NotFound(new { success = false, message = "Task not found" });
                }

                // Parse priority enum
                if (!Enum.TryParse<Priority>(taskDTO.Priority, out var priority))
                {
                    return BadRequest(new { success = false, message = "Invalid priority value" });
                }

                // Parse date
                if (!DateOnly.TryParse(taskDTO.Date, out var date))
                {
                    return BadRequest(new { success = false, message = "Invalid date format" });
                }

                // Parse time if provided
                TimeOnly? time = null;
                if (!string.IsNullOrEmpty(taskDTO.Time))
                {
                    if (!TimeOnly.TryParse(taskDTO.Time, out var parsedTime))
                    {
                        return BadRequest(new { success = false, message = "Invalid time format" });
                    }
                    time = parsedTime;
                }

                existingTask.Title = taskDTO.Title;
                existingTask.Date = date;
                existingTask.Time = time;
                existingTask.Priority = priority;
                existingTask.Notes = taskDTO.Notes;
                existingTask.IsCompleted = taskDTO.IsCompleted;

                await _context.SaveChangesAsync();

                // Update the DTO with latest data
                taskDTO.CreatedAt = existingTask.CreatedAt;

                return Json(new { success = true, task = taskDTO });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        public class UpdateTaskStatusRequest
        {
            public bool IsCompleted { get; set; }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateTaskStatus([FromQuery] int id, [FromBody] UpdateTaskStatusRequest request)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var task = await _context.Calendar
                    .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

                if (task == null)
                {
                    return NotFound(new { success = false, message = "Task not found" });
                }

                task.IsCompleted = request.IsCompleted;
                await _context.SaveChangesAsync();

                var taskDTO = new CalendarTaskDTO
                {
                    Id = task.Id,
                    Title = task.Title,
                    Date = task.Date.ToString("yyyy-MM-dd"),
                    Time = task.Time.HasValue ? task.Time.Value.ToString("HH:mm") : null,
                    Priority = task.Priority.ToString(),
                    Notes = task.Notes,
                    IsCompleted = task.IsCompleted,
                    CreatedAt = task.CreatedAt
                };

                return Json(new { success = true, task = taskDTO });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteTask(int id)
        {
            try
            {
                if (!User.Identity.IsAuthenticated || !User.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
                {
                    return Json(new { success = false, message = "User not authorized" });
                }

                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // First check if the task exists at all
                var taskExists = await _context.Calendar.AnyAsync(c => c.Id == id);
                if (!taskExists)
                {
                    return Json(new { success = false, message = $"Task with ID {id} not found in database" });
                }

                // Then check if it belongs to the user
                var task = await _context.Calendar
                    .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

                if (task == null)
                {
                    return Json(new { success = false, message = "Task not found or doesn't belong to you" });
                }

                _context.Calendar.Remove(task);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Task deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}