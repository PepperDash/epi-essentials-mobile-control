using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Converters;
using PepperDash.Core.Logging;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class ISelectableItemsMessenger<TKey> : MessengerBase
    {
        private static readonly JsonSerializer serializer = new JsonSerializer { Converters = { new StringEnumConverter() } };
        private ISelectableItems<TKey> itemDevice;

        private readonly string _propName;
        public ISelectableItemsMessenger(string key, string messagePath, ISelectableItems<TKey> device, string propName) : base(key, messagePath, device as IKeyName)
        {
            itemDevice = device;
            _propName = propName;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction($"/{_propName}/fullStatus", (id, context) =>
            {
                SendFullStatus();
            });

            itemDevice.ItemsUpdated += (sender, args) =>
            {
                SendFullStatus();
            };

            itemDevice.CurrentItemChanged += (sender, args) =>
            {
                SendFullStatus();
            };

            foreach (var input in itemDevice.Items)
            {
                var key = input.Key;
                var localItem = input.Value;                

                AddAction($"/{_propName}/{key}", (id, content) =>
                {
                    localItem.Select();
                });

                localItem.ItemUpdated += (sender, args) =>
                {
                    SendFullStatus();
                };
            }
        }

        private void SendFullStatus()
        {
            try
            { 
                this.LogInformation("Sending full status");

                var stateObject = new ISelectableItemsStateMessage<TKey>
                {
                    Items = itemDevice.Items,
                    CurrentItem = itemDevice.CurrentItem
                };

                PostStatusMessage(stateObject);
            }
            catch (Exception e)
            {
                this.LogError("Error sending full status: {0}", e.Message);
            }
        }
    }

    public class ISelectableItemsStateMessage<TKey> : DeviceStateMessageBase
    {
        [JsonProperty("items")]
        public Dictionary<TKey, ISelectableItem> Items { get; set; }

        [JsonProperty("currentItem")]
        public TKey CurrentItem { get; set; }
    }

}
