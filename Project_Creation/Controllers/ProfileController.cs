using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_Creation.Data;
using Project_Creation.DTO;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Project_Creation.Models.Entities;
using System.Data;
using Microsoft.AspNetCore.Authentication;
using System.Data.Common;
using static Project_Creation.Models.Entities.Users;
using Project_Creation.Models.ViewModels;
//using NHibernate.Linq;

namespace Project_Creation.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly AuthDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<RegisterController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public ProfileController(AuthDbContext context, IWebHostEnvironment webHostEnvironment, ILogger<RegisterController> logger, IWebHostEnvironment environment, IEmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _environment = environment;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
            _emailService = emailService;
            _configuration = configuration;
        }

        [Authorize]
        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    // User not authenticated
                    TempData["ErrorMessage"] = "You need to log in to access your profile.";
                    return RedirectToAction("Login", "Login");
                }

                if (!int.TryParse(userId, out int businessOwnerId))
                {
                    // Invalid user ID
                    TempData["ErrorMessage"] = "There was a problem identifying your account. Please try again.";
                    return RedirectToAction("Dashboard", "Pages");
                }

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = @"
                    SELECT 
                        u.*,
                        a.BrgyClearancePath,
                        a.IsAllowEditBrgyClearance,
                        a.BusinessOwnerValidId,
                        a.IsAllowEditBusinessOwnerValidId,
                        a.SecCertPath,
                        a.IsAllowEditSecCertPath,
                        a.DtiCertPath,
                        a.IsAllowEditDtiCertPath,
                        a.OperationToOperate,
                        l.*
                    FROM Users u
                    LEFT JOIN UsersAdditionInfo a ON u.Id = a.UserId
                    LEFT JOIN UserSocialMediaLinks l ON u.Id = a.UserId
                    WHERE u.Id = @Id";

                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@Id";
                    parameter.Value = businessOwnerId;
                    command.Parameters.Add(parameter);

                    _context.Database.OpenConnection();

                    using (var result = command.ExecuteReader())
                    {
                        if (!result.HasRows)
                        {
                            // User profile not found
                            TempData["ErrorMessage"] = "We couldn't find your profile. Please contact support if the issue persists.";
                            return RedirectToAction("Dashboard", "Pages");
                        }

                        result.Read();

                        // Map Users data
                        var user = MapUserFromReader(result);

                        // Map Additional Info
                        var additionalData = MapAdditionalInfoFromReader(result);

                        var profileViewModel = new ProfileViewModel
                        {
                            // User properties
                            FirstName = user.FirstName,
                            LastName = user.LastName,
                            Email = user.Email,
                            PhoneNumber = user.PhoneNumber,
                            BusinessName = user.BusinessName,
                            BusinessAddress = user.BusinessAddress,
                            DTIRegistrationNumber = user.DTIReqistrationNumber,
                            NumberOfBusinessYearsOperation = user.NumberOfBusinessYearsOperation,
                            CompanyBackground = user.CompanyBackground,
                            BusinessPermitPath = user.BusinessPermitPath,
                            IsAllowEditBusinessPermitPath = user.IsAllowEditBusinessPermitPath,
                            CategoryOfBusiness = user.CategoryOfBusiness,
                            MarkerPlaceStatus = user.MarkerPlaceStatus.ToString(),

                            // Additional info properties
                            BrgyClearancePath = additionalData.BrgyClearancePath,
                            IsAllowEditBrgyClearance = additionalData.IsAllowEditBrgyClearance,
                            BusinessOwnerValidId = additionalData.BusinessOwnerValidId,
                            IsAllowEditBusinessOwnerValidId = additionalData.IsAllowEditBusinessOwnerValidId,
                            SecCertPath = additionalData.SecCertPath,
                            IsAllowEditSecCertPath = additionalData.IsAllowEditSecCertPath,
                            DtiCertPath = additionalData.DtiCertPath,
                            IsAllowEditDtiCertPath = additionalData.IsAllowEditDtiCertPath,
                            OperationToOperate = additionalData.OperationToOperate,

                            // social media links
                            userSocialMediaLinks = new UserSocialMediaLinks
                            {
                                FacebookLinks = result.IsDBNull(result.GetOrdinal("FacebookLinks")) ? string.Empty : result.GetString(result.GetOrdinal("FacebookLinks")),
                                InstagramLinks = result.IsDBNull(result.GetOrdinal("InstagramLinks")) ? string.Empty : result.GetString(result.GetOrdinal("InstagramLinks")),
                                TwitterLinks = result.IsDBNull(result.GetOrdinal("TwitterLinks")) ? string.Empty : result.GetString(result.GetOrdinal("TwitterLinks")),
                                TikTokLinks = result.IsDBNull(result.GetOrdinal("TikTokLinks")) ? string.Empty : result.GetString(result.GetOrdinal("TikTokLinks")),
                                YouTubeLinks = result.IsDBNull(result.GetOrdinal("YouTubeLinks")) ? string.Empty : result.GetString(result.GetOrdinal("YouTubeLinks")),
                                LinkedInLinks = result.IsDBNull(result.GetOrdinal("LinkedInLinks")) ? string.Empty : result.GetString(result.GetOrdinal("LinkedInLinks")),
                                PinterestLinks = result.IsDBNull(result.GetOrdinal("PinterestLinks")) ? string.Empty : result.GetString(result.GetOrdinal("PinterestLinks")),
                                WhatsAppLinks = result.IsDBNull(result.GetOrdinal("WhatsAppLinks")) ? string.Empty : result.GetString(result.GetOrdinal("WhatsAppLinks")),
                                ThreadsLinks = result.IsDBNull(result.GetOrdinal("ThreadsLinks")) ? string.Empty : result.GetString(result.GetOrdinal("ThreadsLinks")),
                                SnapchatLinks = result.IsDBNull(result.GetOrdinal("SnapchatLinks")) ? string.Empty : result.GetString(result.GetOrdinal("SnapchatLinks"))
                            }
                        };
                        return View(profileViewModel);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading profile page for user {UserId}", userId);
                // General error
                TempData["ErrorMessage"] = "Something went wrong while loading your profile. Please try again later.";
                return RedirectToAction("Dashboard", "Pages");
            }
        }

        private Users MapUserFromReader(DbDataReader reader)
        {
            var user = new Users
            {
                Id = GetInt32Safe(reader, "Id"),
                FirstName = GetStringSafe(reader, "FirstName"),
                LastName = GetStringSafe(reader, "LastName"),
                Email = GetStringSafe(reader, "Email"),
                PhoneNumber = GetStringSafe(reader, "PhoneNumber"),
                Password = GetStringSafe(reader, "Password", "placeholder-not-used"),
                BusinessName = GetStringSafe(reader, "BusinessName"),
                BusinessAddress = GetStringSafe(reader, "BusinessAddress"),
                DTIReqistrationNumber = GetStringSafe(reader, "DTIReqistrationNumber"),
                NumberOfBusinessYearsOperation = GetStringSafe(reader, "NumberOfBusinessYearsOperation"),
                CompanyBackground = GetStringSafe(reader, "CompanyBackground"),
                BusinessPermitPath = GetStringSafe(reader, "BusinessPermitPath"),
                CategoryOfBusiness = GetStringSafe(reader, "CategoryOfBusiness"),
                IsAllowEditBusinessPermitPath = GetBooleanSafe(reader, "IsAllowEditBusinessPermitPath")
            };
            
            // Properly set the MarkerPlaceStatus enum value
            if (reader.GetOrdinal("MarkerPlaceStatus") >= 0 && !reader.IsDBNull(reader.GetOrdinal("MarkerPlaceStatus")))
            {
                int statusValue = reader.GetInt32(reader.GetOrdinal("MarkerPlaceStatus"));
                user.MarkerPlaceStatus = (MarketplaceStatus)statusValue;
            }
            
            return user;
        }

        private UsersAdditionalInfoDto MapAdditionalInfoFromReader(DbDataReader reader)
        {
            return new UsersAdditionalInfoDto
            {
                BrgyClearancePath = GetStringSafe(reader, "BrgyClearancePath"),
                IsAllowEditBrgyClearance = GetBooleanSafe(reader, "IsAllowEditBrgyClearance"),
                BusinessOwnerValidId = GetStringSafe(reader, "BusinessOwnerValidId"),
                IsAllowEditBusinessOwnerValidId = GetBooleanSafe(reader, "IsAllowEditBusinessOwnerValidId"),
                SecCertPath = GetStringSafe(reader, "SecCertPath"),
                IsAllowEditSecCertPath = GetBooleanSafe(reader, "IsAllowEditSecCertPath"),
                DtiCertPath = GetStringSafe(reader, "DtiCertPath"),
                IsAllowEditDtiCertPath = GetBooleanSafe(reader, "IsAllowEditDtiCertPath"),
                OperationToOperate = GetStringSafe(reader, "OperationToOperate")
            };
        }

        // Helper methods
        private string GetStringSafe(DbDataReader reader, string columnName, string defaultValue = "")
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetString(ordinal);
        }

        private int GetInt32Safe(DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        }

        private bool GetBooleanSafe(DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateProfile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning("ModelState error: {Error}", error.ErrorMessage);
                }
                return View("Index", model);
            }

            // Get the current user's ID from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                _logger.LogWarning("User ID not found in claims");
                TempData["ErrorMessage"] = "User identification failed. Please log in again.";
                return RedirectToAction("Login", "Login");
            }

            // Parse userId as integer
            if (!int.TryParse(userId, out int businessOwnerId))
            {
                _logger.LogWarning("Invalid user ID format: {UserId}", userId);
                TempData["ErrorMessage"] = "Invalid user ID format.";
                return RedirectToAction("Dashboard", "Pages");
            }

            _logger.LogInformation("Processing profile update for user {UserId}", businessOwnerId);

            // Handle file upload for business permit if provided
            string businessPermitPath = model.BusinessPermitPath;
            if (model.BusinessPermitFile != null && model.BusinessPermitFile.Length > 0)
            {
                try
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "business_permits");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.BusinessPermitFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        model.BusinessPermitFile.CopyTo(fileStream);
                    }

                    businessPermitPath = "/uploads/business_permits/" + uniqueFileName;
                    _logger.LogInformation("Business permit file uploaded successfully: {FilePath}", businessPermitPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading business permit file");
                    TempData["ErrorMessage"] = "Error uploading business permit file. Please try again.";
                    return RedirectToAction(nameof(Index));
                }
            }

            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    // Ensure connection is open
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                        _logger.LogInformation("Database connection opened");
                    }

                    // First check if user exists
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM Users WHERE Id = @Id";
                        var parameter = command.CreateParameter();
                        parameter.ParameterName = "@Id";
                        parameter.Value = businessOwnerId;
                        command.Parameters.Add(parameter);

                        using (var result = command.ExecuteReader())
                        {
                            if (!result.HasRows)
                            {
                                _logger.LogWarning("User not found with ID: {UserId}", businessOwnerId);
                                TempData["ErrorMessage"] = "User profile not found.";
                                return RedirectToAction("Dashboard", "Pages");
                            }

                            result.Read();

                            // Manually map the data
                            var user = new Users
                            {
                                Id = result.GetInt32(result.GetOrdinal("Id")),
                                FirstName = result.IsDBNull(result.GetOrdinal("FirstName")) ? string.Empty : result.GetString(result.GetOrdinal("FirstName")),
                                LastName = result.IsDBNull(result.GetOrdinal("LastName")) ? string.Empty : result.GetString(result.GetOrdinal("LastName")),
                                Email = result.IsDBNull(result.GetOrdinal("Email")) ? string.Empty : result.GetString(result.GetOrdinal("Email")),
                                PhoneNumber = result.IsDBNull(result.GetOrdinal("PhoneNumber")) ? string.Empty : result.GetString(result.GetOrdinal("PhoneNumber")),
                                Password = result.IsDBNull(result.GetOrdinal("Password")) ? "placeholder-not-used" : result.GetString(result.GetOrdinal("Password")),
                                BusinessName = result.IsDBNull(result.GetOrdinal("BusinessName")) ? string.Empty : result.GetString(result.GetOrdinal("BusinessName")),
                                BusinessAddress = result.IsDBNull(result.GetOrdinal("BusinessAddress")) ? string.Empty : result.GetString(result.GetOrdinal("BusinessAddress")),
                                DTIReqistrationNumber = result.IsDBNull(result.GetOrdinal("DTIReqistrationNumber")) ? string.Empty : result.GetString(result.GetOrdinal("DTIReqistrationNumber")),
                                NumberOfBusinessYearsOperation = result.IsDBNull(result.GetOrdinal("NumberOfBusinessYearsOperation")) ? string.Empty : result.GetString(result.GetOrdinal("NumberOfBusinessYearsOperation")),
                                CompanyBackground = result.IsDBNull(result.GetOrdinal("CompanyBackground")) ? string.Empty : result.GetString(result.GetOrdinal("CompanyBackground")),
                                BusinessPermitPath = result.IsDBNull(result.GetOrdinal("BusinessPermitPath")) ? string.Empty : result.GetString(result.GetOrdinal("BusinessPermitPath")),
                            };

                            // Track which fields are actually changing
                            var changedFields = new List<string>();
                            
                            if (user.FirstName != model.FirstName) 
                            {
                                changedFields.Add("First Name");
                                user.FirstName = model.FirstName;
                            }
                            
                            if (user.LastName != model.LastName) 
                            {
                                changedFields.Add("Last Name");
                                user.LastName = model.LastName;
                            }
                            
                            if (user.PhoneNumber != model.PhoneNumber) 
                            {
                                changedFields.Add("Phone Number");
                                user.PhoneNumber = model.PhoneNumber;
                            }
                            
                            if (user.BusinessName != model.BusinessName) 
                            {
                                changedFields.Add("Business Name");
                                user.BusinessName = model.BusinessName;
                            }
                            
                            if (user.BusinessAddress != model.BusinessAddress) 
                            {
                                changedFields.Add("Business Address");
                                user.BusinessAddress = model.BusinessAddress;
                            }
                            
                            if (user.DTIReqistrationNumber != model.DTIRegistrationNumber) 
                            {
                                changedFields.Add("DTI Registration Number");
                                user.DTIReqistrationNumber = model.DTIRegistrationNumber;
                            }
                            
                            if (user.NumberOfBusinessYearsOperation != model.NumberOfBusinessYearsOperation) 
                            {
                                changedFields.Add("Years in Operation");
                                user.NumberOfBusinessYearsOperation = model.NumberOfBusinessYearsOperation;
                            }
                            
                            if (user.CompanyBackground != model.CompanyBackground) 
                            {
                                changedFields.Add("Company Background");
                                user.CompanyBackground = model.CompanyBackground;
                            }

                            // Only update business permit path if a new file was uploaded
                            if (model.BusinessPermitFile != null && model.BusinessPermitFile.Length > 0)
                            {
                                changedFields.Add("Business Permit");
                                user.BusinessPermitPath = businessPermitPath;
                            }

                            // If no changes were made, inform the user and return
                            if (changedFields.Count == 0)
                            {
                                _logger.LogInformation("No changes detected in profile update for user {UserId}", businessOwnerId);
                                TempData["WarningMessage"] = "No changes were made to your profile. Please make sure you've changed at least one field before saving.";
                                return RedirectToAction(nameof(Index));
                            }

                            // Build SQL UPDATE statement
                            string updateSql = @"
                                UPDATE Users 
                                SET FirstName = @FirstName, 
                                    LastName = @LastName,
                                    PhoneNumber = @PhoneNumber,
                                    BusinessName = @BusinessName,
                                    BusinessAddress = @BusinessAddress,
                                    DTIReqistrationNumber = @DTIReqistrationNumber,
                                    NumberOfBusinessYearsOperation = @NumberOfBusinessYearsOperation,
                                    CompanyBackground = @CompanyBackground";

                            // Only include business permit path in SQL if a new file was uploaded
                            if (model.BusinessPermitFile != null && model.BusinessPermitFile.Length > 0)
                            {
                                updateSql += ", BusinessPermitPath = @BusinessPermitPath";
                            }

                            updateSql += " WHERE Id = @Id";

                            // Create direct command for better control over execution
                            using (var updateCommand = connection.CreateCommand())
                            {
                                updateCommand.CommandText = updateSql;

                                // Add parameters to command
                                var parameters = new Dictionary<string, object>
                                {
                                    { "@FirstName", model.FirstName },
                                    { "@LastName", model.LastName },
                                    { "@PhoneNumber", model.PhoneNumber },
                                    { "@BusinessName", model.BusinessName },
                                    { "@BusinessAddress", model.BusinessAddress },
                                    { "@DTIReqistrationNumber", model.DTIRegistrationNumber },
                                    { "@NumberOfBusinessYearsOperation", model.NumberOfBusinessYearsOperation },
                                    { "@CompanyBackground", model.CompanyBackground },
                                    { "@Id", businessOwnerId }
                                };

                                // Add BusinessPermitPath parameter if needed
                                if (model.BusinessPermitFile != null && model.BusinessPermitFile.Length > 0)
                                {
                                    parameters.Add("@BusinessPermitPath", businessPermitPath);
                                }

                                // Add parameters to command
                                foreach (var param in parameters)
                                {
                                    var sqlParameter = updateCommand.CreateParameter();
                                    sqlParameter.ParameterName = param.Key;
                                    sqlParameter.Value = param.Value ?? DBNull.Value;
                                    updateCommand.Parameters.Add(sqlParameter);
                                }

                                // Log the SQL for debugging
                                _logger.LogDebug("Executing SQL: {Sql}", updateSql);

                                // Execute the command and get rows affected
                                var rowsAffected = updateCommand.ExecuteNonQuery();
                                _logger.LogInformation("Profile update affected {RowCount} rows for user {UserId}", rowsAffected, businessOwnerId);

                                if (rowsAffected > 0)
                                {
                                    var fieldsUpdated = string.Join(", ", changedFields);
                                    TempData["SuccessMessage"] = $"Profile updated successfully! Updated information: {fieldsUpdated}";

                                    // Update user claims to ensure dashboard displays correct information
                                    try
                                    {
                                        // Get the current principal
                                        var identity = (System.Security.Claims.ClaimsIdentity)User.Identity;
                                        bool claimsChanged = false;

                                        // Update BusinessName claim
                                        if (!string.IsNullOrEmpty(model.BusinessName))
                                        {
                                            var existingBusinessClaim = identity.FindFirst("BusinessName");
                                            if (existingBusinessClaim != null)
                                            {
                                                identity.RemoveClaim(existingBusinessClaim);
                                            }
                                            identity.AddClaim(new System.Security.Claims.Claim("BusinessName", model.BusinessName));
                                            claimsChanged = true;
                                        }

                                        // Update Name claim if first name or last name changed
                                        if (!string.IsNullOrEmpty(model.FirstName) || !string.IsNullOrEmpty(model.LastName))
                                        {
                                            // Get updated name from database to ensure accuracy
                                            using (var nameCommand = connection.CreateCommand())
                                            {
                                                nameCommand.CommandText = "SELECT FirstName, LastName FROM Users WHERE Id = @Id";
                                                var idParam = nameCommand.CreateParameter();
                                                idParam.ParameterName = "@Id";
                                                idParam.Value = businessOwnerId;
                                                nameCommand.Parameters.Add(idParam);

                                                using (var reader = nameCommand.ExecuteReader())
                                                {
                                                    if (reader.Read())
                                                    {
                                                        string dbFirstName = reader.IsDBNull(reader.GetOrdinal("FirstName"))
                                                            ? string.Empty : reader.GetString(reader.GetOrdinal("FirstName"));
                                                        string dbLastName = reader.IsDBNull(reader.GetOrdinal("LastName"))
                                                            ? string.Empty : reader.GetString(reader.GetOrdinal("LastName"));

                                                        string fullName = $"{dbFirstName} {dbLastName}";

                                                        // Update Name claim
                                                        var existingNameClaim = identity.FindFirst(System.Security.Claims.ClaimTypes.Name);
                                                        if (existingNameClaim != null)
                                                        {
                                                            identity.RemoveClaim(existingNameClaim);
                                                        }
                                                        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, fullName));
                                                        claimsChanged = true;
                                                    }
                                                }
                                            }
                                        }

                                        // Sign in with updated claims if changes were made
                                        if (claimsChanged)
                                        {
                                            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
                                            HttpContext.SignInAsync(principal);
                                            _logger.LogInformation("User claims updated for user {UserId}", businessOwnerId);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // Log exception but don't prevent successful update
                                        _logger.LogError(ex, "Error updating user claims for user {UserId}", businessOwnerId);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("No rows affected when updating profile for user {UserId}", businessOwnerId);
                                    TempData["WarningMessage"] = "No changes were made to your profile. Please make sure you've changed at least one field before saving.";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", businessOwnerId);
                TempData["ErrorMessage"] = $"Error updating profile: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Settings()
        {
            // Get User from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? string.Empty;
            var fullname = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? string.Empty;

            // Get user settings from database
            var user = _context.Users.FirstOrDefault(u => u.Id.ToString() == userId);
            var admin = _context.Admin.FirstOrDefault(a => a.Id.ToString() == userId);
            var staff = _context.Staffs.FirstOrDefault(s => s.Id.ToString() == userId);

            // Create settings view model
            var settingsViewModel = new SettingsViewModel
            {
                Email = email,
                Fullname = fullname,
                TwoFactorEnabled = user?.TwoFactorAuthentication ?? admin?.TwoFactorAuthentication ?? staff?.TwoFactorAuthentication ?? false,
                AllowEmailNotifications = user?.AllowEmailNotifications ?? admin?.AllowEmailNotifications ?? staff?.AllowEmailNotifications ?? false,
                AllowLoginAlerts = user?.AllowLoginAlerts ?? admin?.AllowLoginAlerts ?? staff?.AllowLoginAlerts ?? false
            };
            
            return View(settingsViewModel);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
    if (!ModelState.IsValid)
    {
        return Json(new { success = false, message = "Please correct the errors and try again." });
    }

    // Check if password and confirmation match
    if (model.NewPassword != model.ConfirmPassword)
    {
        return Json(new { success = false, message = "New password and confirmation password do not match." });
    }

    // Get the current user's ID from claims
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId == null)
    {
        return Json(new { success = false, message = "User not found. Please log in again." });
    }

    // Parse userId as integer
    if (!int.TryParse(userId, out int businessOwnerId))
    {
        return Json(new { success = false, message = "Invalid user ID format." });
    }

    try
    {
        // First verify the current password
        bool passwordVerified = false;
        string storedHashedPassword = string.Empty;

        // Get the stored hashed password
        using (var connection = _context.Database.GetDbConnection())
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Password FROM Users WHERE Id = @Id";
                var param = command.CreateParameter();
                param.ParameterName = "@Id";
                param.Value = businessOwnerId;
                command.Parameters.Add(param);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        storedHashedPassword = reader.IsDBNull(reader.GetOrdinal("Password"))
                            ? string.Empty : reader.GetString(reader.GetOrdinal("Password"));
                    }
                    else
                    {
                        return Json(new { success = false, message = "User not found." });
                    }
                }
            }

            // Verify current password
            passwordVerified = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, storedHashedPassword);

            if (!passwordVerified)
            {
                return Json(new { success = false, message = "Current password is incorrect." });
            }
            
            // Check if new password is the same as the current password
            if (BCrypt.Net.BCrypt.Verify(model.NewPassword, storedHashedPassword))
            {
                return Json(new { success = false, message = "New password cannot be the same as your current password." });
            }

            // Hash the new password
            string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

            // Update the password
            using (var updateCommand = connection.CreateCommand())
            {
                updateCommand.CommandText = "UPDATE Users SET Password = @Password WHERE Id = @Id";

                var passwordParam = updateCommand.CreateParameter();
                passwordParam.ParameterName = "@Password";
                passwordParam.Value = newHashedPassword;
                updateCommand.Parameters.Add(passwordParam);

                var idParam = updateCommand.CreateParameter();
                idParam.ParameterName = "@Id";
                idParam.Value = businessOwnerId;
                updateCommand.Parameters.Add(idParam);

                int rowsAffected = updateCommand.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    return Json(new { success = true, message = "Password changed successfully! Please use your new password the next time you log in." });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to update password. Please try again." });
                }
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error changing password for user {UserId}", userId);
        return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
    }
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ValidationForMarketplace(
    [FromForm] UsersAdditionalInfoDto model,
    [FromForm] IFormFile BrgyClearanceFile,
    [FromForm] IFormFile OperationToOperate,
    [FromForm] IFormFile OwnerIdFile,
    [FromForm] IFormFile SecCertFile,
    [FromForm] IFormFile DtiCertFile,
    [FromForm] IFormFile BusinessPermitFile)
        {
            try
            {
                // Validate required files
                if (BrgyClearanceFile == null || BrgyClearanceFile.Length == 0)
                {
                    _logger.LogWarning("Validation failed: Barangay Clearance is required for user {UserId}", model.UserId);
                    ModelState.AddModelError("", "Barangay Clearance is required");
                    return RedirectToAction("Index");
                }

                if (OwnerIdFile == null || OwnerIdFile.Length == 0)
                {
                    _logger.LogWarning("Validation failed: Owner ID is required for user {UserId}", model.UserId);
                    ModelState.AddModelError("", "Owner ID is required");
                    return RedirectToAction("Index");
                }

                if (SecCertFile == null || SecCertFile.Length == 0)
                {
                    _logger.LogWarning("Validation failed: SEC Certificate is required for user {UserId}", model.UserId);
                    ModelState.AddModelError("", "SEC Certificate is required");
                    return RedirectToAction("Index");
                }

                if (DtiCertFile == null || DtiCertFile.Length == 0)
                {
                    _logger.LogWarning("Validation failed: DTI Certificate is required for user {UserId}", model.UserId);
                    ModelState.AddModelError("", "DTI Certificate is required");
                    return RedirectToAction("Index");
                }

        var existingUser = await _context.Users.FindAsync(model.UserId);
        if (existingUser == null)
        {
            _logger.LogError("User not found");
            TempData["ErrorMessage"] = "User Not Found!";
            return RedirectToAction("Login", "Login");
        }

        // Check if user has rejected status and needs to replace files
        bool isResubmission = existingUser.MarkerPlaceStatus == MarketplaceStatus.Rejected;
        
        // Get existing additional info if any
        var existingAdditionalInfo = await _context.UsersAdditionInfo.FirstOrDefaultAsync(a => a.UserId == model.UserId);
        
        // List to track files to delete
        var filesToDelete = new List<string>();
        
        // If it's a resubmission, collect old file paths to delete them later
        if (isResubmission && existingAdditionalInfo != null)
        {
            if (!string.IsNullOrEmpty(existingAdditionalInfo.BrgyClearancePath))
                filesToDelete.Add(existingAdditionalInfo.BrgyClearancePath);
                
            if (!string.IsNullOrEmpty(existingAdditionalInfo.BusinessOwnerValidId))
                filesToDelete.Add(existingAdditionalInfo.BusinessOwnerValidId);
                
            if (!string.IsNullOrEmpty(existingAdditionalInfo.SecCertPath))
                filesToDelete.Add(existingAdditionalInfo.SecCertPath);
                
            if (!string.IsNullOrEmpty(existingAdditionalInfo.DtiCertPath))
                filesToDelete.Add(existingAdditionalInfo.DtiCertPath);
                
            if (!string.IsNullOrEmpty(existingAdditionalInfo.OperationToOperate))
                filesToDelete.Add(existingAdditionalInfo.OperationToOperate);
                
            // Remove existing record if it's a resubmission
            _context.UsersAdditionInfo.Remove(existingAdditionalInfo);
            await _context.SaveChangesAsync();
        }
        
        // Add business permit to files to delete if it's a resubmission
        if (isResubmission && !string.IsNullOrEmpty(existingUser.BusinessPermitPath))
        {
            filesToDelete.Add(existingUser.BusinessPermitPath);
        }
        
                // Process file uploads
                var additionalInfo = new UsersAdditionInfo
                {
                    UserId = model.UserId,
                    BrgyClearancePath = await SaveFile(model.UserId.ToString(), BrgyClearanceFile, "BrgyClearance"),
                    IsAllowEditBrgyClearance = false,
                    BusinessOwnerValidId = await SaveFile(model.UserId.ToString(), OwnerIdFile, "BusinessOwnerValidId"),
                    IsAllowEditBusinessOwnerValidId = false,
                    SecCertPath = await SaveFile(model.UserId.ToString(), SecCertFile, "SecCertPath"),
                    IsAllowEditSecCertPath = false,
                    DtiCertPath = await SaveFile(model.UserId.ToString(), DtiCertFile, "DtiCertPath"),
                    IsAllowEditDtiCertPath = false,
                    OperationToOperate = string.Empty,
                    SubmissionDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore"))
                };
                
                if (OperationToOperate != null && OperationToOperate.Length > 0)
                {
                    // Save the file and get the file path
                    additionalInfo.OperationToOperate = await SaveFile(model.UserId.ToString(), OperationToOperate, "BusinessPermits");
                }

                if (BusinessPermitFile != null && BusinessPermitFile.Length > 0)
                {
                    // Save the file and get the file path
                    string savedFilePath = await SaveFile(model.UserId.ToString(), BusinessPermitFile, "BusinessPermits");
                    existingUser.BusinessPermitPath = savedFilePath;
                }
                
                // Assign the saved file path to the BusinessPermitPath property
                existingUser.MarkerPlaceStatus = MarketplaceStatus.AwaitingApproval;
        _logger.LogInformation("existingUser.MarkerPlaceStatus set to {Status}", existingUser.MarkerPlaceStatus);

                _context.Users.Update(existingUser);
                await _context.SaveChangesAsync();

                _context.UsersAdditionInfo.Add(additionalInfo);

                // Save all changes
                await _context.SaveChangesAsync();
        
        // Now that new files are saved, delete old files
        foreach (var filePath in filesToDelete)
        {
            try 
            {
                var physicalPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                    _logger.LogInformation("Deleted old file: {FilePath}", physicalPath);
                }
            }
            catch (Exception ex)
            {
                // Log but continue with other files
                _logger.LogWarning(ex, "Error deleting old file {FilePath}", filePath);
            }
        }

                // Get user details for email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == model.UserId);

                if (user != null)
                {
                    // Send email to user
                    var userEmailBody = $@"
                <h3>Thank you for submitting your documents, {user.FirstName}!</h3>
                <p>We've received your marketplace validation documents and they are now under review.</p>
                <p>Submitted documents:</p>
                <ul>
                    <li>Barangay Clearance</li>
                    <li>Owner ID</li>
                    <li>SEC Certificate</li>
                    <li>DTI Certificate</li>
                    <li>Business Permit</li>
                </ul>
                <p>We'll notify you via email once the verification process is complete.</p>";

                    await _emailService.SendEmail(
                        user.Email,
                isResubmission ? "Marketplace Validation Resubmission Received" : "Marketplace Validation Submission Received",
                        userEmailBody,
                        true);

                    // Send notification to admin
                    var adminEmail = _configuration["AdminSettings:Email"];
                    var adminEmailBody = $@"
                <h3>{(isResubmission ? "Resubmitted" : "New")} Marketplace Validation Submission</h3>
                        <p>User Details:</p>
                        <ul>
                            <li>Name: {user.FirstName} {user.LastName}</li>
                            <li>Email: {user.Email}</li>
                            <li>Business: {user.BusinessName}</li>
                    <li>Status: {(isResubmission ? "Resubmission after rejection" : "New submission")}</li>
                        </ul>
                        <p>Please review the submitted documents in the admin panel.</p>";

                    await _emailService.SendEmail(
                        adminEmail,
                $"{(isResubmission ? "Resubmitted" : "New")} Marketplace Validation Submission Needs Review",
                        adminEmailBody,
                        true);
                }

                _logger.LogInformation("Successfully processed marketplace validation for user {UserId}", model.UserId);
        TempData["SuccessMessage"] = isResubmission 
            ? "Documents resubmitted successfully! You'll receive an email once verification is complete." 
            : "Documents submitted successfully! You'll receive an email once verification is complete.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Detailed error logging
                _logger.LogError(ex, "Error during marketplace validation for user {UserId}. Error Details: {ErrorMessage}",
                    model?.UserId, ex.Message);

                // Log inner exception if it exists
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {InnerExceptionMessage}", ex.InnerException.Message);
                }

                // Log the model state if available
                if (model != null)
                {
                    _logger.LogError("Model state: {@Model}", model);
                }

                TempData["ErrorMessage"] = "An error occurred while processing your registration. Please try again.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            try
            {
                // Get the current user's ID from claims
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null)
                {
                    TempData["ErrorMessage"] = "User not found. Please log in again.";
                    return RedirectToAction("Login", "Login");
                }

                // Parse userId as integer
                if (!int.TryParse(userId, out int businessOwnerId))
                {
                    TempData["ErrorMessage"] = "Invalid user ID format.";
                    return RedirectToAction("Dashboard", "Pages");
                }

                // Find the user
                var user = await _context.Users.FindAsync(businessOwnerId);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Login", "Login");
                }

                // Find and delete additional info if exists
                var additionalInfo = await _context.UsersAdditionInfo.FirstOrDefaultAsync(a => a.UserId == businessOwnerId);
                if (additionalInfo != null)
                {
                    _context.UsersAdditionInfo.Remove(additionalInfo);
                }

                // Find and delete all chat messages
                var userChats = await _context.Chats.Where(c => c.SenderId == businessOwnerId || c.ReceiverId == businessOwnerId).ToListAsync();
                if (userChats.Any())
                {
                    _context.Chats.RemoveRange(userChats);
                    _logger.LogInformation("Deleted {Count} chat messages for user {UserId}", userChats.Count, userId);
                }

                // Delete staff records
                var staff = await _context.Staff.Where(a => a.BOId == businessOwnerId).ToListAsync();
                if (staff.Any())
                {
                    _context.Staff.RemoveRange(staff);
                }

                // Delete supplier records
                var suppliers = await _context.Supplier2.Where(a => a.BOId == businessOwnerId).ToListAsync();
                if (suppliers.Any())
                {
                    _context.Supplier2.RemoveRange(suppliers);
                }

                // Delete sales and related items
                var sales = await _context.Sales.Where(a => a.BOId == businessOwnerId).ToListAsync();
                if (sales.Any())
                {
                    var saleIds = sales.Select(s => s.Id).ToList();
                    var saleItems = await _context.SaleItems.Where(a => saleIds.Contains(a.SaleId)).ToListAsync();
                    if (saleItems.Any())
                    {
                        _context.SaleItems.RemoveRange(saleItems);
                    }
                    _context.Sales.RemoveRange(sales);
                }

                // Delete products and related images
                var products = await _context.Products2.Where(a => a.BOId == businessOwnerId).ToListAsync();
                if (products.Any())
                {
                    var productIds = products.Select(p => p.Id).ToList();
                    var productImages = await _context.ProductImages.Where(a => productIds.Contains(a.ProductId)).ToListAsync();
                    if (productImages.Any())
                    {
                        _context.ProductImages.RemoveRange(productImages);
                    }
                    _context.Products2.RemoveRange(products);
                }

                // Delete other related entities
                var entitiesToDelete = new List<IQueryable<object>>
                {
                    _context.Leads.Where(a => a.CreatedById == businessOwnerId),
                    _context.InventoryTransactions.Where(a => a.BOId == businessOwnerId),
                    _context.InventoryLogs.Where(a => a.BOId == businessOwnerId),
                    _context.Categories.Where(a => a.BOId == businessOwnerId),
                    _context.Campaigns.Where(a => a.SenderId == businessOwnerId),
                    _context.Calendar.Where(a => a.UserId == businessOwnerId),
                    _context.BOBusinessProfiles.Where(a => a.UserId == businessOwnerId)
                };

                foreach (var entityQuery in entitiesToDelete)
                {
                    var entities = await entityQuery.ToListAsync();
                    if (entities.Any())
                    {
                        _context.RemoveRange(entities);
                    }
                }

                // Delete user files and folders
                try
                {
                    var userUploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", userId);
                    if (Directory.Exists(userUploadsFolder))
                    {
                        Directory.Delete(userUploadsFolder, true);
                        _logger.LogInformation("Deleted main uploads folder for user {UserId}", userId);
                    }

                    var uploadsBaseFolder = Path.Combine(_environment.WebRootPath, "uploads");
                    if (Directory.Exists(uploadsBaseFolder))
                    {
                        // Search in specific subfolders
                        string[] commonSubfolders = { "business_permits", "BrgyClearance", "BusinessOwnerValidId", "SecCertPath", "DtiCertPath" };
                        foreach (var subfolder in commonSubfolders)
                        {
                            var path = Path.Combine(uploadsBaseFolder, subfolder);
                            if (Directory.Exists(path))
                            {
                                foreach (var file in Directory.GetFiles(path, $"*{userId}*", SearchOption.AllDirectories))
                                {
                                    System.IO.File.Delete(file);
                                    _logger.LogInformation("Deleted user file: {File}", file);
                                }
                            }
                        }

                        // Search for any remaining user folders
                        foreach (var folder in Directory.GetDirectories(uploadsBaseFolder, $"*{userId}*", SearchOption.AllDirectories))
                        {
                            if (folder != userUploadsFolder)
                            {
                                Directory.Delete(folder, true);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deleting user files for {UserId}", userId);
                }

                // Remove user and save changes
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                // Sign out
                await HttpContext.SignOutAsync();

                // Send confirmation email
                try
                {
                    await _emailService.SendEmail(
                        user.Email,
                        "Account Deletion Confirmation",
                        $@"<h3>Account Deleted</h3>
                   <p>Hello {user.FirstName},</p>
                   <p>Your account and all associated data have been permanently deleted.</p>",
                        true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send deletion email to {Email}", user.Email);
                }

                TempData["SuccessMessage"] = "Your account has been permanently deleted.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account deletion failed");
                TempData["ErrorMessage"] = "Account deletion failed. Please contact support.";
                return RedirectToAction("Settings");
            }
        }

        private async Task<string> SaveFile(string userId, IFormFile file, string subFolder)
        {
            if (file == null || file.Length == 0)
                return string.Empty;

            // Validate file extension
            var permittedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(extension) || !permittedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Invalid file type. Only PDF, JPG, and PNG are allowed.");
            }

            // Validate file size (5MB limit)
            if (file.Length > 5 * 1024 * 1024)
            {
                throw new InvalidOperationException("File size exceeds 5MB limit.");
            }

            var userUploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", userId, subFolder);
            Directory.CreateDirectory(userUploadsFolder);

            var fileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(userUploadsFolder, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/{userId}/{subFolder}/{fileName}";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTwoFactor(bool enabled)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                var admin = await _context.Admin.FirstOrDefaultAsync(a => a.Id.ToString() == userId);
                var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.Id.ToString() == userId);

                if (user != null)
                {
                    user.TwoFactorAuthentication = enabled;
                }
                else if (admin != null)
                {
                    admin.TwoFactorAuthentication = enabled;
                }
                else if (staff != null)
                {
                    staff.TwoFactorAuthentication = enabled;
                }
                else
                {
                    return Json(new { success = false, message = "User not found." });
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Two-factor authentication {Status} for user {UserId}", 
                    enabled ? "enabled" : "disabled", userId);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling two-factor authentication");
                return Json(new { success = false, message = "An error occurred while updating settings." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEmailNotifications(bool enabled)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                var admin = await _context.Admin.FirstOrDefaultAsync(a => a.Id.ToString() == userId);
                var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.Id.ToString() == userId);

                if (user != null)
                {
                    user.AllowEmailNotifications = enabled;
                }
                else if (admin != null)
                {
                    admin.AllowEmailNotifications = enabled;
                }
                else if (staff != null)
                {
                    staff.AllowEmailNotifications = enabled;
                }
                else
                {
                    return Json(new { success = false, message = "User not found." });
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Email notifications {Status} for user {UserId}", 
                    enabled ? "enabled" : "disabled", userId);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling email notifications");
                return Json(new { success = false, message = "An error occurred while updating settings." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLoginAlerts(bool enabled)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                var admin = await _context.Admin.FirstOrDefaultAsync(a => a.Id.ToString() == userId);
                var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.Id.ToString() == userId);

                if (user != null)
                {
                    user.AllowLoginAlerts = enabled;
                }
                else if (admin != null)
                {
                    admin.AllowLoginAlerts = enabled;
                }
                else if (staff != null)
                {
                    staff.AllowLoginAlerts = enabled;
                }
                else
                {
                    return Json(new { success = false, message = "User not found." });
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Login alerts {Status} for user {UserId}", 
                    enabled ? "enabled" : "disabled", userId);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling login alerts");
                return Json(new { success = false, message = "An error occurred while updating settings." });
            }
        }

        public async Task<IActionResult> SocialMediaLinks(ProfileViewModel model)
        {
            var link = model.userSocialMediaLinks;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                TempData["ErrorMessage"] = "User not found. Please log in again.";
                return RedirectToAction("Login", "Login");
            }
            // Parse userId as integer
            if (!int.TryParse(userId, out int businessOwnerId))
            {
                TempData["ErrorMessage"] = "Invalid user ID format.";
                return RedirectToAction("Dashboard", "Pages");
            }
            var user = await _context.Users.FindAsync(businessOwnerId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login", "Login");
            }
            if (link != null)
            {
                var existingLink = await _context.UserSocialMediaLinks.FirstOrDefaultAsync(l => l.UserId == businessOwnerId);
                if (existingLink != null)
                {
                    _context.UserSocialMediaLinks.Remove(existingLink);
                }
                var newLink = new UserSocialMediaLinks
                {
                    UserId = businessOwnerId,
                    FacebookLinks = link.FacebookLinks,
                    InstagramLinks = link.InstagramLinks,
                    TwitterLinks = link.TwitterLinks,
                    TikTokLinks = link.TikTokLinks,
                    PinterestLinks = link.PinterestLinks,
                    WhatsAppLinks = link.WhatsAppLinks,
                    ThreadsLinks = link.ThreadsLinks,
                    YouTubeLinks = link.YouTubeLinks,
                    SnapchatLinks = link.SnapchatLinks,
                    LinkedInLinks = link.LinkedInLinks
                };
                _context.UserSocialMediaLinks.Update(newLink);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Social media links updated successfully!";
                return RedirectToAction("Index");
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update social media links.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetMarketplaceValidation()
        {
            try
            {
                // Get the current user's ID from claims
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null)
                {
                    TempData["ErrorMessage"] = "User not found. Please log in again.";
                    return RedirectToAction("Login", "Login");
                }

                // Parse userId as integer
                if (!int.TryParse(userId, out int businessOwnerId))
                {
                    TempData["ErrorMessage"] = "Invalid user ID format.";
                    return RedirectToAction("Index");
                }

                // Get the user
                var user = await _context.Users.FindAsync(businessOwnerId);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Index");
                }

                // Check if the user's marketplace status is actually "Rejected"
                if (user.MarkerPlaceStatus != MarketplaceStatus.Rejected)
                {
                    TempData["ErrorMessage"] = "You can only reset when your application has been rejected.";
                    return RedirectToAction("Index");
                }

                // Get existing additional info
                var additionalInfo = await _context.UsersAdditionInfo.FirstOrDefaultAsync(a => a.UserId == businessOwnerId);
                
                // List to track files to delete
                var filesToDelete = new List<string>();
                
                // If additional info exists, collect file paths to delete
                if (additionalInfo != null)
                {
                    if (!string.IsNullOrEmpty(additionalInfo.BrgyClearancePath))
                        filesToDelete.Add(additionalInfo.BrgyClearancePath);
                        
                    if (!string.IsNullOrEmpty(additionalInfo.BusinessOwnerValidId))
                        filesToDelete.Add(additionalInfo.BusinessOwnerValidId);
                        
                    if (!string.IsNullOrEmpty(additionalInfo.SecCertPath))
                        filesToDelete.Add(additionalInfo.SecCertPath);
                        
                    if (!string.IsNullOrEmpty(additionalInfo.DtiCertPath))
                        filesToDelete.Add(additionalInfo.DtiCertPath);
                        
                    if (!string.IsNullOrEmpty(additionalInfo.OperationToOperate))
                        filesToDelete.Add(additionalInfo.OperationToOperate);
                        
                    // Remove existing record
                    _context.UsersAdditionInfo.Remove(additionalInfo);
                }
                
                // Add business permit to delete list
                if (!string.IsNullOrEmpty(user.BusinessPermitPath))
                {
                    filesToDelete.Add(user.BusinessPermitPath);
                    user.BusinessPermitPath = string.Empty;
                }
                
                // Set status to NotApplied so user can reapply
                user.MarkerPlaceStatus = MarketplaceStatus.NotApplied;
                _context.Users.Update(user);
                
                // Save changes to database
                await _context.SaveChangesAsync();
                
                // Delete the physical files
                foreach (var filePath in filesToDelete)
                {
                    try 
                    {
                        var physicalPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
                        if (System.IO.File.Exists(physicalPath))
                        {
                            System.IO.File.Delete(physicalPath);
                            _logger.LogInformation("Deleted file: {FilePath}", physicalPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other files
                        _logger.LogWarning(ex, "Error deleting file {FilePath}", filePath);
                    }
                }
                
                // Send notification to user
                var emailBody = $@"
                    <h3>Marketplace Application Reset</h3>
                    <p>Hello {user.FirstName},</p>
                    <p>Your marketplace application has been reset successfully. You can now submit new documents for validation.</p>
                    <p>Please ensure that you upload all required documents correctly to avoid rejection.</p>
                    <p>If you have any questions, please contact our support team.</p>";
                    
                await _emailService.SendEmail(
                    user.Email,
                    "Marketplace Application Reset",
                    emailBody,
                    true);
                    
                TempData["SuccessMessage"] = "Your marketplace application has been reset. You can now submit new documents.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting marketplace validation for user");
                TempData["ErrorMessage"] = "An error occurred while resetting your application. Please try again.";
                return RedirectToAction("Index");
            }
        }
    }
}