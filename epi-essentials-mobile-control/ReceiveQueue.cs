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
    /// Provides a disposable, threadsafe mechnism for enqueing and processing responses
    /// </summary>
    public class ReceiveQueue : IDisposable, IKeyed
    {
        private readonly CrestronQueue<string> _queue;
        private readonly Thread _worker;
        private readonly CEvent _wh = new CEvent();
        private readonly Action<string> _processResponseAction;

        public bool Disposed { get; private set; }

        public ReceiveQueue(string key, Action<string> processResponse)
        {
            Key = key;
            _queue = new CrestronQueue<string>();
            _processResponseAction = processResponse;
            _worker = new Thread(ProcessMessage, null, Thread.eThreadStartOptions.Running)
            {
                Priority = Thread.eThreadPriority.HighPriority
            };

            CrestronEnvironment.ProgramStatusEventHandler += programEvent =>
            {
                if (programEvent != eProgramStatusEventType.Stopping)
                    return;

                Dispose();
            };
        }

        private object ProcessMessage(object obj)
        {
            while (true)
            {
                string response = null;

                if (_queue.Count > 0)
                {
                    response = _queue.Dequeue();
                    if (response == null)
                        break;
                }
                if (response != null)
                {
                    try
                    {
                        _processResponseAction(response);
                    }
                    catch (Exception ex)
                    {
                        Debug.ConsoleWithLog(0, this, "Caught an exception in the Response Processor {0}\r{1}\r{2}", ex.Message, ex.InnerException, ex.StackTrace);
                    }
                }
                else _wh.Wait();
            }

            return null;
        }

        /// <summary>
        /// Enqueues a response to be processed
        /// </summary>
        /// <param name="response">response to be processed</param>
        public void EnqueueResponse(string response)
        {
            _queue.Enqueue(response);
            _wh.Set();
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
                EnqueueResponse(null);
                _worker.Join();
                _wh.Close();
            }

            Disposed = true;
        }

        ~ReceiveQueue()
        {
            Dispose(false);
        }

        public string Key { get; private set; }
    }
}