using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

const string AppConfigurationConnectionStringEnvVarName = "AppConfigurationConnectionString";
string appConfigurationConnectionString = Environment.GetEnvironmentVariable(AppConfigurationConnectionStringEnvVarName);
IConfigurationRefresher _refresher = null;
    

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(s =>
    {
        s.AddAzureAppConfiguration(options =>
        {
            options.Connect(appConfigurationConnectionString);
            options.ConfigureRefresh(refresh =>
            {
                refresh.Register("TestApp:Settings:Message")
                .SetCacheExpiration(TimeSpan.FromSeconds(5));
            });
            options.UseFeatureFlags(f =>
            {
                f.CacheExpirationInterval = TimeSpan.FromSeconds(5);
            });

            _refresher = options.GetRefresher();
        });
    })
    .ConfigureServices(s =>
    {
        s.AddAzureAppConfiguration();
        s.AddFeatureManagement();
    })
    .ConfigureDefaults(args)
    .Build();

var configuration = builder.Services.GetService<IConfiguration>();
var features = builder.Services.GetService<IFeatureManager>();
var logger = builder.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger<Program>();

var message = configuration.GetSection("TestApp:Settings").Get<Settings>().Message;

logger.LogInformation($"Initial value: {message}");

while (true)
{
    await _refresher.TryRefreshAsync();

    if (configuration.GetSection("TestApp:Settings").Get<Settings>().Message != message)
    {
        logger.LogInformation($"New value: {configuration["TestApp:Settings:Message"]}");
        message = configuration.GetSection("TestApp:Settings").Get<Settings>().Message;
    }

    await Task.Delay(TimeSpan.FromSeconds(1));

    if (await features.IsEnabledAsync(FeatureFlags.ExitConsole))
    {
        logger.LogInformation("Feature Flag Enabled!");
        Environment.Exit(0);
    }
}

public static class FeatureFlags
{
    public const string ExitConsole = "ExitConsole";
}

public class Settings
{
    public string Message { get; set; }
}