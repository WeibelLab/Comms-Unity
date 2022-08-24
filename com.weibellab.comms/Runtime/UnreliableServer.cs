using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Comms
{
    public class UnreliableServer : Server
    {
        public int KeepClientsForSeconds;
        private Dictionary<UnreliableClient, System.DateTime> ClientTimeouts;
        private UdpClient impl;

        protected override void CloseSocket()
        {
            this.impl.Close();
        }

        protected override void DisposeSocket()
        {
            this.impl = null;
        }

        protected override void SetupSocket()
        {
            try
            {
                this.impl = new UdpClient(config.Endpoint.AsIPEndPoint());
            }
            catch (System.Exception err)
            {
                LogError($"Failed to initialize socket\n{err}");
                this.StopThread();
                this.impl = null;
            }
        }

        protected override void ServerThreadLoop()
        {
            IPEndPoint ipEndpoint = config.Endpoint.AsIPEndPoint();
            // TODO: statistics.RecordConnectionEstablished()
            while (!stopThread)
            {
                try
                {
                    ipEndpoint = config.Endpoint.AsIPEndPoint();
                    // Read from socket
                    byte[] msg = impl.Receive(ref ipEndpoint);
                    MessageParser(msg);
                    continue;

                    // TODO: implement header read
                    // TODO: stats.RecordPacketReceived(msg.Length);

                    // Create a client from the sender so a response can be sent
                    if (KeepClientsForSeconds != 0)
                    {
                        // Check if client already exists
                        UnreliableClient client = null;
                        for (int i=0; i<Clients.Count; i++)
                        {
                            if (Clients[i].config.Endpoint.Equals(ipEndpoint))
                            {
                                Log("Client already existed");
                                client = Clients[i] as UnreliableClient;
                                client.MessageParser(msg);
                                break;
                            }
                        }

                        // Create new client
                        if (client == null)
                        {
                            Log("Creating new Client");
                            // TODO: Unity throws warning, but this is handled through ForceUpdate
                            client = new UnreliableClient(ipEndpoint, config, $"{config.Endpoint.Name}_UDP_{Clients.Count}");
                            Clients.Add(client);
                            ClientTimeouts.Add(client, System.DateTime.Now);
                            // Events
                            client.OnByteMessageReceived.AddListener(this.OnByteMessageReceived.Invoke);
                            client.OnStringMessageReceived.AddListener(this.OnStringMessageReceived.Invoke);
                            client.OnJsonMessageReceived.AddListener(this.OnJsonMessageReceived.Invoke);

                            client.MessageParser(msg);
                        }
                    }
                    else
                    {
                        MessageParser(msg);
                    }
                }
                catch (System.Threading.ThreadAbortException)
                { }
                catch (System.Net.Sockets.SocketException socketException)
                {
                    if (socketException.SocketErrorCode != SocketError.Interrupted)
                    {
                        LogError($"Socket Error: {socketException.SocketErrorCode} -> {socketException}");
                        // TODO: stats.RecordStreamError();
                    }
                }
                catch (System.Exception err)
                {
                    LogError($"Generic Exception -> {err}");
                    // TODO: stats.RecordStreamError();
                }
            }
        }

        //private IEnumerator RemoveClientAfterTimeout(UnreliableClient client)
        //{
        //    if (KeepClientsForSeconds == -1)
        //        yield break;
            
        //    yield return new WaitForSecondsRealtime(KeepClientsForSeconds);
        //    Clients.Remove(client);
        //    //Destroy(client);
        //}

        private void CheckForTimedOutClients()
        {
            if (KeepClientsForSeconds <= 0) return; // only check if there's an actual timeout

            for (int i=Clients.Count-1; i>=0; i--)
            {
                UnreliableClient client = Clients[i] as UnreliableClient;
                if (!ClientTimeouts.ContainsKey(client))
                {
                    Clients.RemoveAt(i);
                    Destroy(client);
                }
                else if ((System.DateTime.Now - ClientTimeouts[client]).TotalSeconds > KeepClientsForSeconds)
                {
                    ClientTimeouts.Remove(client);
                    Clients.RemoveAt(i);
                    Destroy(client);
                }
                else
                {
                    // Hasn't timed out
                }
            }
        }


        #region Unity Runtime
        new private void Awake()
        {
            base.Awake();
            ClientTimeouts = new Dictionary<UnreliableClient, System.DateTime>();
            if (this.MessageParser == null) this.MessageParser = this.EnqueueMessage;
        }

        new private void Update()
        {
            base.Update();
            ReadFromQueue();
            CheckForTimedOutClients();

            for (int i = 0; i < Clients.Count; i++)
            {
                (Clients[i] as UnreliableClient).ForceUpdate();
            }
        }
        #endregion
    }
}