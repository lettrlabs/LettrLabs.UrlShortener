using LettrLabs.UrlShorterner.Core.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace LettrLabs.UrlShorterner
{
    public class Program
    {
        public static void Main()
        {
            ShortenerSettings shortenerSettings = null;

            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices((context, services) =>
                {
                    // Add our global configuration instance
                    services.AddSingleton(_ =>
                    {
                        var configuration = context.Configuration;
                        shortenerSettings = new ShortenerSettings();
                        configuration.Bind(shortenerSettings);
                        return configuration;
                    });

                    // Add our configuration class
                    services.AddSingleton(_ => shortenerSettings);
                    services.AddSingleton(_ => new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    services.AddScoped(options => new StorageTableHelper(options.GetRequiredService<ShortenerSettings>().DataStorage));
                })
                .Build();

            host.Run();
        }
    }
}