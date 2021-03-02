using System;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class SimplDirectRouteMessenger:MessengerBase
    {
        private readonly BasicTriList _eisc;

        public MobileControlSIMPLRunDirectRouteActionJoinMap JoinMap { get; private set; }

        public Dictionary<string, DestinationListItem> DestinationList { get; set; }

        public SimplDirectRouteMessenger(string key, BasicTriList eisc, string messagePath) : base(key, messagePath)
        {
            _eisc = eisc;

            JoinMap = new MobileControlSIMPLRunDirectRouteActionJoinMap(1101);

            DestinationList = new Dictionary<string, DestinationListItem>();
        }

        #region Overrides of MessengerBase

        protected override void CustomRegisterWithAppServer(MobileControlSystemController controller)
        {
            //handle routing feedback from SIMPL
            foreach(var destination in DestinationList)
            {
                var key = destination.Key;
                var dest = destination.Value;

                _eisc.SetStringSigAction((uint)(JoinMap.SourceForDestinationJoinStart.JoinNumber + dest.Order),
                    s => UpdateSourceForDestination(s, key));

                controller.AddAction(String.Format("{0}/{1}/selectSource", MessagePath, dest.SinkKey),
                    new Action<string>(
                        s =>
                            _eisc.StringInput[JoinMap.SourceForDestinationJoinStart.JoinNumber + key].StringValue = s));
            }

            //Audio source
            _eisc.SetStringSigAction(JoinMap.SourceForDestinationAudio.JoinNumber,
                (s) => controller.SendMessageObjectToServer(new
                {
                    path = MessagePath + "/audio/currentSource",
                    content = new
                    {
                        selectedSourceKey = s,
                    }
                }));

            controller.AddAction(MessagePath + "/audio/selectSource",
                new Action<string>(
                    (s) => _eisc.StringInput[JoinMap.SourceForDestinationAudio.JoinNumber].StringValue = s));



            controller.AddAction(MessagePath + "/fullStatus", new Action(() =>
            {
                foreach (var dest in DestinationList)
                {
                    var key = dest.Key;
                    var item = dest.Value;

                    var source =
                        _eisc.StringOutput[(uint)(JoinMap.SourceForDestinationJoinStart.JoinNumber + item.Order)].StringValue;

                    UpdateSourceForDestination(source, key);
                }
            }));
        }

        #endregion

        private void UpdateSourceForDestination(string sourceKey, string destKey)
        {
            AppServerController.SendMessageObjectToServer(new
            {
                path = String.Format("{0}/{1}/currentSource", MessagePath, destKey),
                content = new
                {
                    selectedSourceKey = sourceKey
                }
            });
        }
    }


}