using System.Text;
using System.Text.Json;
using ECommerce.Shared;
using Microsoft.Extensions.Options;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ECommerce.InventoryConsumer;

public class InventoryService
{
    private readonly RabbitMqConnection _connection;
    private readonly RetryPolicyOptions _retryPolicyOptions;

    public InventoryService(RabbitMqConnection connection, IOptions<RetryPolicyOptions> retryPolicyOptions)
    {
        _connection = connection;
        _retryPolicyOptions = retryPolicyOptions.Value;
    }

    public void StartProcessing()
    {
        const string exchangeName = "ECommerceOrderExchange";
        const string dlqExchangeName = "ECommerceDLQExchange";
        const string queueName = "ECommerceOrderQueue";
        const string dlqQueueName = "ECommerceDLQQueue";
        const string routingKey = "ECommerceOrderKey";
        const string dlqRoutingKey = "ECommerceOrderKey";

        using var channel = _connection.CreateConnection();
        channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, durable: true);
        channel.ExchangeDeclare(dlqExchangeName, ExchangeType.Direct, durable: true);

        var arguments = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", dlqExchangeName },
            { "x-dead-letter-routing-key", dlqRoutingKey },
        };

        channel.QueueDeclare(queueName, true, false, false, arguments);
        channel.QueueDeclare(dlqQueueName, true, false, false, null);

        channel.QueueBind(queueName, exchangeName, routingKey);
        channel.QueueBind(dlqQueueName, dlqExchangeName, dlqRoutingKey);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = JsonSerializer.Deserialize<OrderPlaced>(body);
                var msg = Encoding.UTF8.GetString(body);
                Console.WriteLine($"Message received: {message}");
                
                //Apply retry policy
                var retryPolicy = Policy
                    .Handle<Exception>()
                    .WaitAndRetry(_retryPolicyOptions.RetryCount,
                        retryAttempt => TimeSpan.FromSeconds(_retryPolicyOptions.RetryIntervalInSeconds),
                        (exception, timeSpan, retryCount, context) =>
                        {
                            Console.WriteLine(
                                $"Retry {retryCount} failed after {timeSpan.TotalSeconds}'s: {exception}");
                        });
                retryPolicy.Execute(() =>
                {
                    Console.WriteLine($"Checking Inventory for order {message!.Id}");
                    var isInventoryAvailable = IsInventoryAvailable(message);
                    if (!isInventoryAvailable)
                        throw new Exception($"Inventory check failed for order {message!.Id}");
                    Console.WriteLine($"Successfully inventory check for order {message!.Id}");
                });
                
                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Message processing failed: {ex.Message}");
                channel.BasicReject(ea.DeliveryTag, false);
                throw;
            }
        };
        
        channel.BasicConsume(queueName, false, consumer);
        Console.WriteLine($"Consumer (Inventory) is now listening for message");
        Console.ReadLine();
    }

    private bool IsInventoryAvailable(OrderPlaced message)
    {
        return true;
    }
}