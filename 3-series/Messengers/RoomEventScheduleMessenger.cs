﻿using System;
using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Room.Config;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class RoomEventScheduleMessenger:MessengerBase
    {
        private readonly IRoomEventSchedule _room;
        public RoomEventScheduleMessenger(string key, string messagePath) : base(key, messagePath)
        {
        }

        public RoomEventScheduleMessenger(string key, string messagePath, IRoomEventSchedule room)
            : this(key, messagePath)
        {
            _room = room;
        }

        #region Overrides of MessengerBase

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            appServerController.AddAction(MessagePath + "/save", (id, content) => SaveScheduledEvents(content.ToObject<List<ScheduledEventConfig>>()));
            appServerController.AddAction(MessagePath + "/fullStatus", (id, content) =>
            {
                var events = _room.GetScheduledEvents();

                SendFullStatus(events);
            });

            _room.ScheduledEventsChanged += (sender, args) =>  SendFullStatus(args.ScheduledEvents);
        }

        #endregion

        private void SaveScheduledEvents(List<ScheduledEventConfig> events)
        {
            foreach (var evt in events)
            {
                SaveScheduledEvent(evt);
            }
        }

        private void SaveScheduledEvent(ScheduledEventConfig eventConfig)
        {
            try
            {
                _room.AddOrUpdateScheduledEvent(eventConfig);
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Exception saving event: {0}\r\n{1}", ex.Message, ex.StackTrace);
            }
        }

        private void SendFullStatus(List<ScheduledEventConfig> events) 
        {

                var message = new
                {
                    scheduleEvents = events,
                };

                PostStatusMessage(message);
        }
    }
}