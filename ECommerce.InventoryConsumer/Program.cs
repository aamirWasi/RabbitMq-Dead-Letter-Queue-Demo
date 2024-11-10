using ECommerce.Shared;
using Microsoft.Extensions.Options;

namespace ECommerce.InventoryConsumer;

class Program
{
    static void Main(string[] args)
    {
        var rabbitMqOptions = Options.Create(new RabbitMqOptions
        {
            HostName = "localhost",
            Username = "admin",
            Password = "admin",
            VirtualHost = "/ecommerce_vhost"
        });

        var rabbitMqConnection = new RabbitMqConnection(rabbitMqOptions);
        var retryPolicyOptions = Options.Create(new RetryPolicyOptions
        {
            RetryCount = 3,
            RetryIntervalInSeconds = 5
        });

        var inventoryService = new InventoryService(rabbitMqConnection, retryPolicyOptions);
        inventoryService.StartProcessing();
        Console.ReadLine();
    }
}