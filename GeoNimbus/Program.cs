namespace GeoNimbus {

    public class Program {

        public static async Task Main(string[] args) {
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.ConfigureLogging(logging => {
                logging.ClearProviders();
                logging.AddConsole();
            });
            // Configure configuration sources
            builder.Configuration
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false) // Base settings
                .AddJsonFile($"appsettings.{System.Environment.MachineName}.json", optional: true, reloadOnChange: false) // Machine-specific settings
                .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false) // Environment-specific settings
                .AddEnvironmentVariables() // Environment variables
                .AddCommandLine(args); // Command-line arguments

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var dm = new DependencyManagement();
            dm.RegisterDependancies(builder);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment()) {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            await dm.FinalizeDependancies(app);

            app.Run();
        }
    }
}