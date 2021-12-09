﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using PepperDash.Core;
using PepperDash.Essentials.Core.Queues;
using WebSocketSharp;
using PepperDash.Essentials.AppServer.Messengers;

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

        public TransmitMessage(DeviceStateMessageBase msg, WebSocket ws)
        {
            _ws = ws;
            msgToSend = msg;
        }

        #region Implementation of IQueueMessage

        public void Dispatch()
        {
            try
            {

                //Debug.Console(2, "Dispatching message type: {0}", msgToSend.GetType());

                //Debug.Console(2, "Message: {0}", msgToSend.ToString());

                //var messageToSend = JObject.FromObject(msgToSend);

                if (_ws != null && _ws.IsAlive)
                {
                    var message = JsonConvert.SerializeObject(msgToSend, Formatting.None,
                        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Converters = {new IsoDateTimeConverter()} });

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


#if SERIES4
    public class MessageToClients : IQueueMessage
    {
        private readonly MobileControlWebsocketServer _server;
        private readonly object msgToSend;

        public MessageToClients(object msg, MobileControlWebsocketServer server)
        {
            _server = server;
            msgToSend = msg;
        }

        public MessageToClients(DeviceStateMessageBase msg, MobileControlWebsocketServer server)
        {
            _server = server;
            msgToSend = msg;
        }

        #region Implementation of IQueueMessage

        public void Dispatch()
        {
            try
            {

                //Debug.Console(2, "Dispatching message type: {0}", msgToSend.GetType());

                //Debug.Console(2, "Message: {0}", msgToSend.ToString());

                //var messageToSend = JObject.FromObject(msgToSend);

                if (_server != null)
                {
                    var message = JsonConvert.SerializeObject(msgToSend, Formatting.None,
                        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Converters = { new IsoDateTimeConverter() } });

                    Debug.Console(2, "Message TX: {0}", message);

                    _server.SendMessageToAllClients(message);
                }
                else if (_server == null)
                {
                    Debug.Console(1, "Cannot send. No server.");
                }
            }
            catch (Exception ex)
            {
                Debug.ConsoleWithLog(0, "Caught an exception in the Transmit Processor {0}\r{1}\r{2}", ex.Message, ex.InnerException, ex.StackTrace);
            }


        }
        #endregion
    }
#endif
}