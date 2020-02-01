using System;
using MeshNetworkEventAggregator.Framework.Interfaces;

namespace MeshNetworkEventAggregator.Framework
{
    /// <summary>
    /// Base class for messages that provides weak reference storage of the sender
    /// </summary>
    public abstract class MeshNetworkMessageBase : IMeshNetworkMessage
    {
        /// <summary>
        /// Store a WeakReference to the sender just in case anyone is daft enough to
        /// keep the message around and prevent the sender from being collected.
        /// </summary>
        private WeakReference _Sender;
        public object Sender
        {
            get
            {
                return (_Sender == null) ? null : _Sender.Target;
            }
        }

        /// <summary>
        /// Initializes a new instance of the MessageBase class.
        /// </summary>
        /// <param name="sender">Message sender (usually "this")</param>
        public MeshNetworkMessageBase(object sender)
        {
            if (sender == null)
                throw new ArgumentNullException("sender");

            _Sender = new WeakReference(sender);
        }
    }
}