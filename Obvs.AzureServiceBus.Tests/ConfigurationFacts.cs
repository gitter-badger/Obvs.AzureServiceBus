﻿using System;
using FluentAssertions;
using Microsoft.ServiceBus;
using Moq;
using Obvs.AzureServiceBus.Configuration;
using Obvs.AzureServiceBus.Infrastructure;
using Obvs.Serialization;
using Obvs.Types;
using Xunit;

namespace Obvs.AzureServiceBus.Tests
{
    public class ConfigurationFacts
    {
        private readonly Mock<INamespaceManager> _mockNamespaceManager;
        private readonly Mock<IMessagingFactory> _mockMessagingFactory;
        private readonly Mock<IMessageSerializer> _mockMessageSerializer;
        private readonly Mock<IMessageDeserializerFactory> _mockMessageDeserializerFactory;

        public ConfigurationFacts()
        {
            _mockNamespaceManager = new Mock<INamespaceManager>();
            _mockNamespaceManager.Setup(nsm => nsm.QueueExists(It.IsAny<string>()))
                .Returns(true);
            _mockNamespaceManager.Setup(nsm => nsm.TopicExists(It.IsAny<string>()))
                .Returns(true);
            _mockNamespaceManager.Setup(nsm => nsm.SubscriptionExists(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            _mockMessagingFactory = new Mock<IMessagingFactory>();

            _mockMessagingFactory.Setup(mf => mf.CreateMessageReceiver(It.IsAny<string>(), It.IsAny<MessageReceiveMode>()))
                .Returns(new Mock<IMessageReceiver>().Object);
            _mockMessagingFactory.Setup(mf => mf.CreateMessageSender(It.IsAny<string>()))
                .Returns(new Mock<IMessageSender>().Object);

            _mockMessageSerializer = new Mock<IMessageSerializer>();
            _mockMessageDeserializerFactory = new Mock<IMessageDeserializerFactory>();
        }

        public class NamespaceConfigurationFacts : ConfigurationFacts
        {
            [Fact]
            public void ConfigureAzureServiceBusEndpointWithNullConnectionStringThrows()
            {
                Action action = () => ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithConnectionString(null);

                action.ShouldThrow<ArgumentNullException>();
            }

            [Fact]
            public void ConfigureAzureServiceBusEndpointWithNullINamespaceManagerThrows()
            {
                Action action = () => ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager((INamespaceManager)null);

                action.ShouldThrow<ArgumentNullException>();
            }

            [Fact]
            public void ConfigureAzureServiceBusEndpointWithNullNamespaceManagerThrows()
            {
                Action action = () => ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager((NamespaceManager)null);

                action.ShouldThrow<ArgumentNullException>();
            }
        }

        public class MessageTypeConfigurationFacts : ConfigurationFacts
        {
            [Fact]
            public void ConfigureNoMessageTypesShouldThrow()
            {
                Action action = () => ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager(_mockNamespaceManager.Object)
                    .WithMessagingFactory(_mockMessagingFactory.Object)
                    .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                    .AsClientAndServer()
                    .CreateServiceBus();

                action.ShouldThrow<ArgumentException>()
                    .And.ParamName.Should().Be("messageTypePathMappings");
            }

            [Fact]
            public void ConfigureSameMessageTypeForSameRoleMoreThanOnceShouldThrow()
            {
                Action action = () => ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager(_mockNamespaceManager.Object)
                    .WithMessagingFactory(_mockMessagingFactory.Object)
                    .UsingQueueFor<ICommand>("commands")
                    .UsingQueueFor<ICommand>("commandsAgain")
                    .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                    .AsClientAndServer()
                    .CreateServiceBus();

                var exceptionAssertion = action.ShouldThrow<MoreThanOneMappingExistsForMessageTypeException>();

                exceptionAssertion.And.MessageType.Should().Be(typeof(ICommand));
                exceptionAssertion.And.ExpectedEntityTypes.Should().BeEquivalentTo(MessagingEntityType.Queue, MessagingEntityType.Topic);
            }

            [Fact]
            public void ConfigureCommandMessageTypeOnlyShouldBeAbleToSendReceiveCommands()
            {
                IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse> serviceBus = ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager(_mockNamespaceManager.Object)
                    .WithMessagingFactory(_mockMessagingFactory.Object)
                    .UsingQueueFor<ICommand>("commands")
                    .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                    .AsClientAndServer()
                    .CreateServiceBus();

                serviceBus.Should().NotBeNull();

                serviceBus.SendAsync(new TestCommand());
            }

            [Fact]
            public void SendingACommandWhenNotConfiguredAsAMessageTypeShouldThrow()
            {
                IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse> serviceBus = ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager(_mockNamespaceManager.Object)
                    .WithMessagingFactory(_mockMessagingFactory.Object)
                    .UsingQueueFor<IEvent>("events")
                    .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                    .AsClientAndServer()
                    .CreateServiceBus();

                serviceBus.Should().NotBeNull();

                Action action = () => serviceBus.SendAsync(new TestCommand()).Wait();

                action.ShouldThrow<InvalidOperationException>();
            }

            [Fact]
            public void PublishingAnEventWhenNotConfiguredAsAMessageTypeShouldThrow()
            {
                IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse> serviceBus = ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager(_mockNamespaceManager.Object)
                    .WithMessagingFactory(_mockMessagingFactory.Object)
                    .UsingQueueFor<ICommand>("commands")
                    .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                    .AsClientAndServer()
                    .CreateServiceBus();

                serviceBus.Should().NotBeNull();

                Action action = () => serviceBus.PublishAsync(new TestEvent()).Wait();

                action.ShouldThrow<InvalidOperationException>();
            }

            [Fact]
            public void ConfigureAllMessageTypes()
            {
                IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse> serviceBus = ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                        .Named("Test Service Bus")
                        .WithNamespaceManager(_mockNamespaceManager.Object)
                        .WithMessagingFactory(_mockMessagingFactory.Object)
                        .UsingQueueFor<ICommand>("commands")
                        .UsingQueueFor<IRequest>("requests")
                        .UsingQueueFor<IResponse>("responses")
                        .UsingTopicFor<IEvent>("events")
                        .UsingSubscriptionFor<IEvent>("events", "my-event-subscription")
                        .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                        .FilterMessageTypeAssemblies(assembly => assembly.GetName().Name == "Obvs.AzureServiceBus.Tests")
                        .AsClientAndServer()
                    .CreateServiceBus();

                serviceBus.Should().NotBeNull();

                _mockNamespaceManager.Verify(nsm => nsm.QueueExists("commands"), Times.Once());
                _mockNamespaceManager.Verify(nsm => nsm.QueueExists("requests"), Times.Once());
                _mockNamespaceManager.Verify(nsm => nsm.QueueExists("responses"), Times.Once());
                _mockNamespaceManager.Verify(nsm => nsm.TopicExists("events"), Times.Once());
                _mockNamespaceManager.Verify(nsm => nsm.SubscriptionExists("events", "my-event-subscription"), Times.Once());

                _mockMessagingFactory.Verify(mf => mf.CreateMessageSender("commands"), Times.Once());
                _mockMessagingFactory.Verify(mf => mf.CreateMessageReceiver("commands", MessageReceiveMode.PeekLock), Times.Once());

                _mockMessagingFactory.Verify(mf => mf.CreateMessageSender("requests"), Times.Once());
                _mockMessagingFactory.Verify(mf => mf.CreateMessageReceiver("requests", MessageReceiveMode.PeekLock), Times.Once());

                _mockMessagingFactory.Verify(mf => mf.CreateMessageSender("responses"), Times.Once());
                _mockMessagingFactory.Verify(mf => mf.CreateMessageReceiver("responses", MessageReceiveMode.PeekLock), Times.Once());

                _mockMessagingFactory.Verify(mf => mf.CreateMessageSender("events"), Times.Once());
                _mockMessagingFactory.Verify(mf => mf.CreateMessageReceiver("events", MessageReceiveMode.PeekLock), Times.Never);

                _mockMessagingFactory.Verify(mf => mf.CreateMessageSender("events/subscriptions/my-event-subscription"), Times.Never);
                _mockMessagingFactory.Verify(mf => mf.CreateMessageReceiver("events/subscriptions/my-event-subscription", MessageReceiveMode.PeekLock), Times.Once());
            }
        }

        public class ExistingMessagingEntityFacts : ConfigurationFacts
        {
            [Fact]
            public void UseExistingMessagingEntityThatDoesNotExistShouldThrow()
            {
                _mockNamespaceManager.Setup(nsm => nsm.QueueExists("commands"))
                    .Returns(false);

                Action action = () => ServiceBus.Configure()
                 .WithAzureServiceBusEndpoint<TestMessage>()
                 .Named("Test Service Bus")
                 .WithNamespaceManager(_mockNamespaceManager.Object)
                 .WithMessagingFactory(_mockMessagingFactory.Object)
                 .UsingQueueFor<ICommand>("commands")
                 .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                 .AsClientAndServer()
                 .CreateServiceBus();

                var exceptionAssertion = action.ShouldThrow<MessagingEntityDoesNotAlreadyExistException>();

                exceptionAssertion.And.Path.Should().Be("commands");
                exceptionAssertion.And.MessagingEntityType.Should().Be(MessagingEntityType.Queue);
            }

            [Fact]
            public void UseExistingMessagingEntityShouldNotTryToCreateTheMessagingEntity()
            {
                _mockNamespaceManager.Setup(nsm => nsm.QueueExists("commands"))
                    .Returns(true);

                ServiceBus.Configure()
                 .WithAzureServiceBusEndpoint<TestMessage>()
                 .Named("Test Service Bus")
                 .WithNamespaceManager(_mockNamespaceManager.Object)
                 .WithMessagingFactory(_mockMessagingFactory.Object)
                 .UsingQueueFor<ICommand>("commands", MessagingEntityCreationOptions.CreateIfDoesntExist)
                 .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                 .AsClientAndServer()
                 .CreateServiceBus();

                _mockNamespaceManager.Verify(nsm => nsm.QueueExists("commands"), Times.Once());
                _mockNamespaceManager.Verify(nsm => nsm.CreateQueue("commands"), Times.Never);
            }
        }

        public class TemporaryMessagingEntityFacts : ConfigurationFacts
        {
            [Fact]
            public void UseTemporaryMessagingEntityThatAlreadyExistsWithoutSpecifyingCanDeleteIfAlreadyExistsShouldThrow()
            {
                Action action = () => ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager(_mockNamespaceManager.Object)
                    .WithMessagingFactory(_mockMessagingFactory.Object)
                    .UsingQueueFor<ICommand>("commands", MessagingEntityCreationOptions.CreateIfDoesntExist | MessagingEntityCreationOptions.CreateAsTemporary)
                    .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                    .AsClientAndServer()
                    .CreateServiceBus();

                var exceptionAssertion = action.ShouldThrow<Obvs.AzureServiceBus.Configuration.MessagingEntityAlreadyExistsException>();

                exceptionAssertion.And.Path.Should().Be("commands");
                exceptionAssertion.And.MessagingEntityType.Should().Be(MessagingEntityType.Queue);
            }

            [Fact]
            public void UseTemporaryMessagingEntityThatAlreadyExiststSpecifyingRecreateOptionShouldRecreate()
            {
                ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager(_mockNamespaceManager.Object)
                    .WithMessagingFactory(_mockMessagingFactory.Object)
                    .UsingQueueFor<ICommand>("commands", MessagingEntityCreationOptions.CreateIfDoesntExist | MessagingEntityCreationOptions.CreateAsTemporary | MessagingEntityCreationOptions.RecreateExistingTemporary)
                    .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                    .AsClientAndServer()
                    .CreateServiceBus();

                _mockNamespaceManager.Verify(nsm => nsm.DeleteQueue("commands"), Times.Once);
                _mockNamespaceManager.Verify(nsm => nsm.CreateQueue("commands"), Times.Once);
            }

            [Fact]
            public void UseTemporarySubscriptionForTopicThatAlreadyExistsShouldCreateSubscription()
            {
                _mockNamespaceManager.Setup(nsm => nsm.TopicExists("events"))
                    .Returns(true);

                _mockNamespaceManager.Setup(nsm => nsm.SubscriptionExists("events", "test-subscription"))
                    .Returns(false);

                ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager(_mockNamespaceManager.Object)
                    .WithMessagingFactory(_mockMessagingFactory.Object)
                    .UsingSubscriptionFor<IEvent>("events", "test-subscription", MessagingEntityCreationOptions.CreateIfDoesntExist | MessagingEntityCreationOptions.CreateAsTemporary | MessagingEntityCreationOptions.RecreateExistingTemporary)
                    .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                    .AsClient()
                    .CreateServiceBusClient();

                _mockNamespaceManager.Verify(nsm => nsm.TopicExists("events"), Times.Once);
                _mockNamespaceManager.Verify(nsm => nsm.CreateSubscription("events", "test-subscription"), Times.Once);
            }

            [Fact]
            public void UseTemporarySubscriptionForTopicThatDoesntAlreadyExistThrows()
            {
                _mockNamespaceManager.Setup(nsm => nsm.TopicExists("events"))
                    .Returns(false);

                _mockNamespaceManager.Setup(nsm => nsm.SubscriptionExists("events", "test-subscription"))
                    .Returns(false);

                Action action = () => ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager(_mockNamespaceManager.Object)
                    .WithMessagingFactory(_mockMessagingFactory.Object)
                    .UsingSubscriptionFor<IEvent>("events", "test-subscription", MessagingEntityCreationOptions.CreateIfDoesntExist | MessagingEntityCreationOptions.CreateAsTemporary)
                    .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                    .AsClient()
                    .CreateServiceBusClient();

                var exceptionAssertion = action.ShouldThrow<MessagingEntityDoesNotAlreadyExistException>();

                exceptionAssertion.And.Path.Should().Be("events");
                exceptionAssertion.And.MessagingEntityType.Should().Be(MessagingEntityType.Topic);
            }

            [Fact]
            public void UseTemporarySubscriptionForTemporaryTopicShouldCreateTopicAndSubscription()
            {
                _mockNamespaceManager.Setup(nsm => nsm.TopicExists("events"))
                    .Returns(false);

                _mockNamespaceManager.Setup(nsm => nsm.SubscriptionExists("events", "test-subscription"))
                    .Returns(false);

                ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager(_mockNamespaceManager.Object)
                    .WithMessagingFactory(_mockMessagingFactory.Object)
                    .UsingTopicFor<IEvent>("events", MessagingEntityCreationOptions.CreateIfDoesntExist | MessagingEntityCreationOptions.CreateAsTemporary)
                    .UsingSubscriptionFor<IEvent>("events", "test-subscription", MessagingEntityCreationOptions.CreateIfDoesntExist | MessagingEntityCreationOptions.CreateAsTemporary)
                    .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                    .AsClient()
                    .CreateServiceBusClient();

                _mockNamespaceManager.Verify(nsm => nsm.CreateTopic("events"), Times.Once);
                _mockNamespaceManager.Verify(nsm => nsm.CreateSubscription("events", "test-subscription"), Times.Once);
            }

            [Fact]
            public void UseTemporarySubscriptionThatAlreadyExistsShouldRecreateSubscription()
            {
                _mockNamespaceManager.Setup(nsm => nsm.TopicExists("events"))
                    .Returns(true);

                _mockNamespaceManager.Setup(nsm => nsm.SubscriptionExists("events", "test-subscription"))
                    .Returns(true);

                ServiceBus.Configure()
                    .WithAzureServiceBusEndpoint<TestMessage>()
                    .Named("Test Service Bus")
                    .WithNamespaceManager(_mockNamespaceManager.Object)
                    .WithMessagingFactory(_mockMessagingFactory.Object)
                    .UsingSubscriptionFor<IEvent>("events", "test-subscription", MessagingEntityCreationOptions.CreateIfDoesntExist | MessagingEntityCreationOptions.CreateAsTemporary | MessagingEntityCreationOptions.RecreateExistingTemporary)
                    .SerializedWith(_mockMessageSerializer.Object, _mockMessageDeserializerFactory.Object)
                    .AsClient()
                    .CreateServiceBusClient();

                _mockNamespaceManager.Verify(nsm => nsm.DeleteSubscription("events", "test-subscription"), Times.Once);
                _mockNamespaceManager.Verify(nsm => nsm.CreateSubscription("events", "test-subscription"), Times.Once);
            }
        }

        public class TestMessage : IMessage
        {
        }

        public class TestEvent : TestMessage, IEvent
        {

        }

        public class TestCommand : TestMessage, ICommand
        {

        }

        public class TestRequest : TestMessage, IRequest
        {
            public string RequestId
            {
                get;
                set;
            }

            public string RequesterId
            {
                get;
                set;
            }
        }

        public class TestResponse : TestMessage, IResponse
        {
            public string RequestId
            {
                get;
                set;
            }

            public string RequesterId
            {
                get;
                set;
            }
        }
    }
}
