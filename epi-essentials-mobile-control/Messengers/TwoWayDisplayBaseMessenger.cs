using System;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class TwoWayDisplayBaseMessenger:MessengerBase
    {
        private const string PowerStatusPath = "/powerStatus";
        private const string InputStatusPath = "/inputStatus";
        private readonly TwoWayDisplayBase _display;

        public TwoWayDisplayBaseMessenger(string key, string messagePath) : base(key, messagePath)
        {
        }

        public TwoWayDisplayBaseMessenger(string key, string messagePath, TwoWayDisplayBase display)
            : this(key, messagePath)
        {
            _display = display;
        }

        #region Overrides of MessengerBase

        public void SendFullStatus()
        {
            var messageObj = new
            {
                powerState = _display.PowerIsOnFeedback.BoolValue,
                currentInput = _display.CurrentInputFeedback.StringValue
            };

            PostStatusMessage(messageObj);
        }

        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
            appServerController.AddAction(MessagePath + "/fullStatus", new Action(SendFullStatus));

            _display.PowerIsOnFeedback.OutputChange += PowerIsOnFeedbackOnOutputChange;
            _display.CurrentInputFeedback.OutputChange += CurrentInputFeedbackOnOutputChange;
        }

        private void CurrentInputFeedbackOnOutputChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            var messageObj = new
            {
                type = String.Format("{0}{1}", MessagePath, InputStatusPath),
                content = new
                {
                    currentInput = feedbackEventArgs.StringValue
                }
            };

            AppServerController.SendMessageObjectToServer(messageObj);
        }


        private void PowerIsOnFeedbackOnOutputChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            var messageObj = new
            {
                type = String.Format("{0}{1}", MessagePath, PowerStatusPath),
                content = new
                {
                    powerState = feedbackEventArgs.BoolValue
                }
            };

            AppServerController.SendMessageObjectToServer(messageObj);
        }

        #endregion
    }
}