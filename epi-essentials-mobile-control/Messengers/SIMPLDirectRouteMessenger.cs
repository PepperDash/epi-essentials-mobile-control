using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Org.BouncyCastle.Asn1.X509.Qualified;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common.VideoCodec;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class SIMPLDirectRouteMessenger:MessengerBase
    {
        private readonly BasicTriList _eisc;

        public MobileControlSIMPLRunDirectRouteActionJoinMap JoinMap { get; private set; }

        public Dictionary<string, DestinationListItem> DestinationList { get; set; }

        public SIMPLDirectRouteMessenger(string key, BasicTriList eisc, string messagePath) : base(key, messagePath)
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
                    (s) => UpdateSourceForDestination(s, key));

                controller.AddAction(String.Format("{0}/{1}/selectSource", MessagePath, dest.SinkKey),
                    new Action<string>(
                        (s) =>
                            _eisc.StringInput[JoinMap.SourceForDestinationJoinStart.JoinNumber + key].StringValue = s));
            }

            controller.AddAction(MessagePath + "/fullStatus", new Action(() =>
            {

                var status = DestinationList.Select((d) =>
                {
                    var responseObject = new
                    {
                        destinationKey = d.Key,
                        selectedSourceKey =
                            _eisc.StringOutput[JoinMap.SourceForDestinationJoinStart.JoinNumber + d.Key].StringValue
                    };

                    return responseObject;
                }).ToDictionary((d) => d.destinationKey, (d) => new {d.selectedSourceKey});

                PostStatusMessage(status);
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