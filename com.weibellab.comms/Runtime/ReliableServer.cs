using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Comms
{
    public class ReliableServer : Server
    {
        public Header header;

        protected override void CloseSocket()
        {
            throw new System.NotImplementedException();
        }

        protected override void DisposeSocket()
        {
            throw new System.NotImplementedException();
        }

        protected override void SetupSocket()
        {
            throw new System.NotImplementedException();
        }

        protected override void ServerThreadLoop()
        {
            throw new System.NotImplementedException();
        }
    }
}
