using Para.Bussiness.Notification;
using System.Net.Mail;

namespace Para.Api.Jobs
{
    public interface IJobEmailService
    {
        void DelayedJob(NotificationModel message);
    }
}
