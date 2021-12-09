using System;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class CommMonitorMessenger:MessengerBase
    {
        private const string OnlineStatusPath = "/onlineStatus";
        private const string PollStatusPath = "/status";

        private readonly ICommunicationMonitor _monitor;

        public CommMonitorMessenger(string key, string messagePath) : base(key, messagePath)
        {
        }

        public CommMonitorMessenger(string key, string messagePath, ICommunicationMonitor monitor)
            : this(key, messagePath)
        {
            _monitor = monitor;
        }

        private void SendStatus()
        {
            var messageObj = new
            {
                commMonitor = new {
                    online = _monitor.CommunicationMonitor.IsOnline,
                    status = _monitor.CommunicationMonitor.Status.ToString()
                }
            };

            PostStatusMessage(messageObj);
        }

        #region Overrides of MessengerBase

        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
            appServerController.AddAction(MessagePath + "/fullStatus", new Action(SendStatus));
            
            _monitor.CommunicationMonitor.IsOnlineFeedback.OutputChange += IsOnlineFeedbackOnOutputChange;
            _monitor.CommunicationMonitor.StatusChange += CommunicationMonitorOnStatusChange;
        }

        private void CommunicationMonitorOnStatusChange(object sender, MonitorStatusChangeEventArgs monitorStatusChangeEventArgs)
        {
            var messageObj = new
            {
                type = MessagePath + PollStatusPath,
                content = new
                {
                    commMonitor = new {
                        status = monitorStatusChangeEventArgs.Status.ToString()
                    }
                }
            };

            AppServerController.SendMessageObject(messageObj);
        }

        private void IsOnlineFeedbackOnOutputChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            var messageObj = new
            {
                type = MessagePath + OnlineStatusPath,
                content = new
                {
                    commMonitor = new {
                        online = feedbackEventArgs.BoolValue
                    }
                }
            };

            AppServerController.SendMessageObject(messageObj);
        }

        #endregion
    }
}