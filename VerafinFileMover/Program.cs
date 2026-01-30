using Microsoft.EntityFrameworkCore;
using Serilog;
using VerafinFileMover.Entities;
using VerafinFileMover.Models;
using VerafinFileMover.Services;

namespace VerafinFileMover
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            var loggingOn = builder.Configuration.GetSection("AppSettings:AppWriteToFileLogging").Value;
            if (loggingOn != null)
            {
                if (bool.Parse(loggingOn))
                {
                    Log.Logger = new LoggerConfiguration()
                        .WriteTo.File(
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "service.log"),
                            fileSizeLimitBytes: 10 * 1024 * 1024,
                            rollOnFileSizeLimit: true
                        )
                        .CreateLogger();
                }
            }


            builder.Services.AddHostedService<Worker>();

            builder.Services.AddWindowsService(opts =>
            {
                opts.ServiceName = builder.Configuration.GetSection("AppSettings:ServiceName").Value!;

            });

            builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
            builder.Services.AddDbContext<VerafinFileMoverContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("DbCnn"));
            });

            builder.Services.AddHttpClient<ILogService, LogService>(client =>
            {
                client.BaseAddress = new Uri(builder.Configuration.GetSection("AppSettings:LogUrl").Value!);

            });
            builder.Services.AddSerilog();
            builder.Services.AddSingleton<IFileMoverService, FileMoverService>();

            var host = builder.Build();
            host.Run();
        }
    }
}
