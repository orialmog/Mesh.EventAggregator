namespace MeshNetworkEventAggregator.Framework.Interfaces
{
    /// <summary>
    /// Represents a message subscription
    /// </summary>
    public interface IMeshNetworkMessageSubscription
    {
        /// <summary>
        /// Token returned to the subscribed to reference this subscription
        /// </summary>
        MeshNetworkMessageSubscriptionToken SubscriptionToken { get; }

        /// <summary>
        /// Whether delivery should be attempted.
        /// </summary>
        /// <param name="networkMessage">Message that may potentially be delivered.</param>
        /// <returns>True - ok to send, False - should not attempt to send</returns>
        bool ShouldAttemptDelivery(IMeshNetworkMessage networkMessage);

        /// <summary>
        /// Deliver the message
        /// </summary>
        /// <param name="networkMessage">Message to deliver</param>
        void Deliver(IMeshNetworkMessage networkMessage);
    }
}