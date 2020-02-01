using System;
using System.Collections.Generic;
using System.Net;

namespace MeshNetworkEventAggregator.Framework.ServiceDiscovery
{
    class IPEndpointComparer : IComparer<IPEndPoint>
    {
        public static readonly IPEndpointComparer Instance = new IPEndpointComparer();

        public int Compare(IPEndPoint x, IPEndPoint y)
        {
            var c = String.Compare(x.Address.ToString(), y.Address.ToString(), StringComparison.Ordinal);
            if (c != 0) return c;
            return y.Port - x.Port;
        }
    }
}
