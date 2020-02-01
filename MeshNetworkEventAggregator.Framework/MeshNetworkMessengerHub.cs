using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using MeshNetworkEventAggregator.Framework.Interfaces;
using MeshNetworkEventAggregator.Framework.ServiceDiscovery;
using Watson;

namespace MeshNetworkEventAggregator.Framework
{
    /// <summary>
    /// Messenger hub responsible for taking subscriptions/publications and delivering of messages.
    /// </summary>
    public sealed class MeshNetworkMessengerHub : DiscoverableMeshEthNetwork, IMeshNetworkMessengerHub
    {
        readonly ISubscriberErrorHandler _subscriberErrorHandler;

        #region ctor methods

        public MeshNetworkMessengerHub(string meshname,
        NetworkInterfaceType network = NetworkInterfaceType.Ethernet, 
            AddressFamily addressFamily = AddressFamily.InterNetwork,
            int fromPortRange = 8048, int toPortRange = 12000) : base(meshname, network,addressFamily, fromPortRange, toPortRange)
        {
            _subscriberErrorHandler = new DefaultSubscriberErrorHandler();

        }

        public MeshNetworkMessengerHub(string meshname, ISubscriberErrorHandler subscriberErrorHandler,
            NetworkInterfaceType network = NetworkInterfaceType.Ethernet, 
            AddressFamily addressFamily = AddressFamily.InterNetwork,
            int fromPortRange = 8048, int toPortRange = 12000) :base(meshname, network,addressFamily, fromPortRange, toPortRange)
        {
            _subscriberErrorHandler = subscriberErrorHandler;
        }

        #endregion

        #region Private Types and Interfaces
        private class WeakMeshNetworkMessageSubscription<TMessage> : IMeshNetworkMessageSubscription
            where TMessage : class, IMeshNetworkMessage
        {
            protected MeshNetworkMessageSubscriptionToken _SubscriptionToken;
            protected WeakReference _DeliveryAction;
            protected WeakReference _MessageFilter;

            public MeshNetworkMessageSubscriptionToken SubscriptionToken
            {
                get { return _SubscriptionToken; }
            }

            public bool ShouldAttemptDelivery(IMeshNetworkMessage networkMessage)
            {
                if (networkMessage == null)
                    return false;

                if (!(typeof(TMessage).IsAssignableFrom(networkMessage.GetType())))
                    return false;

                if (!_DeliveryAction.IsAlive)
                    return false;

                if (!_MessageFilter.IsAlive)
                    return false;

                return ((Func<TMessage, bool>)_MessageFilter.Target).Invoke(networkMessage as TMessage);
            }

            public void Deliver(IMeshNetworkMessage networkMessage)
            {
                if (!(networkMessage is TMessage))
                    throw new ArgumentException("Message is not the correct type");

                if (!_DeliveryAction.IsAlive)
                    return;

                ((Action<TMessage>)_DeliveryAction.Target).Invoke(networkMessage as TMessage);
            }

            /// <summary>
            /// Initializes a new instance of the WeakMeshMessageSubscription class.
            /// </summary>
            /// <param name="destination">Destination object</param>
            /// <param name="deliveryAction">Delivery action</param>
            /// <param name="messageFilter">Filter function</param>
            public WeakMeshNetworkMessageSubscription(MeshNetworkMessageSubscriptionToken subscriptionToken, Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter)
            {
                if (subscriptionToken == null)
                    throw new ArgumentNullException("subscriptionToken");

                if (deliveryAction == null)
                    throw new ArgumentNullException("deliveryAction");

                if (messageFilter == null)
                    throw new ArgumentNullException("messageFilter");

                _SubscriptionToken = subscriptionToken;
                _DeliveryAction = new WeakReference(deliveryAction);
                _MessageFilter = new WeakReference(messageFilter);
            }
        }

