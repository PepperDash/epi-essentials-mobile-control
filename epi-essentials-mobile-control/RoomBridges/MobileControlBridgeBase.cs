using System;
using PepperDash.Essentials.Core;

using PepperDash.Core;

using PepperDash.Essentials.Core.DeviceTypeInterfaces;


namespace PepperDash.Essentials
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class MobileControlBridgeBase : EssentialsDevice, IMobileControlRoomBridge
    {
        public event EventHandler<EventArgs> UserCodeChanged;

        public event EventHandler<EventArgs> UserPromptedForCode;

        public event EventHandler<EventArgs> ClientJoined;

        public MobileControlSystemController Parent { get; private set; }

        public string UserCode { get; private set; }

        public string QrCodeUrl { get; private set; }

        public string QrCodeChecksum { get; private set; }

        public string McServerUrl { get; private set; }

        public abstract string RoomName { get; }

        protected MobileControlBridgeBase(string key, string name)
            : base(key, name)
        {
        }

        /// <summary>
        /// Set the parent.  Does nothing else.  Override to add functionality such
        /// as adding actions to parent
        /// </summary>
        /// <param name="parent"></param>
        public virtual void AddParent(MobileControlSystemController parent)
        {
            Parent = parent;

            McServerUrl = Parent.Config.ClientAppUrl;


        }

        /// <summary>
        /// Sets the UserCode on the bridge object. Called from controller. A changed code will
        /// fire method UserCodeChange.  Override that to handle changes
        /// </summary>
        /// <param name="code"></param>
        public void SetUserCode(string code)
        {
            var changed = UserCode != code;
            UserCode = code;
            if (changed)
            {
                UserCodeChange();
            }
        }


        /// <summary>
        /// Sets the UserCode on the bridge object. Called from controller. A changed code will
        /// fire method UserCodeChange.  Override that to handle changes
        /// </summary>
        /// <param name="code"></param>
        /// <param name="qrChecksum">Checksum of the QR code. Used for Cisco codec branding command</param>
        public void SetUserCode(string code, string qrChecksum)
        {
            QrCodeChecksum = qrChecksum;

            SetUserCode(code);
        }

        /// <summary>
        /// Empty method in base class.  Override this to add functionality
        /// when code changes
        /// </summary>
        protected virtual void UserCodeChange()
        {
            Debug.Console(1, this, "Server user code changed: {0}", UserCode);

            var qrUrl = string.Format("{0}/api/system/{1}/qr?x={2}", Parent.Host, Parent.SystemUuid, new Random().Next());
            QrCodeUrl = qrUrl;

            Debug.Console(1, this, "Server user code changed: {0} - {1}", UserCode, qrUrl);

            OnUserCodeChanged();
        }

        void OnUserCodeChanged()
        {
            var handler = UserCodeChanged;
            if (handler != null)
            {
                handler(this, new EventArgs());
            }
        }

        void OnUserPromptedForCode()
        {
            var handler = UserPromptedForCode;
            if (handler != null)
            {
                handler(this, new EventArgs());
            }
        }

        void OnClientJoined()
        {
            var handler = ClientJoined;
            if (handler != null)
            {
                handler(this, new EventArgs());
            }
        }
    }
}