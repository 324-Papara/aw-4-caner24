using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Net.Mail;
using System.Text;
using Newtonsoft.Json;
using System.Net;
using Para.Bussiness.Notification;

namespace Para.Api.Service
{
    public class ContinuousConsumeService
    {
        private IConnection _connection;
        private IModel _channel;
        private readonly ILogger<ContinuousConsumeService> _logger;

        public ContinuousConsumeService(ILogger<ContinuousConsumeService> logger)
        {
            _logger = logger;
            InitializeRabbitMQ();
        }

        private void InitializeRabbitMQ()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(queue: "mail",
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);
        }

        public void ProcessMessages()
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var deserializedMessage = JsonConvert.DeserializeObject<NotificationModel>(message);

                MailMessage mail = new MailMessage
                {
                    From = new MailAddress("sekerteshis@gmail.com"),
                    Subject = deserializedMessage.Subject,
                    Body = deserializedMessage.Content
                };
                mail.To.Add(deserializedMessage.Email);

                using var smtpClient = new SmtpClient("smtp.gmail.com", 587)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential("sekerteshis@gmail.com", "szny nxdb brke ieow")
                };

                try
                {
                    smtpClient.Send(mail);
                    _channel.BasicAck(ea.DeliveryTag, false); 
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(queue: "mail",
                                 autoAck: false, 
                                 consumer: consumer);
        }

        public void StopProcessing()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}
