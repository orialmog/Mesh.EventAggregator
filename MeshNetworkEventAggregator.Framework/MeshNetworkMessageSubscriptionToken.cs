using System;
using MeshNetworkEventAggregator.Framework.Interfaces;

namespace MeshNetworkEventAggregator.Framework
{
    /// <summary>
    /// Represents an active subscription to a message
    /// </summary>
    public sealed class MeshNetworkMessageSubscriptionToken : IDisposable
    {
        private WeakReference _Hub;
        private Type _MessageType;

        /// <summary>
        /// Initializes a new instance of the MeshMessageSubscriptionToken class.
        /// </summary>
        public MeshNetworkMessageSubscriptionToken(IMeshNetworkMessengerHub hub, Type messageType)
        {
            if (hub == null)
                throw new ArgumentNullException("hub");

            if (!typeof(IMeshNetworkMessage).IsAssignableFrom(messageType))
                throw new ArgumentOutOfRangeException("messageType");

            _Hub = new WeakReference(hub);
            _MessageType = messageType;
        }

        public void Dispose()
        {
            if (_Hub.IsAlive)
            {
                var hub = _Hub.Target as IMeshNetworkMessengerHub;

                if (hub != null)
                {
                    var unsubscribeMethod = typeof(IMeshNetworkMessengerHub).GetMethod("Unsubscribe", new Type[] { typeof(MeshNetworkMessageSubscriptionToken) });
                    unsubscribeMethod = unsubscribeMethod.MakeGenericMethod(_MessageType);
                    unsubscribeMethod.Invoke(hub, new object[] { this });
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}