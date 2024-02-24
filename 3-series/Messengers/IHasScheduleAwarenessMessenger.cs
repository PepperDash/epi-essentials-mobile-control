using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class IHasScheduleAwarenessMessenger : MessengerBase
    {
        public IHasScheduleAwareness ScheduleSource { get; private set; }

        public IHasScheduleAwarenessMessenger(string key, IHasScheduleAwareness scheduleSource, string messagePath)
            :base(key, messagePath, scheduleSource as Device)
        {
            if (scheduleSource == null)
            {
                throw new ArgumentNullException("scheduleSource");
            }

            ScheduleSource = scheduleSource;
            ScheduleSource.CodecSchedule.MeetingsListHasChanged += new EventHandler<EventArgs>(CodecSchedule_MeetingsListHasChanged);
            ScheduleSource.CodecSchedule.MeetingEventChange += new EventHandler<MeetingEventArgs>(CodecSchedule_MeetingEventChange);
        }

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            appServerController.AddAction(MessagePath + "/fullStatus", (id, content) => SendFullScheduleObject());
        }

        void CodecSchedule_MeetingEventChange(object sender, MeetingEventArgs e)
        {
            AppServerController.SendMessageObject(new MobileControlMessage
            {
                Type = MessagePath,
                Content = JToken.FromObject( new MeetingChangeMessage
                {
                    MeetingChange = new MeetingChange 
                {
                    ChangeType = e.ChangeType.ToString(),
                    Meeting = e.Meeting
                }
                })
            });
        }

        void CodecSchedule_MeetingsListHasChanged(object sender, EventArgs e)
        {
            SendFullScheduleObject();
        }

        /// <summary>
        /// Helper method to send the full schedule data
        /// </summary>
        private void SendFullScheduleObject()
        {
            PostStatusMessage(new FullScheduleMessage
                {
                    Meetings = ScheduleSource.CodecSchedule.Meetings,
                    MeetingWarningMinutes = ScheduleSource.CodecSchedule.MeetingWarningMinutes
                });
        }
    }

    public class FullScheduleMessage : DeviceStateMessageBase
    {
        [JsonProperty("meetings", NullValueHandling = NullValueHandling.Ignore)]
        public List<Meeting> Meetings { get; set; }

        [JsonProperty("meetingWarningMinutes", NullValueHandling = NullValueHandling.Ignore)]
        public int MeetingWarningMinutes { get; set; }
    }

    public class MeetingChangeMessage
    {
        [JsonProperty("meetingChange", NullValueHandling = NullValueHandling.Ignore)]
        public MeetingChange MeetingChange { get; set; }
    }

    public class MeetingChange
    {
        [JsonProperty("changeType", NullValueHandling = NullValueHandling.Ignore)]
        public string ChangeType { get; set; }

        [JsonProperty("meeting", NullValueHandling = NullValueHandling.Ignore)]
        public Meeting Meeting { get; set; }
    }
}