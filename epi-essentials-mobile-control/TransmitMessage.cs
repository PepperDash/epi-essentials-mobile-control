using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Newtonsoft.Json.Linq;
using PepperDash.Essentials.Core.Queues;
using WebSocketSharp;

namespace PepperDash.Essentials
{
    public class TransmitMessage:IQueueMessage
    {
        private readonly WebSocket _ws;
        private readonly JObject msgToSend;

        public TransmitMessage(JObject msg, WebSocket ws)
        {
            _ws = ws;
            msgToSend = msg;
        }
        #region Implementation of IQueueMessage

        public void Dispatch()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}