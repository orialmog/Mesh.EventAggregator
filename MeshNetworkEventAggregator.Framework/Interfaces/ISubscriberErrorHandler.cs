using System;

namespace MeshNetworkEventAggregator.Framework.Interfaces
{
    public interface ISubscriberErrorHandler
    {
        void Handle(IMeshNetworkMessage networkMessage, Exception exception);
    }
}