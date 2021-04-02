using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core.Queues;
using WebSocketSharp;

namespace PepperDash.Essentials
{
    public class TransmitMessage : IQueueMessage
    {
        private readonly WebSocket _ws;
        private readonly object msgToSend;

        public TransmitMessage(object msg, WebSocket ws)
        {
            _ws = ws;
            msgToSend = msg;
        }
        #region Implementation of IQueueMessage

        public void Dispatch()
        {
            try
            {
                var messageToSend = JObject.FromObject(msgToSend);

                if (_ws != null && _ws.IsAlive)
                {
                    var message = JsonConvert.SerializeObject(messageToSend, Formatting.None,
                        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                    Debug.Console(2, "Message TX: {0}", message);

                    _ws.Send(message);
                }
                else if (_ws == null)
                {
                    Debug.Console(1, "Cannot send. No client.");
                }
            }
            catch (Exception ex)
            {
                Debug.ConsoleWithLog(0,  "Caught an exception in the Transmit Processor {0}\r{1}\r{2}", ex.Message, ex.InnerException, ex.StackTrace);
            }


        }
        #endregion
    }
}