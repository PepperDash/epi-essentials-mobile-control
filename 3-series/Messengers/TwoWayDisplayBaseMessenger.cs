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
            _display.IsCoolingDownFeedback.OutputChange += IsCoolingFeedbackOnOutputChange;
            _display.IsWarmingUpFeedback.OutputChange += IsWarmingFeedbackOnOutputChange;
        }

        private void CurrentInputFeedbackOnOutputChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            var messageObj = new
            {
                type = MessagePath,
                content = new
                {
                    currentInput = feedbackEventArgs.StringValue
                }
            };

            AppServerController.SendMessageObject(messageObj);
        }


        private void PowerIsOnFeedbackOnOutputChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            var messageObj = new
            {
                type = MessagePath,
                content = new
                {
                    powerState = feedbackEventArgs.BoolValue
                }
            };

            AppServerController.SendMessageObject(messageObj);
        }

        private void IsWarmingFeedbackOnOutputChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            var messageObj = new
            {
                type = MessagePath,
                content = new
                {
                    isWarming = feedbackEventArgs.BoolValue
                }
            };

            AppServerController.SendMessageObject(messageObj);
        }

        private void IsCoolingFeedbackOnOutputChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            var messageObj = new
            {
                type = MessagePath,
                content = new
                {
                    isCooling = feedbackEventArgs.BoolValue
                }
            };

            AppServerController.SendMessageObject(messageObj);
        }

        #endregion
    }
}