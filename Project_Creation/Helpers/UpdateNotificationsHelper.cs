using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace Project_Creation.Helpers
{
    public static class UpdateNotificationsHelper
    {
        public static async Task UpdateExistingNotifications(IConfiguration configuration)
        {
            try
            {
                var connectionString = configuration.GetConnectionString("ProjectCreationDB");
                
                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.WriteLine("Connection string not found");
                    return;
                }
                
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    
                    // Update notifications based on user roles
                    string updateSql = @"
                        UPDATE n
                        SET 
                            IsForAdmin = CASE 
                                WHEN u.Role = 'Admin' THEN 1 
                                ELSE 0 
                            END,
                            IsForStaff = CASE 
                                WHEN u.Role = 'Staff' THEN 1 
                                ELSE 0 
                            END,
                            IsForBusinessOwner = CASE 
                                WHEN u.Role = 'BusinessOwner' THEN 1 
                                ELSE 0 
                            END
                        FROM Notifications n
                        INNER JOIN Users u ON n.UserId = u.Id;
                        
                        -- For notifications without a specific user type, enable them for all users
                        -- This ensures backward compatibility with existing notifications
                        UPDATE Notifications
                        SET IsForAdmin = 1, IsForStaff = 1, IsForBusinessOwner = 1
                        WHERE IsForAdmin = 0 AND IsForStaff = 0 AND IsForBusinessOwner = 0;
                    ";
                    
                    using (var command = new SqlCommand(updateSql, connection))
                    {
                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        Console.WriteLine($"Updated {rowsAffected} notification records");
                    }
                }
                
                Console.WriteLine("Notification update completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating notifications: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }
    }
} 