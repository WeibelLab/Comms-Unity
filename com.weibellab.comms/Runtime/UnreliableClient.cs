using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace Comms
{
    public class UnreliableClient : Client
    {
        private UdpClient impl = new UdpClient();

        public UnreliableClient(string address, int port, Config config, string name ="UnreliableClient"): base()
        {
            this.config = config;
            this.config.Endpoint = new CommunicationEndpoint(address, port, name);
            // Setup Events
            OnByteMessageReceived = new CommunicationByteEvent();
            OnStringMessageReceived = new CommunicationStringEvent();
            OnJsonMessageReceived = new CommunicationJsonEvent();
            this.Awake();
        }

        public UnreliableClient(System.Net.IPEndPoint endpoint, Config config, string name = "UnreliableClient"): base()
        {
            this.config = config;
            this.config.Endpoint = new CommunicationEndpoint(endpoint, name);
            // Setup Events
            OnByteMessageReceived = new CommunicationByteEvent();
            OnStringMessageReceived = new CommunicationStringEvent();
            OnJsonMessageReceived = new CommunicationJsonEvent();
            this.Awake();
        }

        public void Configure(string address, int port, Config config, string name = "UnreliableClient")
        {
            this.config = config;
            this.config.Endpoint = new CommunicationEndpoint(address, port, name);
        }

        public void Configure(System.Net.IPEndPoint endpoint, Config config, string name = "UnreliableClient")
        {
            this.config = config;
            this.config.Endpoint = new CommunicationEndpoint(endpoint, name);
        }

        override public void Send(byte[] data)
        {
            Log($"sending message with {data.Length} bytes to {config.Endpoint.Address}:{config.Endpoint.Port}");
            //this.Header.GetHeader(); // TODO: add header
            impl.SendAsync(data, data.Length, this.config.Endpoint.AsIPEndPoint());
        }


        #region Unity Runtime
        new protected void Awake()
        {
            base.Awake();
            if (impl == null) impl = new UdpClient();
            this.MessageParser = this.EnqueueMessage;
        }

        /// <summary>
        /// Allows for a server to forcibly call the update loop
        /// This is done because the server instantiates clients directly
        /// and not through Unity.
        /// </summary>
        public void ForceUpdate()
        {
            this.Update();
        }

        new protected void Update()
        {
            base.Update();
        }
        #endregion
    }
}