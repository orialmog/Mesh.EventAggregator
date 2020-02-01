using System;
using MeshNetworkEventAggregator.Framework.Interfaces;

namespace MeshNetworkEventAggregator.Framework
{
    public class DefaultSubscriberErrorHandler : ISubscriberErrorHandler
    {
        public void Handle(IMeshNetworkMessage networkMessage, Exception exception)
        {
            //default behaviour is to do nothing

        }
    }
}