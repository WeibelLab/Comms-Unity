using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Threading;
using System;

namespace Comms
{
    public class ReliableClient : Client
    {
        public Header Header;

        [SerializeField]
        private int ReconnectTimeoutMs = 1000;
        [SerializeField]
        private int ListenErrorTimeoutMs = 5000;

        private TcpClient impl;

        public bool isConnected { get { if (impl != null) return impl.Connected; else return false; } }

        public ReliableClient(Config config): base(config)
        {
            Log("Initializing Reliable Client");
            if (impl == null) impl = new TcpClient(config.Endpoint.AsIPEndPoint());

            // TODO: check to see if this is blocking and TcpClient should be created in a thread
        }

        public ReliableClient(TcpClient from, Config config, ReliableServer attachTo, string name="ReliableClient"): base(config)
        {
            this.impl = from;
            this.config.Endpoint = new CommunicationEndpoint((IPEndPoint)from.Client.RemoteEndPoint, name);
            // Setup events
            OnByteMessageReceived.AddListener(attachTo.OnByteMessageReceived.Invoke);
            OnStringMessageReceived.AddListener(attachTo.OnStringMessageReceived.Invoke);
            OnJsonMessageReceived.AddListener(attachTo.OnJsonMessageReceived.Invoke);
            OnClientConnected.AddListener(attachTo.OnClientConnected.Invoke);
            OnClientDisconnected.AddListener(attachTo.OnClientDisconnected.Invoke);
            OnClientError.AddListener(attachTo.OnClientError.Invoke);

            if (impl == null) impl = new TcpClient();
        }

        ~ReliableClient()
        {
            if (impl != null) impl.Close();
        }

        public bool StartThread()
        {
            // Check if already running
            if (thread != null && thread.IsAlive)
            {
                LogWarning("Already running.");
                return false;
            }

            try
            {
                stopThread = false;
                this.thread = new Thread(new ThreadStart(threadLoop));
                this.thread.Start();
                return true;
            }
            catch (System.Exception e)
            {
                LogError("Failed to start thread.\n" + e);
                return false;
            }
        }

        override public void Send(byte[] data)
        {
            if (impl == null)
            {
                LogWarning("Not connected! Dropping message...");
                // TODO: stats. drop message?
                return;
            }

            // Send message
            byte[] msg = this.Header.AffixHeader(data);
            try
            {
                NetworkStream stream = impl.GetStream();
                if (stream.CanWrite)
                {
                    stream.Write(msg, 0, msg.Length);
                    // TODO: stats.RecordMessageSent(msg.Length);
                }
            }
            catch (SocketException e)
            {
                Log($"Socket Exception while sending. {e}");
            }
            catch (System.ObjectDisposedException e)
            {
                LogWarning("Tried to send message through stream that was closed/closing. Dropping message and forcing stream to close.");
                impl.Close();
                impl = null;
            }
        }

        protected override void DefaultMessageParser(object o)
        {
            if (!(typeof(NetworkStream).IsAssignableFrom(o.GetType()))) return;
            NetworkStream stream = o as NetworkStream;

            (Header h, byte[] msg) = this.Header.ReadFromStream(stream);
            // TODO: stats.RecordMessageReceived()
            
        }

        private void threadLoop()
        {
            bool firstTime = true;
            bool implInitialized = false;

            while (!stopThread)
            {
                try
                {
                    implInitialized = false;
                    if (!firstTime)
                        Thread.Sleep(ReconnectTimeoutMs);
                    firstTime = false;

                    Log($"Connecting to {config.Endpoint.Address}:{config.Endpoint.Port}");
                    if (!isConnected)
                    {
                        impl.Close();
                        impl = null;
                        continue;
                    }
                    if (impl == null || !impl.Connected)
                    {
                        impl = new TcpClient(config.Endpoint.AsIPEndPoint());
                        implInitialized = true;
                    }

                    ClientReadLoop();
                }
                catch (SocketException socketException)
                {
                    switch (socketException.SocketErrorCode)
                    {
                        case SocketError.Interrupted:
                            return; // we were forcefully canceled - free thread
                        case SocketError.Shutdown:
                            return; // we forcefully freed the socket, so yeah, we will get an error
                        case SocketError.TimedOut:
                            if (stopThread)
                                LogError("timed out");
                            else
                                LogError("timed out - Trying again in " + (ReconnectTimeoutMs / 1000f) + " sec");
                            //TODO: stats.RecordStreamError();
                            break;
                        case SocketError.ConnectionRefused:
                            if (stopThread)
                                LogError("connection refused! Are you sure the server is running?");
                            else
                                LogError("connection refused! Are you sure the server is running? - Trying again in " + (ReconnectTimeoutMs / 1000f) + " sec");
                            // TODO: stats.RecordStreamError();
                            break;
                        case SocketError.NotConnected:
                            // this sounds extra, but sockets that never connected will die with NotConnected
                            if (implInitialized)
                            {
                                LogError(" Socket Exception: " + socketException.SocketErrorCode + "->" + socketException);
                                // TODO: stats.RecordStreamError();
                            }
                            break;
                        default:
                            // if we didn't interrupt it -> reconnect, report statistics, log warning
                            LogError(" Socket Exception: " + socketException.SocketErrorCode + "->" + socketException);
                            // TODO: stats.RecordStreamError();
                            break;
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
                catch (SocketDisconnected)
                {
                    // this is our very own exception for when a client disconnects during a read
                    // do nothing.. finally will take care of it below
                }
                catch (Exception e)
                {
                    // this is likely not a socket error. So while we do not record a stream error,
                    // we still log for later learning about it
                    LogWarning("Exception " + e);
                }
                finally
                {
                    if (implInitialized)
                    {
                        if (stopThread)
                            Log("Disconnected");
                        else
                            Log("Disconnected - Trying again in " + (ReconnectTimeoutMs / 1000f) + " sec");
                        // TODO: statisticsReporter.RecordStreamDisconnect();
                        RunOnUnityThread(() =>
                        {
                            OnClientDisconnected.Invoke(this);
                        });
                    }
                }
            }
        }

        private void ClientReadLoop()
        {
            using (NetworkStream stream = impl.GetStream())
            {
                // TODO: stats.RecordConnectionEstablished
                try
                {
                    while (!stopThread)
                    {
                        this.MessageParser(stream);
                    }
                }
                catch (System.IO.IOException ioException)
                {
                    // when stream read fails, it throws IOException.
                    // let's expose that exception and handle it below
                    throw ioException.InnerException;
                }
            }
        }

        #region Unity Runtime
        #endregion
    }
}