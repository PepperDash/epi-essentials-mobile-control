using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Independentsoft.Json.Parser;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class ITechPasswordMessenger : MessengerBase
    {
        private readonly ITechPassword _room;

        public ITechPasswordMessenger(string key, string messagePath, ITechPassword room)
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

            AddAction("/validateTechPassword", (id, content) =>
            {
                var password = content.Value<string>("techPassword");

                _room.ValidateTechPassword(password);
            });

            AddAction("/setTechPassword", (id, content) =>
            {
                var response = content.ToObject<SetTechPasswordContent>();

                _room.SetTechPassword(response.OldPassword, response.NewPassword);
            });

            _room.TechPasswordChanged += (sender, args) =>
            {
                var status = new ITechPasswordEventMessage
                {
                    TechPasswordChanged = true
                };

                PostEventMessage(status);
            };

            _room.TechPasswordValidateResult += (sender, args) =>
            {
                var status = new ITechPasswordEventMessage
                {
                    TechPasswordIsValid = args.IsValid
                };

                PostEventMessage(status);
            };
        }

        private void SendFullStatus()
        {
            var status = new ITechPasswordStateMessage
            {
                TechPasswordLength = _room.TechPasswordLength
            };

            PostStatusMessage(status);
        }

    }

    public class ITechPasswordStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("techPasswordLength", NullValueHandling = NullValueHandling.Ignore)]
        public int? TechPasswordLength { get; set; }
    }

    public class ITechPasswordEventMessage : DeviceEventMessageBase
    {
        [JsonProperty("techPasswordIsValid", NullValueHandling = NullValueHandling.Ignore)]
        public bool? TechPasswordIsValid { get; set; }

        [JsonProperty("techPasswordChanged", NullValueHandling = NullValueHandling.Ignore)]
        public bool? TechPasswordChanged { get; set; }
    }
    
    class SetTechPasswordContent
    {
        [JsonProperty("oldPassword")]
        public string OldPassword { get; set; }

        [JsonProperty("newPassword")]
        public string NewPassword { get; set; }
    }

}
