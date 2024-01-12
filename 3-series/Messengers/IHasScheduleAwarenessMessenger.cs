using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Essentials.Devices.Common.Codec;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class IHasScheduleAwarenessMessenger : MessengerBase
    {
        public IHasScheduleAwareness ScheduleSource { get; private set; }

        public IHasScheduleAwarenessMessenger(string key, IHasScheduleAwareness scheduleSource, string messagePath)
            :base(key, messagePath)
        {
            if (scheduleSource == null)
            {
                throw new ArgumentNullException("scheduleSource");
            }

            ScheduleSource = scheduleSource;
            ScheduleSource.CodecSchedule.MeetingsListHasChanged += new EventHandler<EventArgs>(CodecSchedule_MeetingsListHasChanged);
            ScheduleSource.CodecSchedule.MeetingEventChange += new EventHandler<MeetingEventArgs>(CodecSchedule_MeetingEventChange);
        }

        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
        {
            appServerController.AddAction(MessagePath + "/fullStatus", new Action(SendFullScheduleObject));

        }

        void CodecSchedule_MeetingEventChange(object sender, MeetingEventArgs e)
        {
            PostStatusMessage(new
            {
                meetingChange = new
                {
                    changeType = e.ChangeType.ToString(),
                    meeting = e.Meeting
                }
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
            PostStatusMessage(new
                {
                    meetings = ScheduleSource.CodecSchedule.Meetings,
                    meetingWarningMinutes = ScheduleSource.CodecSchedule.MeetingWarningMinutes
                });
        }
    }
}