        private class StrongMeshNetworkMessageSubscription<TMessage> : IMeshNetworkMessageSubscription
            where TMessage : class, IMeshNetworkMessage
        {
            protected MeshNetworkMessageSubscriptionToken _SubscriptionToken;
            protected Action<TMessage> _DeliveryAction;
            protected Func<TMessage, bool> _MessageFilter;

            public MeshNetworkMessageSubscriptionToken SubscriptionToken
            {
                get { return _SubscriptionToken; }
            }

            public bool ShouldAttemptDelivery(IMeshNetworkMessage networkMessage)
            {
                if (networkMessage == null)
                    return false;

                if (!(typeof(TMessage).IsAssignableFrom(networkMessage.GetType())))
                    return false;

                return _MessageFilter.Invoke(networkMessage as TMessage);
            }

            public void Deliver(IMeshNetworkMessage networkMessage)
            {
                if (!(networkMessage is TMessage))
                    throw new ArgumentException("Message is not the correct type");

                _DeliveryAction.Invoke(networkMessage as TMessage);
            }

            /// <summary>
            /// Initializes a new instance of the MeshMessageSubscription class.
            /// </summary>
            /// <param name="destination">Destination object</param>
            /// <param name="deliveryAction">Delivery action</param>
            /// <param name="messageFilter">Filter function</param>
            public StrongMeshNetworkMessageSubscription(MeshNetworkMessageSubscriptionToken subscriptionToken, Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter)
            {
                if (subscriptionToken == null)
                    throw new ArgumentNullException("subscriptionToken");

                if (deliveryAction == null)
                    throw new ArgumentNullException("deliveryAction");

                if (messageFilter == null)
                    throw new ArgumentNullException("messageFilter");

                _SubscriptionToken = subscriptionToken;
                _DeliveryAction = deliveryAction;
                _MessageFilter = messageFilter;
            }
        }
        #endregion

        #region Subscription dictionary
        private class SubscriptionItem
        {
            public IMeshNetworkMessageProxy Proxy { get; private set; }
            public IMeshNetworkMessageSubscription Subscription { get; private set; }

            public SubscriptionItem(IMeshNetworkMessageProxy proxy, IMeshNetworkMessageSubscription subscription)
            {
                Proxy = proxy;
                Subscription = subscription;
            }
        }

        private readonly object _SubscriptionsPadlock = new object();
        private readonly List<SubscriptionItem> _Subscriptions = new List<SubscriptionItem>();
        #endregion

        #region Public API
        /// <summary>
        /// Subscribe to a message type with the given destination and delivery action.
        /// All references are held with strong references
        /// 
        /// All messages of this type will be delivered.
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        /// <param name="deliveryAction">Action to invoke when message is delivered</param>
        /// <returns>MeshMessageSubscription used to unsubscribing</returns>
        public MeshNetworkMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction) where TMessage : class, IMeshNetworkMessage
        {
            return AddSubscriptionInternal<TMessage>(deliveryAction, (m) => true, true, DefaultMeshNetworkMessageProxy.Instance);
        }

