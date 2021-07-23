﻿using System;
using PepperDash.Core;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Provides a messaging bridge
    /// </summary>
    public abstract class MessengerBase : IKeyed
    {
        public string Key { get; private set; }

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
        {
            Key = key;

            if (string.IsNullOrEmpty(messagePath))
                throw new ArgumentException("messagePath must not be empty or null");

            MessagePath = messagePath;
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
        protected void PostStatusMessage(object contentObject)
        {
            if (AppServerController != null)
            {
                AppServerController.SendMessageObjectToServer(new
                {
                    type = MessagePath,
                    content = contentObject
                });
            }
        }
    }
}