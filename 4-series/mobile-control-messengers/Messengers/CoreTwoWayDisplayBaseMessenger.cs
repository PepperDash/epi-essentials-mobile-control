using Newtonsoft.Json.Linq;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PepperDash.Core;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class CoreTwoWayDisplayBaseMessenger : MessengerBase
    {
        private readonly TwoWayDisplayBase _display;        

        public CoreTwoWayDisplayBaseMessenger(string key, string messagePath, Device display)
            : base(key, messagePath, display)
        {
            _display = display as TwoWayDisplayBase;
        }

        #region Overrides of MessengerBase

        public void SendFullStatus()
        {
            var messageObj = new TwoWayDisplayBaseStateMessage
            {
                PowerState = _display.PowerIsOnFeedback.BoolValue,
                CurrentInput = _display.CurrentInputFeedback.StringValue
            };

            PostStatusMessage(messageObj);
        }

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            base.CustomRegisterWithAppServer(appServerController);
            if(_display == null)
            {
                Debug.Console(0, this, $"Unable to register TwoWayDisplayBase messenger {Key}");
                return;
            }

            appServerController.AddAction(MessagePath + "/fullStatus", (id, content) => SendFullStatus());

            _display.PowerIsOnFeedback.OutputChange += PowerIsOnFeedbackOnOutputChange;
            _display.CurrentInputFeedback.OutputChange += CurrentInputFeedbackOnOutputChange;
            _display.IsCoolingDownFeedback.OutputChange += IsCoolingFeedbackOnOutputChange;
            _display.IsWarmingUpFeedback.OutputChange += IsWarmingFeedbackOnOutputChange;
        }

        private void CurrentInputFeedbackOnOutputChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            var messageObj = new MobileControlMessage
            {
                Type = MessagePath,
                Content = JToken.FromObject(new
                {
                    currentInput = feedbackEventArgs.StringValue
                })
            };

            AppServerController.SendMessageObject(messageObj);
        }


        private void PowerIsOnFeedbackOnOutputChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            var messageObj = new MobileControlMessage
            {
                Type = MessagePath,
                Content = JToken.FromObject(new
                {
                    powerState = feedbackEventArgs.BoolValue
                })
            };

            AppServerController.SendMessageObject(messageObj);
        }

        private void IsWarmingFeedbackOnOutputChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            var messageObj = new MobileControlMessage
            {
                Type = MessagePath,
                Content = JToken.FromObject(new
                {
                    isWarming = feedbackEventArgs.BoolValue
                })
            };

            AppServerController.SendMessageObject(messageObj);
        }

        private void IsCoolingFeedbackOnOutputChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            var messageObj = new MobileControlMessage
            {
                Type = MessagePath,
                Content = JToken.FromObject(new
                {
                    isCooling = feedbackEventArgs.BoolValue
                })
            };

            AppServerController.SendMessageObject(messageObj);
        }

        #endregion
    }
}
