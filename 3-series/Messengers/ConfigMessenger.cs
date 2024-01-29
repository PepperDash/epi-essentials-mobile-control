using System;
using PepperDash.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Handles interactions with the app server to update the config
    /// </summary>
    public class ConfigMessenger : MessengerBase
    {
        public ConfigMessenger(string key, string messagePath)
            : base(key, messagePath)
        {
            ConfigUpdater.ConfigStatusChanged -= ConfigUpdater_ConfigStatusChanged;
            ConfigUpdater.ConfigStatusChanged += ConfigUpdater_ConfigStatusChanged;
        }

        private void ConfigUpdater_ConfigStatusChanged(object sender, ConfigStatusEventArgs e)
        {
            PostUpdateStatus(e.UpdateStatus.ToString());
        }

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            appServerController.AddAction(MessagePath + "/updateConfig", new Action<string>(GetConfigFile));
        }

        /// <summary>
        /// Generates or passes the URL to make the request to GET the config from a server
        /// </summary>
        /// <param name="url"></param>
        private void GetConfigFile(string url)
        {
            try
            {
                // Attempt to parse the URL
                var parser = new Crestron.SimplSharp.Net.Http.UrlParser(url);

                Debug.Console(0, Debug.ErrorLogLevel.Notice, "Successfully parsed URL from AppServer message: {0}",
                    parser.Url);
            }
            catch (Exception e)
            {
                // If unable to parse the URL, generate it from config data
                Debug.Console(2, "Error parsing URL: {0}", e);

                Debug.Console(0, Debug.ErrorLogLevel.Notice,
                    "Unable to parse URL from AppServer message.  Generating URL from config data");
                url = string.Format("http://{0}/api/system/{1}/config", AppServerController.Config.ServerUrl,
                    AppServerController.SystemUuid);
            }

            ConfigUpdater.GetConfigFromServer(url);
        }

        /// <summary>
        /// Posts a message with the current status of the config update
        /// </summary>
        /// <param name="status"></param>
        private void PostUpdateStatus(string status)
        {
            PostStatusMessage(new
            {
                updateStatus = status
            });
        }
    }
}