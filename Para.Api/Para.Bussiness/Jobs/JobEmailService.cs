using Hangfire;
using Para.Bussiness.MessageQuakers.RabbitMQ.Abstract;
using Para.Bussiness.Notification;
using System.Net.Mail;

namespace Para.Api.Jobs
{
    public class JobEmailService : IJobEmailService
    {
        private readonly IBackgroundJobClient _backgroundJobService;
        private readonly IMessageProducer _notificationServiceConsumer;
        public JobEmailService(IBackgroundJobClient backgroundJobService, IMessageProducer notificationServiceConsumer)
        {
            _notificationServiceConsumer = notificationServiceConsumer;
            _backgroundJobService = backgroundJobService;
        }
        public void DelayedJob(NotificationModel message)
        {
            _backgroundJobService.Schedule(() => _notificationServiceConsumer.SendMessage<NotificationModel>(message), TimeSpan.FromSeconds(5));
        }
    }
}
