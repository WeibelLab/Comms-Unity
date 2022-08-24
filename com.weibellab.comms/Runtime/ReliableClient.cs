using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Comms
{
    public class ReliableClient : Client
    {
        public Header Header;

        override public void Send(byte[] data)
        {
            throw new System.NotImplementedException();
        }
    }
}