using ECommerce.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.PaymentConsumer;

class Program
{
    static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            }).ConfigureServices((context, services) =>
            {
                services.Configure<RabbitMqOptions>(context.Configuration.GetSection("RabbitMQ"));
                services.Configure<RetryPolicyOptions>(context.Configuration.GetSection("RetryPolicy"));

                services.AddSingleton<RabbitMqConnection>();
                services.AddSingleton<PaymentService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
            })
            .Build();

        var config = host.Services.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        Console.WriteLine($"RabbitMq Host: {config.HostName}, Username: {config.Username}");

        var paymentService = host.Services.GetRequiredService<PaymentService>();
        paymentService.StartProcessing();
    }
}