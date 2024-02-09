using Newtonsoft.Json.Linq;
using PepperDash.Essentials.Core;
using TwoWayDisplayBase = PepperDash.Essentials.Devices.Common.Displays.TwoWayDisplayBase;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using Newtonsoft.Json;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class TwoWayDisplayBaseMessenger:MessengerBase
    {
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

    public class TwoWayDisplayBaseStateMessage: DeviceStateMessageBase
    {
        [JsonProperty("powerState", NullValueHandling = NullValueHandling.Ignore)]
        public bool? PowerState { get; set; }

        [JsonProperty("currentInput", NullValueHandling = NullValueHandling.Ignore)]
        public string CurrentInput { get; set; }
    }
}