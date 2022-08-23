using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Events;

namespace Comms
{

    [System.Serializable]
    public class CommunicationJsonEvent : UnityEvent<JSONObject> { }
    [System.Serializable]
    public class CommunicationStringEvent : UnityEvent<string> { }
    [System.Serializable]
    public class CommunicationByteEvent : UnityEvent<byte[]> { }



    [System.Serializable]
    public class SocketConnectedEvent : UnityEvent<Client> { }

    [System.Serializable]
    public class SocketDisconnectedEvent : UnityEvent<Client> { }

    /// <summary>
    /// Called when ReliableCommunicationSocket raises an error (int -> socket error; string -> message converted to text)
    /// </summary>
    [System.Serializable]
    public class SocketErrorEvent : UnityEvent<Client, int, string> { }


    [System.Serializable]
    public enum CommunicationMessageType { Byte, String, Json }

    [System.Serializable]
    public enum CommunicationHeaderType { Length, Time }

    [System.Serializable]
    public enum TargettingStrategy { Manual, Web, UDP }

    [System.Serializable]
    public struct CommunicationEndpoint
    {
        [SerializeField]
        public string Name;
        [SerializeField]
        public string Address;
        [SerializeField]
        public int Port;
        private IPEndPoint ipendpoint;

        public CommunicationEndpoint(string Address, int Port, string Name = "device")
        {
            this.Name = Name;
            this.Address = Address;
            this.Port = Port;
            this.ipendpoint = new IPEndPoint(IPAddress.Parse(this.Address), this.Port);
        }

        public CommunicationEndpoint(IPEndPoint endpoint, string Name="device") {
            this.Name = Name;
            this.ipendpoint = endpoint;
            this.Address = endpoint.Address.ToString();
            this.Port = endpoint.Port;
        }

        public void SetAddress(string address)
        {
            this.Address = address;
            this.ipendpoint.Address = IPAddress.Parse(address);
        }
        public void SetPort(int port)
        {
            this.Port = port;
            this.ipendpoint.Port = port;
        }

        public void SetName(string name)
        {
            this.Name = name;
        }
        public override string ToString()
        {
            return string.Format("\"{0}\" ({1}:{2})", Name, Address, Port);
        }

        public IPEndPoint AsIPEndPoint()
        {
            if (this.ipendpoint == null)
            {
                this.ipendpoint = new IPEndPoint(IPAddress.Parse(this.Address), this.Port);
            }

            return this.ipendpoint;
        }

        public bool Equals(CommunicationEndpoint other)
        {
            return other.Address.Equals(this.Address) && other.Port == this.Port;
        }

        public bool Equals(IPEndPoint other)
        {
            return other.Address.ToString().Equals(this.Address) && other.Port == this.Port;
        }
    }
}