        /// <summary>
        /// Subscribe to a message type with the given destination and delivery action.
        /// Messages will be delivered via the specified proxy.
        /// All references (apart from the proxy) are held with strong references
        /// 
        /// All messages of this type will be delivered.
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        /// <param name="deliveryAction">Action to invoke when message is delivered</param>
        /// <param name="proxy">Proxy to use when delivering the messages</param>
        /// <returns>MeshMessageSubscription used to unsubscribing</returns>
        public MeshNetworkMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, IMeshNetworkMessageProxy proxy) where TMessage : class, IMeshNetworkMessage
        {
            return AddSubscriptionInternal<TMessage>(deliveryAction, (m) => true, true, proxy);
        }

        /// <summary>
        /// Subscribe to a message type with the given destination and delivery action.
        /// 
        /// All messages of this type will be delivered.
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        /// <param name="deliveryAction">Action to invoke when message is delivered</param>
        /// <param name="useStrongReferences">Use strong references to destination and deliveryAction </param>
        /// <returns>MeshMessageSubscription used to unsubscribing</returns>
        public MeshNetworkMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, bool useStrongReferences) where TMessage : class, IMeshNetworkMessage
        {
            return AddSubscriptionInternal<TMessage>(deliveryAction, (m) => true, useStrongReferences, DefaultMeshNetworkMessageProxy.Instance);
        }

        /// <summary>
        /// Subscribe to a message type with the given destination and delivery action.
        /// Messages will be delivered via the specified proxy.
        /// 
        /// All messages of this type will be delivered.
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        /// <param name="deliveryAction">Action to invoke when message is delivered</param>
        /// <param name="useStrongReferences">Use strong references to destination and deliveryAction </param>
        /// <param name="proxy">Proxy to use when delivering the messages</param>
        /// <returns>MeshMessageSubscription used to unsubscribing</returns>
        public MeshNetworkMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, bool useStrongReferences, IMeshNetworkMessageProxy proxy) where TMessage : class, IMeshNetworkMessage
        {
            return AddSubscriptionInternal<TMessage>(deliveryAction, (m) => true, useStrongReferences, proxy);
        }

        /// <summary>
        /// Subscribe to a message type with the given destination and delivery action with the given filter.
        /// All references are held with WeakReferences
        /// 
        /// Only messages that "pass" the filter will be delivered.
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        /// <param name="deliveryAction">Action to invoke when message is delivered</param>
        /// <returns>MeshMessageSubscription used to unsubscribing</returns>
        public MeshNetworkMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter) where TMessage : class, IMeshNetworkMessage
        {
            return AddSubscriptionInternal<TMessage>(deliveryAction, messageFilter, true, DefaultMeshNetworkMessageProxy.Instance);
        }

        /// <summary>
        /// Subscribe to a message type with the given destination and delivery action with the given filter.
        /// Messages will be delivered via the specified proxy.
        /// All references (apart from the proxy) are held with WeakReferences
        /// 
        /// Only messages that "pass" the filter will be delivered.
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        /// <param name="deliveryAction">Action to invoke when message is delivered</param>
        /// <param name="proxy">Proxy to use when delivering the messages</param>
        /// <returns>MeshMessageSubscription used to unsubscribing</returns>
        public MeshNetworkMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter, IMeshNetworkMessageProxy proxy) where TMessage : class, IMeshNetworkMessage
        {
            return AddSubscriptionInternal<TMessage>(deliveryAction, messageFilter, true, proxy);
        }

        /// <summary>
        /// Subscribe to a message type with the given destination and delivery action with the given filter.
        /// All references are held with WeakReferences
        /// 
        /// Only messages that "pass" the filter will be delivered.
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        /// <param name="deliveryAction">Action to invoke when message is delivered</param>
        /// <param name="useStrongReferences">Use strong references to destination and deliveryAction </param>
        /// <returns>MeshMessageSubscription used to unsubscribing</returns>
        public MeshNetworkMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter, bool useStrongReferences) where TMessage : class, IMeshNetworkMessage
        {
            return AddSubscriptionInternal<TMessage>(deliveryAction, messageFilter, useStrongReferences, DefaultMeshNetworkMessageProxy.Instance);
        }

        /// <summary>
        /// Subscribe to a message type with the given destination and delivery action with the given filter.
        /// Messages will be delivered via the specified proxy.
        /// All references are held with WeakReferences
        /// 
        /// Only messages that "pass" the filter will be delivered.
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        /// <param name="deliveryAction">Action to invoke when message is delivered</param>
        /// <param name="useStrongReferences">Use strong references to destination and deliveryAction </param>
        /// <param name="proxy">Proxy to use when delivering the messages</param>
        /// <returns>MeshMessageSubscription used to unsubscribing</returns>
        public MeshNetworkMessageSubscriptionToken Subscribe<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter, bool useStrongReferences, IMeshNetworkMessageProxy proxy) where TMessage : class, IMeshNetworkMessage
        {
            return AddSubscriptionInternal<TMessage>(deliveryAction, messageFilter, useStrongReferences, proxy);
        }

        /// <summary>
        /// Unsubscribe from a particular message type.
        /// 
        /// Does not throw an exception if the subscription is not found.
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        /// <param name="subscriptionToken">Subscription token received from Subscribe</param>
        public void Unsubscribe<TMessage>(MeshNetworkMessageSubscriptionToken subscriptionToken) where TMessage : class, IMeshNetworkMessage
        {
            RemoveSubscriptionInternal<TMessage>(subscriptionToken);
        }

        /// <summary>
        /// Unsubscribe from a particular message type.
        /// 
        /// Does not throw an exception if the subscription is not found.
        /// </summary>
        /// <param name="subscriptionToken">Subscription token received from Subscribe</param>
        public void Unsubscribe(MeshNetworkMessageSubscriptionToken subscriptionToken)
        {
            RemoveSubscriptionInternal<IMeshNetworkMessage>(subscriptionToken);
        }

        /// <summary>
        /// Publish a message to any subscribers
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        /// <param name="message">Message to deliver</param>
        public void Publish<TMessage>(TMessage message) where TMessage : class, IMeshNetworkMessage
        {
            PublishInternal<TMessage>(message);
        }

        /// <summary>
        /// Publish a message to any subscribers asynchronously
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        /// <param name="message">Message to deliver</param>
        public void PublishAsync<TMessage>(TMessage message) where TMessage : class, IMeshNetworkMessage
        {
            PublishAsyncInternal<TMessage>(message, null);
        }

        /// <summary>
        /// Publish a message to any subscribers asynchronously
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        /// <param name="message">Message to deliver</param>
        /// <param name="callback">AsyncCallback called on completion</param>
        public void PublishAsync<TMessage>(TMessage message, AsyncCallback callback) where TMessage : class, IMeshNetworkMessage
        {
            PublishAsyncInternal<TMessage>(message, callback);
        }
        #endregion

        #region Internal Methods

        private MeshNetworkMessageSubscriptionToken AddSubscriptionInternal<TMessage>(Action<TMessage> deliveryAction, Func<TMessage, bool> messageFilter, bool strongReference, IMeshNetworkMessageProxy proxy)
            where TMessage : class, IMeshNetworkMessage
        {
            if (deliveryAction == null)
                throw new ArgumentNullException("deliveryAction");

            if (messageFilter == null)
                throw new ArgumentNullException("messageFilter");

            if (proxy == null)
                throw new ArgumentNullException("proxy");

            lock (_SubscriptionsPadlock)
            {
                var subscriptionToken = new MeshNetworkMessageSubscriptionToken(this, typeof(TMessage));

                IMeshNetworkMessageSubscription subscription;
                if (strongReference)
                    subscription = new StrongMeshNetworkMessageSubscription<TMessage>(subscriptionToken, deliveryAction, messageFilter);
                else
                    subscription = new WeakMeshNetworkMessageSubscription<TMessage>(subscriptionToken, deliveryAction, messageFilter);

                _Subscriptions.Add(new SubscriptionItem(proxy, subscription));

                return subscriptionToken;
            }
        }

        private void RemoveSubscriptionInternal<TMessage>(MeshNetworkMessageSubscriptionToken subscriptionToken)
            where TMessage : class, IMeshNetworkMessage
        {
            if (subscriptionToken == null)
                throw new ArgumentNullException("subscriptionToken");

            lock (_SubscriptionsPadlock)
            {
                var currentlySubscribed = (from sub in _Subscriptions
                                           where object.ReferenceEquals(sub.Subscription.SubscriptionToken, subscriptionToken)
                                           select sub).ToList();

                currentlySubscribed.ForEach(sub => _Subscriptions.Remove(sub));
            }
        }

        private void PublishInternal<TMessage>(TMessage message)
            where TMessage : class, IMeshNetworkMessage
        {
            DeliverLocal(message, (m) => base.Broadcast(m));
        }

        private void DeliverLocal<TMessage>(TMessage message, Action<TMessage> delivered = null) where TMessage : class, IMeshNetworkMessage
        {
            if (message == null)
                throw new ArgumentNullException("message");

            List<SubscriptionItem> currentlySubscribed;
            lock (_SubscriptionsPadlock)
            {
                currentlySubscribed = (from sub in _Subscriptions
                                       where sub.Subscription.ShouldAttemptDelivery(message)
                                       select sub).ToList();
            }
            
            currentlySubscribed.ForEach(sub =>
            {
                try
                {
                    sub.Proxy.Deliver(message, sub.Subscription);
                    delivered?.Invoke(message);
                }
                catch (Exception exception)
                {
                    // By default ignore any errors and carry on
                    _subscriberErrorHandler.Handle(message, exception);
                }
            });
        }

        protected override void OnPeerSaid(Peer peer, TypeDescriptor descriptor, object instance)
        {
            if (Self.IpPort != descriptor.Sender) 
            {
                if (instance is IMeshNetworkMessage message)
                    DeliverLocal(message: message);
            }
        }

        private void PublishAsyncInternal<TMessage>(TMessage message, AsyncCallback callback) where TMessage : class, IMeshNetworkMessage
        {
            Action publishAction = () => { PublishInternal<TMessage>(message); };

            publishAction.BeginInvoke(callback, null);

        }


        #endregion
    }
}