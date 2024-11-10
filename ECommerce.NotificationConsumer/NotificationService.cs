using System.Text;
using System.Text.Json;
using ECommerce.Shared;
using Microsoft.Extensions.Options;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ECommerce.NotificationConsumer;

public class NotificationService
{
    private readonly RabbitMqConnection _connection;
    private readonly RetryPolicyOptions _retryPolicyOptions;

    public NotificationService(RabbitMqConnection connection, IOptions<RetryPolicyOptions> retryPolicyOptions)
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
                Console.WriteLine($"Sending notification received for order: {message}");
                
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
                    Console.WriteLine($"Sending notification for order {message!.Id}");
                    var isSuccessfullyNotificationSend = IsSuccessfullyNotificationSend(message);
                    if (!isSuccessfullyNotificationSend)
                        throw new Exception($"Notification sending failed for {message!.Id}");
                    Console.WriteLine($"Notification sent for order {message!.Id} successfully");
                });
                
                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sending notification processing failed: {ex.Message}");
                channel.BasicReject(ea.DeliveryTag, false);
                throw;
            }
        };
        
        channel.BasicConsume(queueName, false, consumer);
        Console.WriteLine($"Consumer (Notification) is now listening for message");
        Console.ReadLine();
    }

    private bool IsSuccessfullyNotificationSend(OrderPlaced message)
    {
        return false;
    }
}