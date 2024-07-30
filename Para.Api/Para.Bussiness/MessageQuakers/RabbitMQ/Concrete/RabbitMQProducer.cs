using Hangfire;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Para.Api.Jobs;
using Para.Bussiness.MessageQuakers.RabbitMQ.Abstract;
using Para.Bussiness.Notification;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Para.Bussiness.MessageQuakers.RabbitMQ.Concrete
{
    public class RabbitMQProducer : IMessageProducer
    {
        public void SendMessage<T>(T message)
        {

            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "mail",
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);
                var json = JsonConvert.SerializeObject(message);
                var body = Encoding.UTF8.GetBytes(json);

                channel.BasicPublish(exchange: "",
                                     routingKey: "mail",
                                     basicProperties: null,
                                     body: body);
            }
        }

    }
}
