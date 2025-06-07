using Microsoft.Extensions.DependencyInjection;
using Project_Creation.Data;

namespace YourNamespace
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IEmailService, EmailService>();
        }
    }
} 