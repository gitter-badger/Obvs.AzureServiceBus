﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using Obvs.AzureServiceBus.Infrastructure;
using Obvs.MessageProperties;
using Obvs.Serialization;
using Obvs.Types;

namespace Obvs.AzureServiceBus
{
    public class MessageSource<TMessage> : IMessageSource<TMessage> 
        where TMessage : IMessage
    {
        private IObservable<BrokeredMessage> _brokeredMessages;
        private Dictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private bool _shouldAutoCompleteMessages;
        private CancellationTokenSource _messageReceiverBrokeredMessageObservableCancellationTokenSource;

        public MessageSource(IMessageReceiver messageReceiver, IEnumerable<IMessageDeserializer<TMessage>> deserializers)
            : this(messageReceiver, deserializers, true)
        {
        }

        public MessageSource(IMessageReceiver messageReceiver, IEnumerable<IMessageDeserializer<TMessage>> deserializers, bool shouldAutoCompleteMessages)
        {
            if(messageReceiver == null) throw new ArgumentNullException("messageReceiver");
            if(shouldAutoCompleteMessages && messageReceiver.Mode != ReceiveMode.PeekLock) throw new ArgumentException("Auto-completion of messages is only supported for ReceiveMode of PeekLock.", "shouldAutoCompleteMessages");

            IObservable<BrokeredMessage> brokeredMessages = CreateBrokeredMessageObservableFromMessageReceiver(messageReceiver);

            Initialize(brokeredMessages, deserializers, shouldAutoCompleteMessages);
        }

        public MessageSource(IObservable<BrokeredMessage> brokeredMessages, IEnumerable<IMessageDeserializer<TMessage>> deserializers)
            : this(brokeredMessages, deserializers, shouldAutoCompleteMessages: false)
        {
        }

        public MessageSource(IObservable<BrokeredMessage> brokeredMessages, IEnumerable<IMessageDeserializer<TMessage>> deserializers, bool shouldAutoCompleteMessages)
        {
            Initialize(brokeredMessages, deserializers, shouldAutoCompleteMessages);
        }

        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(o =>
                    {
                        return (from bm in _brokeredMessages
                                where IsCorrectMessageType(bm)
                                select new
                                {
                                    BrokeredMessage = bm,
                                    DeserializedMessage = Deserialize(bm)
                                })
                            .Subscribe(
                                messageParts =>
                                {
                                    o.OnNext(messageParts.DeserializedMessage);

                                    if(_shouldAutoCompleteMessages)
                                    {
                                        AutoCompleteBrokeredMessage(messageParts.BrokeredMessage);
                                    }
                                },
                                o.OnError,
                                o.OnCompleted);
                    });
            }
        }

        public void Dispose()
        {
            if(_messageReceiverBrokeredMessageObservableCancellationTokenSource != null)
            {
                _messageReceiverBrokeredMessageObservableCancellationTokenSource.Dispose();
                _messageReceiverBrokeredMessageObservableCancellationTokenSource = null;
            }
        }

        private IObservable<BrokeredMessage> CreateBrokeredMessageObservableFromMessageReceiver(IMessageReceiver messageReceiver)
        {
            _messageReceiverBrokeredMessageObservableCancellationTokenSource = new CancellationTokenSource();
            
            IObservable<BrokeredMessage> brokeredMessages = Observable.Create<BrokeredMessage>(async (observer, cancellationToken) =>
            {
                while(!messageReceiver.IsClosed
                            &&
                        !cancellationToken.IsCancellationRequested
                            &&
                       !_messageReceiverBrokeredMessageObservableCancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        BrokeredMessage nextMessage = await messageReceiver.ReceiveAsync();

                        if(nextMessage != null)
                        {
                            observer.OnNext(nextMessage);
                        }
                    }
                    catch(Exception exception)
                    {
                        observer.OnError(exception);
                    }
                }
                
                return Disposable.Empty;
            });

            return brokeredMessages.Publish().RefCount();
        }

        private void Initialize(IObservable<BrokeredMessage> brokeredMessages, IEnumerable<IMessageDeserializer<TMessage>> deserializers, bool shouldAutoCompleteMessages)
        {
            if(brokeredMessages == null) throw new ArgumentNullException("brokeredMessages");
            if(deserializers == null) throw new ArgumentNullException("deserializers");

            _brokeredMessages = brokeredMessages;
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _shouldAutoCompleteMessages = shouldAutoCompleteMessages;
        }

        private bool IsCorrectMessageType(BrokeredMessage brokeredMessage)
        {
            object messageTypeName;
            bool messageTypeMatches = brokeredMessage.Properties.TryGetValue(MessagePropertyNames.TypeName, out messageTypeName);

            if(messageTypeMatches)
            {
                messageTypeMatches = _deserializers.ContainsKey((string)messageTypeName);
            }

            return messageTypeMatches;
        }

        private TMessage Deserialize(BrokeredMessage message)
        {
            object messageTypeName;
            IMessageDeserializer<TMessage> messageDeserializerForType;
            
            if(message.Properties.TryGetValue(MessagePropertyNames.TypeName, out messageTypeName))
            {
                messageDeserializerForType = _deserializers[(string)messageTypeName];
            }
            else
            {
                try
                {
                    messageDeserializerForType = _deserializers.Values.Single();
                }
                catch(InvalidOperationException exception)
                {
                    throw new Exception("The message contained no explicit TypeName property. In this scenario there must be a single deserializer provided.", exception);
                }
            }
            
            TMessage deserializedMessage = messageDeserializerForType.Deserialize(message.GetBody<Stream>());

            return deserializedMessage;
        }

        private static void AutoCompleteBrokeredMessage(BrokeredMessage brokeredMessage)
        {
            brokeredMessage.CompleteAsync().ContinueWith(completeAntecedent =>
            {
                // TODO: figure out how to get an ILogger in here and log failures
            },
            TaskContinuationOptions.OnlyOnFaulted);
        }

    }
}
