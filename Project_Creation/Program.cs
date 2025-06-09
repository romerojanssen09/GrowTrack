using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Http.Features;
using Project_Creation.Controllers;
using ServiceStack;
using Microsoft.AspNetCore.Diagnostics;
using Project_Creation.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.DataProtection;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("ProjectCreationDB") ??
    throw new InvalidOperationException("Connection string 'ProjectCreationDB' not found.");

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR(options => {
    options.EnableDetailedErrors = true;
}).AddJsonProtocol(options => {
    options.PayloadSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
    options.PayloadSerializerOptions.MaxDepth = 32;
    options.PayloadSerializerOptions.PropertyNamingPolicy = null;
});
builder.Services.AddHttpClient();
builder.Services.AddLogging();

// Add CORS policy for API endpoints
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add session support with correct cookie settings
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "ProjectCreation.Session";
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
});

// Test the connection before configuring the DbContext
try
{
    var connString = builder.Configuration.GetConnectionString("ProjectCreationDB");
    using (var connection = new SqlConnection(connString))
    {
        connection.Open();
        Console.WriteLine("Database connection successful!");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Database connection test failed: {ex.Message}");
}

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ProjectCreationDB"),
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

// Configure authentication with correct cookie settings
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.Cookie.Name = "GrowTrack.Auth";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.LoginPath = "/Login/Login";
        options.AccessDeniedPath = "/Login/AccessDenied";
        options.Cookie.Path = "/";
        
        // Handle redirects properly
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToAccessDenied = context => {
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToLogin = context => {
                // Check if request is for admin area
                if (context.Request.Path.StartsWithSegments("/Admin"))
                {
                    context.Response.Redirect("/Login/AdminLogin");
                }
                else
                {
                    context.Response.Redirect(context.RedirectUri);
                }
                return Task.CompletedTask;
            },
            // Add detailed error logging for cookie validation failures
            OnValidatePrincipal = async context => {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                try
                {
                    // Log successful ticket validation
                    logger.LogInformation("Auth cookie validation successful");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error validating authentication ticket: {Message}", ex.Message);
                    context.RejectPrincipal();
                }
            }
        };
    });

// Configure data protection for somee.com hosting - use in-memory keys
builder.Services.AddDataProtection()
    .SetApplicationName("Project_Creation")
    // Use in-memory key ring for somee.com compatibility
    .UseEphemeralDataProtectionProvider()
    .SetDefaultKeyLifetime(TimeSpan.FromDays(30));

builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Add("/Views/Pages/{0}" + RazorViewEngine.ViewExtension);
});
builder.Services.AddAuthorization();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 20971520; // 20MB
});
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICampaignTracker, CampaignTracker>();

// Comment out this line until we fix the CalendarNotificationService implementation
// builder.Services.AddHostedService<CalendarNotificationService>();

var cultureInfo = new CultureInfo("en-PH");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    try
    {
        db.Database.OpenConnection();
        db.Database.CloseConnection();
        Console.WriteLine("Database connection successful");
        
        // Update existing notifications with user type flags
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        Task.Run(async () => {
            await Project_Creation.Helpers.UpdateNotificationsHelper.UpdateExistingNotifications(configuration);
        }).Wait();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database connection failed: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Use our custom error handling middleware for detailed error information
    app.UseErrorHandling();
}

// Enable CORS
app.UseCors("AllowAll");

// Enable session middleware
app.UseSession();
    
    // Fallback error handler if middleware doesn't catch the exception
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/html";
            
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var exceptionHandler = context.Features.Get<IExceptionHandlerFeature>();
            
            if (exceptionHandler != null)
            {
                logger.LogError(exceptionHandler.Error, "Unhandled exception occurred: {Message}", 
                    exceptionHandler.Error.Message);
                
                await context.Response.WriteAsync("<html><body>")
                    .ConfigureAwait(false);
                await context.Response.WriteAsync("<h2>Application Error</h2>")
                    .ConfigureAwait(false);
                
                // Show detailed error information to help diagnose the issue
                await context.Response.WriteAsync($"<p>Error: {exceptionHandler.Error.Message}</p>")
                    .ConfigureAwait(false);
                await context.Response.WriteAsync($"<p>Source: {exceptionHandler.Error.Source}</p>")
                    .ConfigureAwait(false);
                
                if (exceptionHandler.Error.InnerException != null)
                {
                    await context.Response.WriteAsync($"<p>Inner Exception: {exceptionHandler.Error.InnerException.Message}</p>")
                        .ConfigureAwait(false);
                }
                
                await context.Response.WriteAsync("<p>Please try again later or contact support.</p>")
                    .ConfigureAwait(false);
                await context.Response.WriteAsync("</body></html>")
                    .ConfigureAwait(false);
            }
        });
    });
    
    // For somee.com hosting - handle path base correctly
    app.Use(async (context, next) =>
    {
        try 
        {
            // Store original path for debugging if needed
            var originalPath = context.Request.Path.Value;
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            
            logger.LogDebug("Request path before processing: {OriginalPath}", originalPath);
            
            // Handle Somee.com specific pathing
            if (context.Request.Path.StartsWithSegments("/Project_Creation"))
            {
                context.Request.Path = context.Request.Path.Value?.Substring("/Project_Creation".Length) ?? "/";
                logger.LogDebug("Modified request path: {ModifiedPath}", context.Request.Path);
            }
            
            // Add CORS headers for somee.com
            context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization, Cookie");
            context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
            
            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = 200;
                return;
            }
            
            // Check if we need to handle a cookie issue
            if (context.Request.Cookies.ContainsKey("GrowTrack.Auth") &&
                !context.User.Identity.IsAuthenticated)
            {
                logger.LogWarning("Auth cookie exists but user is not authenticated");
            }
            
            await next();
            
            // Log response status code for debugging
            logger.LogDebug("Response status code: {StatusCode}", context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Middleware exception: {Message}", ex.Message);
            throw; // Re-throw to be handled by the exception handler
        }
    });


app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
    }
});
//app.UseStaticFiles();

// Set consistent path base
if (!app.Environment.IsDevelopment())
{
    // For production on somee.com
    // Don't use UsePathBase as it might conflict with somee.com's configuration
    // app.UsePathBase("/");
}

// Fix middleware order for proper cookie handling
app.UseRouting();

// Session and authentication middleware must be in this order
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<RealTimeHub>("/realtimehub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
