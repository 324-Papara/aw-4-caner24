using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Para.Bussiness.Notification
{
    public record NotificationModel
    {

        public string Subject { get; set; }
        public string Email { get; set; }

        public string Content { get; set; }



    }
}
