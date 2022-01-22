using System;
using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;

using Newtonsoft.Json;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Provides a messaging bridge
    /// </summary>
    public abstract class MessengerBase : EssentialsDevice
    {
        private Device _device;

        private List<string> _deviceIntefaces;

        /// <summary>
        /// 
        /// </summary>
        public MobileControlSystemController AppServerController { get; private set; }

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
            :this(key, messagePath) 
        {
            _device = device;

            _deviceIntefaces = GetInterfaces(_device);
        }

        /// <summary>
        /// Gets the intefaces implmented on the device
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
        public void RegisterWithAppServer(MobileControlSystemController appServerController)
        {
            if (appServerController == null)
                throw new ArgumentNullException("appServerController");

            AppServerController = appServerController;
            CustomRegisterWithAppServer(AppServerController);
        }

        /// <summary>
        /// Implemented in extending classes. Wire up API calls and feedback here
        /// </summary>
        /// <param name="appServerController"></param>
        protected abstract void CustomRegisterWithAppServer(MobileControlSystemController appServerController);

        /// <summary>
        /// Helper for posting status message
        /// </summary>
        /// <param name="contentObject">The contents of the content object</param>
        [Obsolete("Will be removed in next major release, please use overload as substitute")]
        protected void PostStatusMessage(object contentObject)
        {
            if (AppServerController != null)
            {
                AppServerController.SendMessageObject(new
                {
                    type = MessagePath,
                    content = contentObject
                });
            }
        }

        /// <summary>
        /// Helper for posting status message
        /// </summary>
        /// <param name="type"></param>
        /// <param name="message"></param>
        protected void PostStatusMessage(DeviceStateMessageBase message)
        {
            if (AppServerController != null)
            {
                message.SetIntefaces(_deviceIntefaces);

                message.Key = _device.Key;

                message.Name = _device.Name;

                AppServerController.SendMessageObject(new
                {
                    type = MessagePath,
                    content = message,
                });
            }
        }

        protected void PostStatusMessage(MobileControlResponseMessage message)
        {
            if (AppServerController != null)
            {
                var deviceState = message.Content as DeviceStateMessageBase;
                if (deviceState != null)
                {
                    Debug.Console(2, this, "*********************Setting DeviceStateMessageProperties on MobileControlResponseMessage");
                    deviceState.SetIntefaces(_deviceIntefaces);

                    deviceState.Key = _device.Key;

                    deviceState.Name = _device.Name;
                }
                else
                {
                    Debug.Console(2, this, "*********************Content is not DeviceStateMessageBase");
                }

                AppServerController.SendMessageObject(message);
            }
        }
    }
    

    /// <summary>
    /// Base class for messages that includes the type of message and the implmeneted interfaces
    /// </summary>
    public abstract class DeviceStateMessageBase
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

        /// <summary>
        /// The interfaces implmented by the device sending the messsage
        /// </summary>
        [JsonProperty("interfaces")]
        public List<string> Interfaces { get; private set; }

        public void SetIntefaces(List<string> interfaces)
        {
            Interfaces = interfaces;
        }
    }
}