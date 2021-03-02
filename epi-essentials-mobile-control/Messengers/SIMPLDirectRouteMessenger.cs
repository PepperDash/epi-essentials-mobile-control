using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common.VideoCodec;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class SIMPLDirectRouteMessenger:MessengerBase
    {
        private readonly BasicTriList _eisc;

        public MobileControlSIMPLRunDirectRouteActionJoinMap JoinMap { get; private set; }

        public Dictionary<uint, DestinationListItem> DestinationList { get; set; }

        public SIMPLDirectRouteMessenger(string key, BasicTriList eisc, string messagePath) : base(key, messagePath)
        {
            _eisc = eisc;

            JoinMap = new MobileControlSIMPLRunDirectRouteActionJoinMap(1101);

            DestinationList = new Dictionary<uint, DestinationListItem>();
        }

        #region Overrides of MessengerBase

        protected override void CustomRegisterWithAppServer(MobileControlSystemController controller)
        {
            //handle routing feedback from SIMPL
            foreach(var destination in DestinationList)
            {
                var index = destination.Key;
                var dest = destination.Value;

                _eisc.SetStringSigAction(JoinMap.SourceForDestinationJoinStart.JoinNumber + index,
                    (s) => UpdateSourceForDestination(s, dest));

                controller.AddAction(String.Format("{0}/{1}/selectSource", MessagePath, dest.SinkKey),
                    new Action<string>(
                        (s) =>
                            _eisc.StringInput[JoinMap.SourceForDestinationJoinStart.JoinNumber + index].StringValue = s));
            }

            controller.AddAction(MessagePath + "/fullStatus", new Action(() =>
            {

                var status = DestinationList.Select((d) =>
                {
                    var responseObject = new
                    {
                        destinationSinkKey = d.Value.SinkKey,
                        selectedSourceKey =
                            _eisc.StringOutput[JoinMap.SourceForDestinationJoinStart.JoinNumber + d.Key].StringValue
                    };

                    return responseObject;
                }).ToList();

                PostStatusMessage(status);
            }));
        }

        #endregion

        private void UpdateSourceForDestination(string sourceKey, DestinationListItem dest)
        {
            AppServerController.SendMessageObjectToServer(new
            {
                path = String.Format("{0}/{1}/currentSource", MessagePath, dest.SinkKey),
                content = new
                {
                    selectedSourceKey = sourceKey
                }
            });
        }
    }


}