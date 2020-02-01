using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using MeshNetworkEventAggregator.Framework.Interfaces;
using Newtonsoft.Json;
using Watson;

namespace MeshNetworkEventAggregator.Framework.ServiceDiscovery
{
    public class DiscoverableMeshEthNetwork : IDisposable
    {
        public ILogger Log = new ConsoleLog();

        private readonly Probe probe;

        private Random r = new Random();

        private static IPAddress _Ip;
        private static int _Port;
        public static Peer Self { get; private set; }

        private static MeshSettings _settings;
        private static WatsonMesh _mesh; 

        private IPAddress GetFirstIpAddress(NetworkInterfaceType network = NetworkInterfaceType.Ethernet, AddressFamily  addressFamily = AddressFamily.InterNetwork)
        { 
            var addresses = Dns.GetHostAddresses(Environment.MachineName);
            if (!addresses.Any())
            {
                throw new ArgumentException("No addresses found for the given host");
            }

            return NetworkInterface
                  .GetAllNetworkInterfaces()
                  .Where(i => i.NetworkInterfaceType == network)
                  .SelectMany(i => i.GetIPProperties().UnicastAddresses)
                  .Where(a => a.Address.AddressFamily == addressFamily)
                  .Select(a => a.Address).ToArray().First();
        }


        public DiscoverableMeshEthNetwork(
            string meshName, 
            NetworkInterfaceType network = NetworkInterfaceType.Ethernet, 
            AddressFamily addressFamily = AddressFamily.InterNetwork,
            int fromPortRange = 8048, int toPortRange = 12000)
        {
            _Port = r.Next(fromPortRange, toPortRange);
            _Ip = GetFirstIpAddress(network, addressFamily);

            _settings = new MeshSettings();

            Self = new Peer(_Ip.ToString(), _Port, false);
            Log.Info($"I am {Self.IpPort}");

            _mesh = new WatsonMesh(_settings, Self)
            {
                PeerConnected = PeerConnected,
                PeerDisconnected = PeerDisconnected,
                AsyncMessageReceived = AsyncMessageReceived,
                SyncMessageReceived = SyncMessageReceived
            };
            _mesh.StartServer();

            var b = new Beacon(meshName, (ushort)_Port)
            {
                BeaconData = _Ip.ToString()
            };
            b.Start();

            probe = new Probe(meshName);

            probe.BeaconsUpdated += delegate (IEnumerable<BeaconLocation> locations)
                {

                    var connectedPeers = _mesh.GetPeers()
                        .Select(p => p.IpPort)
                        .ToList();

                    var newNodes = locations
                        .Where(x => !connectedPeers.Contains($"{x.Data}:{x.Address.Port}"))
                        .ToArray();

                    foreach (var beacon in newNodes)
                    {
                        var peer = new Peer(beacon.Data, beacon.Address.Port, ssl: false);
                        Console.Write($"Mesh -> Adding peer {peer.Ip}:{peer.Port} to mesh ... ");
                        _mesh.Add(peer);
                        Log.Info("Connected.");

                    }

                };

            probe.Start();
        }

        private bool PeerConnected(Peer peer)
        {
            Log.Info($"Mesh -> Peer connected: {peer}");

            if (peer.IpPort != Self.IpPort)
                _mesh.Broadcast(Encoding.UTF8.GetBytes(Wrap($"Hey {peer.IpPort}!")));

            return true;
        }

        private bool PeerDisconnected(Peer peer)
        {
            Log.Info($"Mesh -> Peer disconnected: {peer}");

            return true;
        }

        private byte[] SyncMessageReceived(Peer peer, byte[] data)
        {
            if (ReceiveBytes(peer, data)) return null;

            return data;
        }

        private bool AsyncMessageReceived(Peer peer, byte[] data)
        {
            return ReceiveBytes(peer, data);
        }

        protected virtual void OnPeerSaid(Peer peer, TypeDescriptor descriptor, object instance)
        {

        }


        private bool ReceiveBytes(Peer peer, byte[] data)
        {
            Log.Info($"Mesh -> {peer} said {Encoding.UTF8.GetString(data)}");

            TypeDescriptor descriptor = null;
            Type type = null;
            object instance = null;
            try
            {
                descriptor = Common.DeserializeJson<TypeDescriptor>(Encoding.UTF8.GetString(data));
                type = Type.GetType(descriptor.FullTypeName);
                instance = JsonConvert.DeserializeObject(descriptor.JsonValue, type);
            }
            catch (Exception e)
            {
                Log.Error(e);
                Log.Info("Mesh -> Dump ===>" + Encoding.UTF8.GetString(data));
            }

            if (type == null || instance == null)
            {
                return true;
            }

            if (peer.IpPort != Self.IpPort)
            {
                Log.Info($"Mesh -> {peer} said {instance}");
                OnPeerSaid(peer, descriptor, instance);
            }
            else
            {
                Log.Info($"Mesh -> I said {instance}"); //Echo 
            }

            return false;
        }

        public void Dispose()
        {
            probe?.Stop();
            probe?.Dispose();
        }

        public void Broadcast(IMeshNetworkMessage networkMessage)
        {
            if (_mesh.GetPeers().Count() > 1)
                _mesh.Broadcast(Encoding.UTF8.GetBytes(Wrap(networkMessage)));
        }
        
        private static string Wrap(object value)
        {
            var json = Common.SerializeJson(value, false);
            var descriptor = new TypeDescriptor()
            {
                FullTypeName = value.GetType().AssemblyQualifiedName,
                Sender = Self.IpPort,
                JsonValue = json,
            };
            return Common.SerializeJson(descriptor, false);
        }
    }
}
