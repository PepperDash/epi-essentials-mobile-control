﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class IHasCurrentSourceInfoMessenger : MessengerBase
    {
        private readonly IHasCurrentSourceInfoChange sourceDevice;
        public IHasCurrentSourceInfoMessenger(string key, string messagePath, IHasCurrentSourceInfoChange device) : base(key, messagePath, device as IKeyName)
        {
            sourceDevice = device;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) =>
            {
                var message = new CurrentSourceStateMessage
                {
                    CurrentSourceKey = sourceDevice.CurrentSourceInfoKey,
                    CurrentSource = sourceDevice.CurrentSourceInfo
                };
            });

            sourceDevice.CurrentSourceChange += (sender, e) => {
                switch (e)
                {
                    case ChangeType.DidChange:
                        {
                            PostStatusMessage(JToken.FromObject(new
                            {
                                currentSourceKey = sourceDevice.CurrentSourceInfoKey,
                                currentSource = sourceDevice.CurrentSourceInfo
                            }));
                            break;
                        }
                }
            };            
        }
    }

    public class CurrentSourceStateMessage: DeviceStateMessageBase
    {
        [JsonProperty("currentSourceKey", NullValueHandling = NullValueHandling.Ignore)]
        public string CurrentSourceKey { get; set; }

        [JsonProperty("currentSource", NullValueHandling = NullValueHandling.Ignore)]
        public SourceListItem CurrentSource { get; set; }
    }
}
