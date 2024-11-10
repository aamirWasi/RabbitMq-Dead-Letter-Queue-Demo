using System.Text;
using System.Text.Json;
using ECommerce.Shared;
using Microsoft.Extensions.Options;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ECommerce.PaymentConsumer;

public class PaymentService
{
    private readonly RabbitMqConnection _connection;
    private readonly RetryPolicyOptions _retryPolicyOptions;

    public PaymentService(RabbitMqConnection connection, IOptions<RetryPolicyOptions> retryPolicyOptions)
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
                Console.WriteLine($"Payment Message received: {message}");
                
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
                    Console.WriteLine($"Processing payment for order {message!.Id}");
                    var paymentSuccess = IsPaymentSufficient(message);
                    if (!paymentSuccess)
                        throw new Exception($"Payment failed for order {message!.Id}");
                    Console.WriteLine($"Payment successfully for order {message!.Id}");
                });
                
                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Payment Message processing failed: {ex.Message}");
                channel.BasicReject(ea.DeliveryTag, false);
                throw;
            }
        };
        
        channel.BasicConsume(queueName, false, consumer);
        Console.WriteLine($"Consumer (Payment) is now listening for message");
        Console.ReadLine();
    }

    private bool IsPaymentSufficient(OrderPlaced message)
    {
        return true;
    }
}