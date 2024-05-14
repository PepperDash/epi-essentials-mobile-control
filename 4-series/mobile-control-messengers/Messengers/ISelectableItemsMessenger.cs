using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class ISelectableItemsMessenger<TKey> : MessengerBase
    {
        private ISelectableItems<TKey> itemDevice;

        private readonly string _propName;
        public ISelectableItemsMessenger(string key, string messagePath, ISelectableItems<TKey> device, string propName) : base(key, messagePath, device as Device)
        {
            itemDevice = device;
            _propName = propName;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, context) =>
            {
                SendFullStatus();
            });

            itemDevice.ItemsUpdated += (sender, args) =>
            {
                SendFullStatus();
            };

            foreach (var input in itemDevice.Items)
            {
                var key = input.Key;
                var localItem = input.Value;

                AddAction($"/{localItem.Key}", (id, content) =>
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
            var stateObject = new JObject();
            stateObject[_propName] = JToken.FromObject(itemDevice);
            PostStatusMessage(stateObject);
        }
    }

}
