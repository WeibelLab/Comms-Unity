using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;

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

        public bool isConnected { get; private set; }


        public ReliableClient(TcpClient from, Config config, ReliableServer attachTo=null)
        {

        }



        override public void Send(byte[] data)
        {
            throw new System.NotImplementedException();
        }

        protected override void DefaultMessageParser(object o)
        {
            throw new System.NotImplementedException();
        }
    }
}