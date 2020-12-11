using System;
using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Room.Config;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class RoomEventScheduleMessenger:MessengerBase
    {
        private readonly EssentialsTechRoom _room;
        public RoomEventScheduleMessenger(string key, string messagePath) : base(key, messagePath)
        {
        }

        public RoomEventScheduleMessenger(string key, string messagePath, EssentialsTechRoom room)
            : this(key, messagePath)
        {
            _room = room;
        }

        #region Overrides of MessengerBase

        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
            appServerController.AddAction(MessagePath + "/save", new Action<ScheduledEventConfig>(SaveScheduledEvent));
            appServerController.AddAction(MessagePath + "/fullStatus", new Action(() =>
            {
                var events = _room.GetScheduledEvents();

                PostStatusMessage(events);
            }));

            _room.ScheduledEventsChanged += (sender, args) => PostStatusMessage(args.ScheduledEvents);
        }

        #endregion

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
    }
}