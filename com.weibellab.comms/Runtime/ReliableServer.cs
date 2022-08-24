using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System;
using System.Net.Http;
using System.Threading;

namespace Comms
{
    public class ReliableServer : Server
    {
        public Header header;

        [SerializeField]
        private int ReconnectTimeoutMs = 1000;
        [SerializeField]
        private int ListenErrorTimeoutMs = 5000;

        private TcpListener impl;

        public bool isConnected { get { return Clients.Count > 0; } }



        public void CloseClients()
        {
            for (int i = Clients.Count - 1; i >= 0; i--)
            {
                // trigger disconnect event
                RunOnUnityThread(() => {
                    OnClientDisconnected.Invoke(Clients[i]);
                });
                Destroy(Clients[i]);
            }
        }

        protected override void CloseSocket()
        {
            if (impl != null) impl.Stop();
            for (int i=Clients.Count-1; i>=0; i--)
            {
                // trigger disconnect event
                RunOnUnityThread(() => {
                    OnClientDisconnected.Invoke(Clients[i]);
                });
                Destroy(Clients[i]);
            }
        }

        protected override void DisposeSocket()
        {
            this.impl = null;
            this.Clients = new List<Client>();
        }

        protected override void SetupSocket()
        {
            // Check if already running
            if (ServerThread != null && ServerThread.IsAlive)
            {
                LogWarning("Already running. Disable and Re-enabled component to reset");
                return;
            }

            // Start server
            stopThread = false;
            try
            {
                this.impl = new TcpListener(config.Endpoint.AsIPEndPoint());
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
            bool firstTime = true;
            bool serverListening = false;
            ReliableClient client = null;

            while (!stopThread)
            {
                try
                {
                    serverListening = false;
                    if (!firstTime)
                    {
                        Thread.Sleep(ListenErrorTimeoutMs);
                    }
                    firstTime = false;

                    impl.Start();
                    serverListening = true;

                    // Get a stream object for reading
                    // Todo: Handle multiple clients
                    while (!stopThread && impl != null)
                    {
                        try
                        {
                            using (TcpClient tcpClient = impl.AcceptTcpClient())
                            {
                                IPEndPoint clientEndpoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
                                Log($"Client connected {clientEndpoint.Address}:{clientEndpoint.Port}");

                                // Create new client
                                client = new ReliableClient(tcpClient, config);
                                this.Clients.Add(client);
                                RunOnUnityThread(() =>
                                {
                                    OnClientConnected.Invoke(client);
                                });

                                // TODO: stats.RecordConnectionEstablished();
                                //SocketMessageReadingLoop(tcpClient, true);
                            }
                        }
                        catch (SocketException socketException)
                        {
                            if (socketException.SocketErrorCode != SocketError.Interrupted)
                            {
                                // if we didn't interrupt it -> reconnect, report statistics, log warning
                                LogError("client socket Exception: " + socketException.SocketErrorCode + "->" + socketException);
                                // TODO: stats.RecordStreamError();
                            }
                            else
                                return; // ends thread (finally will take care of statistics and logging below)
                        }
                        catch (SocketDisconnected)
                        {
                            // do nothing.. finally will take care of it below
                        }
                        finally
                        {
                            if (client != null)
                            {
                                Destroy(client);
                                // TODO: the client should handle this
                                // TODO: stats.RecordStreamDisconnect();
                                Log("Client disconnected");
                            }
                            client = null;
                        }

                    }
                }
                catch (SocketException socketException)
                {
                    if (socketException.SocketErrorCode != SocketError.Interrupted && socketException.SocketErrorCode != SocketError.Shutdown)
                    {
                        // if we didn't interrupt it (or shut it down) -> log warning and report statistics
                        LogError("Server socket Exception: " + socketException.SocketErrorCode + "->" + socketException);
                        // TODO: stats.RecordStreamError();
                        break;
                    }
                    else
                    {
                        return; // ends thread
                    }
                }
                catch (ObjectDisposedException)
                {
                    // this exception happens when the socket could not finish  its operation
                    // and we forcefully aborted the thread and cleared the object
                }
                catch (ThreadAbortException)
                {
                    // this exception happens when the socket could not finish  its operation
                    // and we forcefully aborted the thread (we wait 100 ms)
                }
                catch (Exception e)
                {
                    // this is likely not a socket error. So while we do not record a stream error,
                    // we still log for later learning about it
                    LogWarning("Exception " + e);
                }
                finally
                {
                    // were we listening? 
                    if (serverListening)
                    {
                        if (stopThread)
                            Debug.Log("Stopped");
                        else
                            Debug.Log("Stopped - Trying again in " + (ReconnectTimeoutMs / 1000f) + " sec");
                    }
                }
            }
        }


        #region Unity Runtime

        new private void Update()
        {
            base.Update();
            ReadFromQueue();

            for (int i = 0; i < Clients.Count; i++)
            {
                //(Clients[i] as ReliableClient).ForceUpdate();
            }
        }

        protected override void DefaultMessageParser(object o)
        {
            // TODO: will be handled by clients, not server.

            // Check if byte[]
            if (!(typeof(NetworkStream).IsAssignableFrom(o.GetType())))
                return;
            // Enqueue
            NetworkStream stream = o as NetworkStream;

            return;
        }
        #endregion
    }
}
