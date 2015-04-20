﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ServiceBus.Messaging;

namespace Obvs.AzureServiceBus.Infrastructure
{
    public interface IMessagingFactory
    {
        IMessageSender CreateMessageSender(string entityPath);

        IMessageReceiver CreateMessageReceiver(string entityPath);
    }
}
