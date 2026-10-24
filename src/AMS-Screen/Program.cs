using System;
using System.Diagnostics;

using BambuDashboard;

var builder = WebApplication.CreateBuilder(args);

// Bind the "BambuPrinter" section from appsettings.json to our options class
builder.Services.Configure<BambuPrinterOptions>(builder.Configuration.GetSection("BambuPrinter"));

builder.Services.AddSignalR();
builder.Services.AddHostedService<BambuMqttService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<PrinterHub>("/printerHub");

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStarted.Register(() =>
{
    if (OperatingSystem.IsLinux())
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-c \"unclutter -idle 0.1 -root & DISPLAY=:0 chromium-browser --kiosk http://localhost:5000\"",
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error launching browser: {ex.Message}");
        }
    }
});

app.Run();