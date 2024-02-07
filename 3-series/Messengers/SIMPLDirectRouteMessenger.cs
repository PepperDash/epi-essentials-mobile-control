using System;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

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

            JoinMap = new MobileControlSIMPLRunDirectRouteActionJoinMap(851);

            DestinationList = new Dictionary<string, DestinationListItem>();
        }

        #region Overrides of MessengerBase

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 controller)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController controller)
#endif
        {
            Debug.Console(2, "********** Direct Route Messenger CustomRegisterWithAppServer **********");


            //Audio source
            _eisc.SetStringSigAction(JoinMap.SourceForDestinationAudio.JoinNumber,
                s => controller.SendMessageObject(new MobileControlMessage
                {
                    Type = MessagePath + "/programAudio/currentSource",
                    Content = JToken.FromObject(new
                    {
                        selectedSourceKey = s,
                    })
                }));

            controller.AddAction(MessagePath + "/programAudio/selectSource", (id, content) => {
                var msg = content.ToObject<MobileControlSimpleContent<string>>();

                _eisc.StringInput[JoinMap.SourceForDestinationAudio.JoinNumber].StringValue = msg.Value;
            });

            controller.AddAction(MessagePath + "/fullStatus", (id, content) =>
            {
                foreach (var dest in DestinationList)
                {
                    var key = dest.Key;
                    var item = dest.Value;

                    var source =
                        _eisc.StringOutput[(uint)(JoinMap.SourceForDestinationJoinStart.JoinNumber + item.Order)].StringValue;

                    UpdateSourceForDestination(source, key);
                }

                controller.SendMessageObject(new MobileControlMessage
                {
                    Type = MessagePath + "/programAudio/currentSource",
                    Content = JToken.FromObject(new
                    {
                        selectedSourceKey = _eisc.StringOutput[JoinMap.SourceForDestinationAudio.JoinNumber].StringValue
                    })
                });

                controller.SendMessageObject(new MobileControlMessage
                {
                    Type = MessagePath + "/advancedSharingMode",
                    Content = JToken.FromObject(new
                    {
                        advancedSharingActive = _eisc.BooleanOutput[JoinMap.AdvancedSharingModeFb.JoinNumber].BoolValue
                    })
                });
            });

            controller.AddAction(MessagePath + "/advancedSharingMode",(id, content) =>
            {
                var b = content.ToObject<MobileControlSimpleContent<bool>>();

                Debug.Console(1, "Current Sharing Mode: {2}\r\nadvanced sharing mode: {0} join number: {1}", b.Value,
                    JoinMap.AdvancedSharingModeOn.JoinNumber,
                    _eisc.BooleanOutput[JoinMap.AdvancedSharingModeOn.JoinNumber].BoolValue);
           
                _eisc.SetBool(JoinMap.AdvancedSharingModeOn.JoinNumber, b.Value);
                _eisc.SetBool(JoinMap.AdvancedSharingModeOff.JoinNumber, !b.Value);
                _eisc.PulseBool(JoinMap.AdvancedSharingModeToggle.JoinNumber);
            });

            _eisc.SetBoolSigAction(JoinMap.AdvancedSharingModeFb.JoinNumber,
                (b) => controller.SendMessageObject(new MobileControlMessage
                {
                    Type = MessagePath + "/advancedSharingMode",
                    Content = JToken.FromObject(new
                    {
                        advancedSharingActive = b
                    })
                }));
        }

        public void RegisterForDestinationPaths()
        {
            //handle routing feedback from SIMPL
            foreach (var destination in DestinationList)
            {
                var key = destination.Key;
                var dest = destination.Value;

                _eisc.SetStringSigAction((uint)(JoinMap.SourceForDestinationJoinStart.JoinNumber + dest.Order),
                    s => UpdateSourceForDestination(s, key));

                AppServerController.AddAction(String.Format("{0}/{1}/selectSource", MessagePath, key), (id, content) => {
                    var s = content.ToObject<MobileControlSimpleContent<string>>();

                    _eisc.StringInput[(uint)(JoinMap.SourceForDestinationJoinStart.JoinNumber + dest.Order)].StringValue = s.Value;
                });
           
            }            
        }

        #endregion

        private void UpdateSourceForDestination(string sourceKey, string destKey)
        {
            AppServerController.SendMessageObject(new MobileControlMessage
            {
                Type = String.Format("{0}/{1}/currentSource", MessagePath, destKey),
                Content = JToken.FromObject(new
                {
                    selectedSourceKey = sourceKey
                })
            });
        }
    }


}