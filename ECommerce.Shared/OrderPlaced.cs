using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ECommerce.Shared;

public class OrderPlaced
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public Guid CustomerId { get; set; }
}

public class RabbitMqOptions
{
    public string HostName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = string.Empty;
}

public class RetryPolicyOptions
{
    public int RetryCount { get; set; }
    public int RetryIntervalInSeconds { get; set; }
}

public record OrderDto(Guid Id, decimal Amount, Guid CustomerId);


public class RabbitMqConnection
{
    private readonly RabbitMqOptions _options;

    public RabbitMqConnection(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public IModel CreateConnection()
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            UserName = _options.Username,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        var connection = factory.CreateConnection();
        return connection.CreateModel();
    }
}