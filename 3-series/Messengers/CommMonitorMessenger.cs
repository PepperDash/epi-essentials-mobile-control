using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using System;

namespace PepperDash.Essentials.AppServer.Messengers
{
    [Obsolete("Will be deprecated in favour of build in ICommunicationMonitor tracking id MessengerBase")]
    public class CommMonitorMessenger : MessengerBase
    {
        private const string OnlineStatusPath = "/onlineStatus";
        private const string PollStatusPath = "/status";

        private readonly ICommunicationMonitor _monitor;

        public CommMonitorMessenger(string key, string messagePath, ICommunicationMonitor monitor)
            : base(key, messagePath, monitor as Device)
        {
            _monitor = monitor;
        }

        private void SendStatus()
        {
            var messageObj = new
            {
                commMonitor = new
                {
                    online = _monitor.CommunicationMonitor.IsOnline,
                    status = _monitor.CommunicationMonitor.Status.ToString()
                }
            };

            AppSer(messageObj);
        }

        #region Overrides of MessengerBase

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            appServerController.AddAction(MessagePath + "/fullStatus", (id, content) => SendStatus());

            _monitor.CommunicationMonitor.IsOnlineFeedback.OutputChange += IsOnlineFeedbackOnOutputChange;
            _monitor.CommunicationMonitor.StatusChange += CommunicationMonitorOnStatusChange;
        }

        private void CommunicationMonitorOnStatusChange(object sender, MonitorStatusChangeEventArgs monitorStatusChangeEventArgs)
        {
            var messageObj = new MobileControlMessage
            {
                Type = MessagePath + PollStatusPath,
                Content = JToken.FromObject(new
                {
                    commMonitor = new
                    {
                        status = monitorStatusChangeEventArgs.Status.ToString()
                    }
                })
            };

            AppServerController.SendMessageObject(messageObj);
        }

        private void IsOnlineFeedbackOnOutputChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            var messageObj = new MobileControlMessage
            {
                Type = MessagePath + OnlineStatusPath,
                Content = JToken.FromObject(new
                {
                    commMonitor = new
                    {
                        online = feedbackEventArgs.BoolValue
                    }
                })
            };

            AppServerController.SendMessageObject(messageObj);
        }

        #endregion
    }
}