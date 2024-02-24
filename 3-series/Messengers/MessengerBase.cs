using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using System;
using System.Collections.Generic;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Provides a messaging bridge
    /// </summary>
#if SERIES4
    public abstract class MessengerBase : EssentialsDevice, IMobileControlMessenger
#else
    public abstract class MessengerBase: EssentialsDevice
#endif
    {
        protected Device _device;

        private readonly List<string> _deviceInterfaces;

        /// <summary>
        /// 
        /// </summary>
#if SERIES4
        public IMobileControl3 AppServerController { get; private set; }
#else
        public MobileControlSystemController AppServerController { get; private set; }
#endif

        public string MessagePath { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="messagePath"></param>
        protected MessengerBase(string key, string messagePath)
            : base(key)
        {
            Key = key;

            if (string.IsNullOrEmpty(messagePath))
                throw new ArgumentException("messagePath must not be empty or null");

            MessagePath = messagePath;
        }

        protected MessengerBase(string key, string messagePath, Device device)
            : this(key, messagePath)
        {
            _device = device;

            _deviceInterfaces = GetInterfaces(_device);
        }

        /// <summary>
        /// Gets the interfaces implmented on the device
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private List<string> GetInterfaces(Device device)
        {
            var interfaceTypes = device.GetType().GetInterfaces();

            List<string> interfaces = new List<string>();

            foreach (var i in interfaceTypes)
            {
                interfaces.Add(i.Name);
            }
            return interfaces;
        }

        /// <summary>
        /// Registers this messenger with appserver controller
        /// </summary>
        /// <param name="appServerController"></param>
#if SERIES4
        public void RegisterWithAppServer(IMobileControl3 appServerController)
#else
        public void RegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            AppServerController = appServerController ?? throw new ArgumentNullException("appServerController");
            CustomRegisterWithAppServer(AppServerController);
        }

        /// <summary>
        /// Implemented in extending classes. Wire up API calls and feedback here
        /// </summary>
        /// <param name="appServerController"></param>
#if SERIES4
        protected virtual void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected virtual void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            if (_device is ICommunicationMonitor commMonitor)
            {
                //Debug.Console(2, this, "Subscribing to CommunicationMonitor.StatusChange on: ", _device.Key);
                commMonitor.CommunicationMonitor.StatusChange += CommunicationMonitor_StatusChange;

                GetCommunicationMonitorState();
            }
        }

        private void CommunicationMonitor_StatusChange(object sender, MonitorStatusChangeEventArgs e)
        {
            var message = new DeviceStateMessageBase
            {
                CommMonitor = GetCommunicationMonitorState()
            };


            PostStatusMessage(message);
        }

        protected CommunicationMonitorState GetCommunicationMonitorState()
        {
            if (_device is ICommunicationMonitor commMonitor)
            {
                var state = new CommunicationMonitorState
                {
                    IsOnline = commMonitor.CommunicationMonitor.IsOnline,
                    Status = commMonitor.CommunicationMonitor.Status
                };
                //Debug.Console(2, this, "******************GetCommunitcationMonitorState() IsOnline: {0} Status: {1}", state.IsOnline, state.Status);
                return state;
            }
            else
            {
                //Debug.Console(2, this, "******************Device does not implement ICommunicationMonitor");
                return null;
            }
        }

        /// <summary>
        /// Helper for posting status message
        /// </summary>
        /// <param name="type"></param>
        /// <param name="message"></param>
        protected void PostStatusMessage(DeviceStateMessageBase message, string clientId = null)
        {
            message.SetInterfaces(_deviceInterfaces);

            message.Key = _device.Key;

            message.Name = _device.Name;

            PostStatusMessage(JToken.FromObject(message),MessagePath, clientId);
        }

#if SERIES4 
        protected void PostStatusMessage(string type, DeviceStateMessageBase deviceState, string clientId = null)
        {
            //Debug.Console(2, this, "*********************Setting DeviceStateMessageProperties on MobileControlResponseMessage");
            deviceState.SetInterfaces(_deviceInterfaces);

            deviceState.Key = _device.Key;

            deviceState.Name = _device.Name;            

            PostStatusMessage(JToken.FromObject(deviceState), type, clientId);
        }
#endif
        protected void PostStatusMessage(JToken content, string type = "", string clientId = null)
        {
            AppServerController?.SendMessageObject(new MobileControlMessage { Type = string.IsNullOrEmpty(type) ? type : MessagePath, ClientId = clientId, Content = content });
        }

        protected void PostEventMessage(DeviceEventMessageBase message)
        {
            message.Key = _device.Key;

            message.Name = _device.Name;

            AppServerController?.SendMessageObject(new MobileControlMessage
            {
                Type = MessagePath,
                Content = JToken.FromObject(message),
            });   
        }
    }

    public abstract class DeviceMessageBase
    {
        /// <summary>
        /// The device key
        /// </summary>
        [JsonProperty("key")]
        public string Key { get; set; }

        /// <summary>
        /// The device name
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// The type of the message class
        /// </summary>
        [JsonProperty("messageType")]
        public string MessageType
        {
            get
            {
                return this.GetType().Name;
            }
        }
    }

    /// <summary>
    /// Base class for state messages that includes the type of message and the implmented interfaces
    /// </summary>
    public class DeviceStateMessageBase : DeviceMessageBase
    {
        /// <summary>
        /// For devices that implement ICommunicationMonitor, reports the online status of the device
        /// </summary>
        [JsonProperty("commMonitor", NullValueHandling = NullValueHandling.Ignore)]
        public CommunicationMonitorState CommMonitor { get; set; }

        /// <summary>
        /// The interfaces implmented by the device sending the messsage
        /// </summary>
        [JsonProperty("interfaces")]
        public List<string> Interfaces { get; private set; }

        public void SetInterfaces(List<string> interfaces)
        {
            Interfaces = interfaces;
        }
    }

    /// <summary>
    /// Base class for event messages that include the type of message and an event type
    /// </summary>
    public abstract class DeviceEventMessageBase : DeviceMessageBase
    {
        /// <summary>
        /// The event type
        /// </summary>
        [JsonProperty("eventType")]
        public string EventType { get; set; }
    }

    /// <summary>
    /// Represents the state of the communication monitor
    /// </summary>
    public class CommunicationMonitorState
    {
        /// <summary>
        /// For devices that implement ICommunicationMonitor, reports the online status of the device
        /// </summary>
        [JsonProperty("isOnline", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsOnline { get; set; }

        /// <summary>
        /// For devices that implement ICommunicationMonitor, reports the online status of the device
        /// </summary>
        [JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public MonitorStatus Status { get; set; }

    }
}