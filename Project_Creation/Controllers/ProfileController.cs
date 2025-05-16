using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Project_Creation.Data;
using Project_Creation.DTO;
using System.IO;
using System.Linq;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Project_Creation.Models.Entities;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using Microsoft.AspNetCore.Authentication;
using BCrypt.Net;
using NHibernate.Cfg;
using System.Data.Common;
using static Project_Creation.Models.Entities.Users;

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
                        a.OperationToOperate
                    FROM Users u
                    LEFT JOIN UsersAdditionInfo a ON u.Id = a.UserId
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
                            IsAllowedToMarketPlace = user.MarkerPlaceStatus.ToString(),

                            // Additional info properties
                            BrgyClearancePath = additionalData.BrgyClearancePath,
                            IsAllowEditBrgyClearance = additionalData.IsAllowEditBrgyClearance,
                            BusinessOwnerValidId = additionalData.BusinessOwnerValidId,
                            IsAllowEditBusinessOwnerValidId = additionalData.IsAllowEditBusinessOwnerValidId,
                            SecCertPath = additionalData.SecCertPath,
                            IsAllowEditSecCertPath = additionalData.IsAllowEditSecCertPath,
                            DtiCertPath = additionalData.DtiCertPath,
                            IsAllowEditDtiCertPath = additionalData.IsAllowEditDtiCertPath,
                            OperationToOperate = additionalData.OperationToOperate
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
            return new Users
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
                return View("Index", model);
            }

            // Get the current user's ID from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            // Parse userId as integer
            if (!int.TryParse(userId, out int businessOwnerId))
            {
                TempData["ErrorMessage"] = "Invalid user ID format.";
                return RedirectToAction("Dashboard", "Pages");
            }

            // Handle file upload for business permit if provided
            string businessPermitPath = model.BusinessPermitPath;
            if (model.BusinessPermitFile != null && model.BusinessPermitFile.Length > 0)
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
            }

            // Use raw SQL query with parameters
            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT * FROM Users WHERE Id = @Id";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@Id";
                parameter.Value = businessOwnerId;
                command.Parameters.Add(parameter);

                _context.Database.OpenConnection();

                using (var result = command.ExecuteReader())
                {
                    if (!result.HasRows)
                    {
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
                        //ScopeOfBusiness = result.IsDBNull(result.GetOrdinal("ScopeOfBusiness")) ? string.Empty : result.GetString(result.GetOrdinal("ScopeOfBusiness"))
                    };

                    // Update user properties
                    user.FirstName = model.FirstName;
                    user.LastName = model.LastName;
                    user.PhoneNumber = model.PhoneNumber;
                    user.BusinessName = model.BusinessName;
                    user.BusinessAddress = model.BusinessAddress;
                    user.DTIReqistrationNumber = model.DTIRegistrationNumber;
                    user.NumberOfBusinessYearsOperation = model.NumberOfBusinessYearsOperation;
                    user.CompanyBackground = model.CompanyBackground;

                    // Only update business permit path if a new file was uploaded
                    if (model.BusinessPermitFile != null && model.BusinessPermitFile.Length > 0)
                    {
                        user.BusinessPermitPath = businessPermitPath;
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

                    try
                    {
                        // Create parameters dictionary
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

                        // Create direct command for better control over execution
                        using (var connection = _context.Database.GetDbConnection())
                        {
                            if (connection.State != System.Data.ConnectionState.Open)
                            {
                                connection.Open();
                            }

                            using (var updateCommand = connection.CreateCommand())
                            {
                                updateCommand.CommandText = updateSql;

                                // Add parameters to command
                                foreach (var param in parameters)
                                {
                                    var sqlParameter = updateCommand.CreateParameter();
                                    sqlParameter.ParameterName = param.Key;
                                    sqlParameter.Value = param.Value;
                                    updateCommand.Parameters.Add(sqlParameter);
                                }

                                // Log the SQL and parameters for debugging
                                string debugInfo = updateSql + " Parameters: ";
                                foreach (var param in updateCommand.Parameters)
                                {
                                    var p = param as Microsoft.Data.SqlClient.SqlParameter;
                                    if (p != null)
                                    {
                                        debugInfo += $"{p.ParameterName}={p.Value}, ";
                                    }
                                }
                                System.Diagnostics.Debug.WriteLine(debugInfo);

                                // Execute the command and get rows affected
                                var rowsAffected = updateCommand.ExecuteNonQuery();

                                if (rowsAffected > 0)
                                {
                                    // Create a detailed success message that confirms which fields were updated
                                    var updatedFields = new List<string>();
                                    if (!string.IsNullOrEmpty(model.FirstName)) updatedFields.Add("First Name");
                                    if (!string.IsNullOrEmpty(model.LastName)) updatedFields.Add("Last Name");
                                    if (!string.IsNullOrEmpty(model.PhoneNumber)) updatedFields.Add("Phone Number");
                                    if (!string.IsNullOrEmpty(model.BusinessName)) updatedFields.Add("Business Name");
                                    if (!string.IsNullOrEmpty(model.BusinessAddress)) updatedFields.Add("Business Address");
                                    if (!string.IsNullOrEmpty(model.DTIRegistrationNumber)) updatedFields.Add("DTI Registration Number");
                                    if (!string.IsNullOrEmpty(model.NumberOfBusinessYearsOperation)) updatedFields.Add("Years in Operation");
                                    if (!string.IsNullOrEmpty(model.CompanyBackground)) updatedFields.Add("Company Background");
                                    if (model.BusinessPermitFile != null) updatedFields.Add("Business Permit");

                                    var fieldsUpdated = string.Join(", ", updatedFields);
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
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // Log exception but don't prevent successful update
                                        System.Diagnostics.Debug.WriteLine($"Error updating user claims: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    TempData["WarningMessage"] = "No changes were made to your profile. Please make sure you've changed at least one field before saving.";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        TempData["ErrorMessage"] = $"Error updating profile: {ex.Message}";
                    }

                    return RedirectToAction(nameof(Index));
                }
            }
        }

        public IActionResult Settings()
        {
            // Get email from claims
            var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? string.Empty;

            // Create settings view model
            var settingsViewModel = new SettingsViewModel
            {
                Email = email
            };

            return View(settingsViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? string.Empty;
                var settingsViewModel = new SettingsViewModel
                {
                    Email = email
                };
                TempData["ErrorMessage"] = "Please correct the errors and try again.";
                return View("Settings", settingsViewModel);
            }

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
                                TempData["ErrorMessage"] = "User not found.";
                                return RedirectToAction("Settings");
                            }
                        }
                    }

                    // Verify current password
                    passwordVerified = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, storedHashedPassword);

                    if (!passwordVerified)
                    {
                        TempData["ErrorMessage"] = "Current password is incorrect.";
                        return RedirectToAction("Settings");
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
                            TempData["SuccessMessage"] = "Password changed successfully! Please use your new password the next time you log in.";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Failed to update password. Please try again.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return RedirectToAction("Settings");
            }

            return RedirectToAction(nameof(Settings));
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

                var existingUser = await _context.Users.FindAsync(model.UserId);
                if (existingUser == null)
                {
                    _logger.LogError("User not found");
                    TempData["ErrorMessage"] = "User Not Found!";
                    return RedirectToAction("Login", "Login");
                }

                if (BusinessPermitFile != null && BusinessPermitFile.Length > 0)
                {
                    // Save the file and get the file path
                    string savedFilePath = await SaveFile(model.UserId.ToString(), BusinessPermitFile, "BusinessPermits");
                    existingUser.BusinessPermitPath = savedFilePath;
                }
                
                // Assign the saved file path to the BusinessPermitPath property
                existingUser.MarkerPlaceStatus = MarketplaceStatus.Requesting;
                _logger.LogError("existingUser.IsAllowedToMarketPlace = " + existingUser.MarkerPlaceStatus);

                _context.Users.Update(existingUser);
                await _context.SaveChangesAsync();

                _context.UsersAdditionInfo.Add(additionalInfo);

                // Save all changes
                await _context.SaveChangesAsync();

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
                        "Marketplace Validation Submission Received",
                        userEmailBody,
                        true);

                    // Send notification to admin
                    var adminEmail = _configuration["AdminSettings:Email"];
                    var adminEmailBody = $@"
                        <h3>New Marketplace Validation Submission</h3>
                        <p>User Details:</p>
                        <ul>
                            <li>Name: {user.FirstName} {user.LastName}</li>
                            <li>Email: {user.Email}</li>
                            <li>Business: {user.BusinessName}</li>
                        </ul>
                        <p>Please review the submitted documents in the admin panel.</p>";

                    await _emailService.SendEmail(
                        adminEmail,
                        "New Marketplace Validation Submission Needs Review",
                        adminEmailBody,
                        true);
                }

                _logger.LogInformation("Successfully processed marketplace validation for user {UserId}", model.UserId);
                TempData["SuccessMessage"] = "Documents submitted successfully! You'll receive an email once verification is complete.";
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
    }
}