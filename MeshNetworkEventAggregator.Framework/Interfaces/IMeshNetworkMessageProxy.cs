namespace MeshNetworkEventAggregator.Framework.Interfaces
{

    /// <summary>
    /// Message proxy definition.
    /// 
    /// A message proxy can be used to intercept/alter messages and/or
    /// marshall delivery actions onto a particular thread.
    /// </summary>
    public interface IMeshNetworkMessageProxy
    {
        void Deliver(IMeshNetworkMessage networkMessage, IMeshNetworkMessageSubscription subscription);
    }

}
