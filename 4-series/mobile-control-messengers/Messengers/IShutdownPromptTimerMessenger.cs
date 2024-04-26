using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class IShutdownPromptTimerMessenger : MessengerBase
    {
        private readonly IShutdownPromptTimer _room;

        public IShutdownPromptTimerMessenger(string key, string messagePath, IShutdownPromptTimer room)
            : base(key, messagePath, room as Device)
        {
            _room = room;
        }

        protected override void RegisterActions()
        {
            AddAction("/status", (id, content) =>
            {
                SendFullStatus();
            });

            AddAction("/setShutdownPromptSeconds", (id, content) =>
            {
                var response = content.ToObject<int>();

                _room.SetShutdownPromptSeconds(response);
            });

            _room.ShutdownPromptTimer.HasStarted += (sender, args) =>
            {
                var status = new IShutdownPromptTimerEventMessage
                {
                    TimerStarted = true
                };

                PostEventMessage(status);
            };

            _room.ShutdownPromptTimer.HasFinished += (sender, args) =>
            {
                var status = new IShutdownPromptTimerEventMessage
                {
                    TimerFinished = true
                };

                PostEventMessage(status);
            };

            _room.ShutdownPromptTimer.WasCancelled += (sender, args) =>
            {
                var status = new IShutdownPromptTimerEventMessage
                {
                    TimerCancelled = true
                };

                PostEventMessage(status);
            };

            _room.ShutdownPromptTimer.TimeRemainingFeedback.OutputChange += (sender, args) =>
            {
                var status = new
                {
                    timeRemaining = _room.ShutdownPromptTimer.TimeRemainingFeedback.StringValue
                };
            };
        }

        private void SendFullStatus()
        {
            var status = new IShutdownPromptTimerStateMessage
            {
                ShutdownPromptSeconds = _room.ShutdownPromptTimer.SecondsToCount,
                TimeRemaining = _room.ShutdownPromptTimer.TimeRemainingFeedback.StringValue,
            };

            PostStatusMessage(status);
        }
    }


    public class IShutdownPromptTimerStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("secondsRemaining")]
        public string TimeRemaining { get; set; }

        [JsonProperty("shutdownPromptSeconds")]
        public int ShutdownPromptSeconds { get; set; }
    }

    public class IShutdownPromptTimerEventMessage : DeviceEventMessageBase
    {
        [JsonProperty("timerFinished")]
        public bool TimerFinished { get; set; }

        [JsonProperty("timerStarted")]
        public bool TimerStarted { get; set; }

        [JsonProperty("timerCancelled")]
        public bool TimerCancelled { get; set; }
    }

}
