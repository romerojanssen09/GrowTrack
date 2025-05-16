using Microsoft.EntityFrameworkCore;
using Project_Creation.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Http.Features;
using Project_Creation.Controllers;
using ServiceStack;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("ProjectCreationDB") ??
    throw new InvalidOperationException("Connection string 'ProjectCreationDB' not found.");

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddLogging();
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ProjectCreationDB")));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Login";
        options.AccessDeniedPath = "/Login/AccessDenied";
        options.SlidingExpiration = true;
        // Only persist cookies when "Remember Me" is checked
        options.Cookie.IsEssential = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Add("/Views/Pages/{0}" + RazorViewEngine.ViewExtension);
});
builder.Services.AddAuthorization();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 20971520; // 20MB
});
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddTransient<IEmailService, HardcodedEmailService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICampaignTracker, CampaignTracker>();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exceptionHandler = context.Features.Get<IExceptionHandlerFeature>();

        logger.LogError(exceptionHandler.Error, "Unhandled exception occurred");

        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("An unexpected error occurred. Please try again later.");
    });
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UsePathBase("/Project_Creation");
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.MapHub<RealTimeHub>("/realtimehub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
