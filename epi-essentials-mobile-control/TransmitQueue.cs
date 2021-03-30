using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using WebSocketSharp;

namespace PepperDash.Essentials
{
    /// <summary>
    /// Provides a disposable, threadsafe mechnism for enqueing and dispatching objects as messages to a websocket
    /// </summary>
    public class TransmitQueue : IDisposable, IKeyed
    {
        private readonly CrestronQueue<object> _queue;
        private readonly Thread _worker;
        private readonly CEvent _wh = new CEvent();
        private WebSocket _wsClient;

        public WebSocket WsClient
        {
            set
            {
                _wsClient = value;
                _wh.Set();
            }
        }

        public bool Disposed { get; private set; }

        public TransmitQueue(string key)
        {
            _queue = new CrestronQueue<object>();
            _worker = new Thread(ProcessMessage, null, Thread.eThreadStartOptions.Running)
            {
                Priority = Thread.eThreadPriority.HighPriority
            };

            Key = key;

            //CrestronEnvironment.ProgramStatusEventHandler += programEvent =>
            //{
            //    if (programEvent != eProgramStatusEventType.Stopping)
            //        return;

            //    //Dispose();
            //};
        }

        private object ProcessMessage(object obj)
        {
            while (true)
            {
                object message = null;

                if (_queue.Count > 0)
                {
                    message = _queue.Dequeue();
                    if (message == null)
                        break;
                }
                if (message != null)
                {
                    try
                    {
                        var messageToSend = JObject.FromObject(message);
                        SendMessageToServer(messageToSend);
                    }
                    catch (Exception ex)
                    {
                        Debug.ConsoleWithLog(0, this, "Caught an exception in the Transmit Processor {0}\r{1}\r{2}", ex.Message, ex.InnerException, ex.StackTrace);
                    }
                }
                else _wh.Wait();
            }

            return null;
        }

        /// <summary>
        /// Enqueues a message to be sent
        /// </summary>
        /// <param name="message">object to be serialized and sent in post body</param>
        public void EnqueueMessage(object message)
        {
            if (Disposed)
            {
                Debug.Console(1, this, "This class has been disposed and cannot enqueue any messages.  Are you trying to dispatch a message while the program is stopping?");
                return;
            }

            _queue.Enqueue(message);
            if (_wsClient == null)
                return;

            _wh.Set();
        }

        /// <summary>
        /// Sends a message to the server from a room
        /// </summary>
        /// <param name="o">object to be serialized and sent in post body</param>
        private void SendMessageToServer(JObject o)
        {
            if (_wsClient != null && _wsClient.IsAlive)
            {
                var message = JsonConvert.SerializeObject(o, Formatting.None,
                    new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore});

                if (!message.Contains("/system/heartbeat"))
                {
                    Debug.Console(2, this, "Message TX: {0}", message);
                }
                _wsClient.Send(message);
            }
            else if (_wsClient == null)
            {
                Debug.Console(1, this, "Cannot send. No client.");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            CrestronEnvironment.GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Disposed)
                return;

            if (disposing)
            {
                EnqueueMessage(null);
                _worker.Join();
                _wh.Close();
            }

            Disposed = true;
        }

        ~TransmitQueue()
        {
            Dispose(false);
        }

        public string Key { get; private set; }
    }
}