using Newtonsoft.Json;
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
    public class ICommunicationMonitorMessenger : MessengerBase
    {
        private readonly ICommunicationMonitor _communicationMonitor;

        public ICommunicationMonitorMessenger(string key, string messagePath, ICommunicationMonitor device) : base(key, messagePath, device as IKeyName)
        {
            _communicationMonitor = device;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => 
            {
                PostStatusMessage(new DeviceStateMessageBase
                {
                    CommMonitor = new CommunicationMonitorState
                    {
                        IsOnline = _communicationMonitor.CommunicationMonitor.IsOnline,
                        Status = _communicationMonitor.CommunicationMonitor.Status
                    }
                }); ;
            });

            _communicationMonitor.CommunicationMonitor.StatusChange += (sender, args) =>
            {
                PostStatusMessage(JToken.FromObject(new
                {
                    CommunicationMonitorState = new
                    {
                        IsOnline = _communicationMonitor.CommunicationMonitor.IsOnline,
                        status = _communicationMonitor.CommunicationMonitor.Status
                    }
                }));
            };
        }
    }



}
