using System.Text;
using System.Text.Json;
using ECommerce.Shared;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ECommerce.OrderPublisher;

public class OrderService
{
    private readonly RabbitMqConnection _connection;
    private readonly RetryPolicyOptions _retryPolicyOptions;

    public OrderService(RabbitMqConnection connection, IOptions<RetryPolicyOptions> retryPolicyOptions)
    {
        _connection = connection;
        _retryPolicyOptions = retryPolicyOptions.Value;
    }

    public void PlaceOrder(OrderDto order)
    {
        using var channel = _connection.CreateConnection();
        
        const string exchangeName = "ECommerceOrderExchange";
        const string dlqExchangeName = "ECommerceDLQExchange";
        const string queueName = "ECommerceOrderQueue";
        const string routingKey = "ECommerceOrderKey";
        const string dlqRoutingKey = "ECommerceOrderKey";
        
        channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, durable: true);
        
        var arguments = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", dlqExchangeName },
            { "x-dead-letter-routing-key", dlqRoutingKey },
        };
        
        channel.QueueDeclare(queueName, true, false, false, arguments);
        channel.QueueBind(queueName, exchangeName, routingKey);

        OrderPlaced message = new()
        {
            Id = order.Id,
            Amount = order.Amount,
            CustomerId = order.CustomerId
        };
        
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        
        channel.BasicPublish(exchangeName,routingKey, properties, body);
        Console.WriteLine($"Order {order.Id} placed");
    }
}