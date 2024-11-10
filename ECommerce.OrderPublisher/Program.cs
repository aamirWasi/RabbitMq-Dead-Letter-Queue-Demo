using ECommerce.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.OrderPublisher;

class Program
{
    static void Main(string[] args)
    {
        /*var rabbitMqOptions = Options.Create(new RabbitMqOptions
        {
            Host = "localhost",
            Username = "admin",
            Password = "admin",
            VirtualHost = "/ecommerce_vhost"
        });
        
        var rabbitMqConnection = new RabbitMqConnection(rabbitMqOptions);
        var retryPolicyOptions = Options.Create(new RetryPolicyOptions
        {
            RetryCount = 3,
            RetryIntervalSeconds = 5
        });
        
        var orderService = new OrderService(rabbitMqConnection, retryPolicyOptions);
        var order = new OrderDto(Guid.NewGuid(), 150.00m,Guid.NewGuid());
        orderService.PlaceOrder(order);*/
        
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
                services.AddSingleton<OrderService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
            })
            .Build();

        var config = host.Services.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        Console.WriteLine($"RabbitMq Host: {config.HostName}, Username: {config.Username}");

        var orderService = host.Services.GetRequiredService<OrderService>();
        var order = new OrderDto(Guid.NewGuid(), 150.00m,Guid.NewGuid());
        orderService.PlaceOrder(order);
    }
}