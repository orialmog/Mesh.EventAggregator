using MeshNetworkEventAggregator.Framework.Interfaces;

namespace MeshNetworkEventAggregator.Framework
{

    /// <summary>
    /// Default "pass through" proxy.
    /// 
    /// Does nothing other than deliver the message.
    /// </summary>
    public sealed class DefaultMeshNetworkMessageProxy : IMeshNetworkMessageProxy
    {
      
        /// <summary>
        /// Singleton instance of the proxy.
        /// </summary>
        public static DefaultMeshNetworkMessageProxy Instance =>  new DefaultMeshNetworkMessageProxy();

        private DefaultMeshNetworkMessageProxy()
        {
        }

        public void Deliver(IMeshNetworkMessage networkMessage, IMeshNetworkMessageSubscription subscription)
        {
            subscription.Deliver(networkMessage);
        }
    }


}
