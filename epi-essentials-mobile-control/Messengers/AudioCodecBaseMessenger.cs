using System;
using System.Linq;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.AudioCodec;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Provides a messaging bridge for an AudioCodecBase device
    /// </summary>
    public class AudioCodecBaseMessenger : MessengerBase
    {
        /// <summary>
        /// Device being bridged
        /// </summary>
        public AudioCodecBase Codec { get; private set; }

        /// <summary>
        /// Constuctor
        /// </summary>
        /// <param name="key"></param>
        /// <param name="codec"></param>
        /// <param name="messagePath"></param>
        public AudioCodecBaseMessenger(string key, AudioCodecBase codec, string messagePath)
            : base(key, messagePath)
        {
            if (codec == null)
                throw new ArgumentNullException("codec");

            Codec = codec;
            codec.CallStatusChange += codec_CallStatusChange;
        }

        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
            appServerController.AddAction(MessagePath + "/fullStatus", new Action(SendAtcFullMessageObject));
            appServerController.AddAction(MessagePath + "/dial", new Action<string>(s => Codec.Dial(s)));
            appServerController.AddAction(MessagePath + "/endCallById", new Action<string>(s =>
            {
                var call = GetCallWithId(s);
                if (call != null)
                    Codec.EndCall(call);
            }));
            appServerController.AddAction(MessagePath + "/endAllCalls", new Action(Codec.EndAllCalls));
            appServerController.AddAction(MessagePath + "/dtmf", new Action<string>(s => Codec.SendDtmf(s)));
            appServerController.AddAction(MessagePath + "/rejectById", new Action<string>(s =>
            {
                var call = GetCallWithId(s);
                if (call != null)
                    Codec.RejectCall(call);
            }));
            appServerController.AddAction(MessagePath + "/acceptById", new Action<string>(s =>
            {
                var call = GetCallWithId(s);
                if (call != null)
                    Codec.AcceptCall(call);
            }));
        }

        /// <summary>
        /// Helper to grab a call with string ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private CodecActiveCallItem GetCallWithId(string id)
        {
            return Codec.ActiveCalls.FirstOrDefault(c => c.Id == id);
        }

        private void codec_CallStatusChange(object sender, CodecCallStatusItemChangeEventArgs e)
        {
            SendAtcFullMessageObject();
        }

        /// <summary>
        /// Helper method to build call status for vtc
        /// </summary>
        /// <returns></returns>
        private void SendAtcFullMessageObject()
        {
            var info = Codec.CodecInfo;
            PostStatusMessage(new
            {
                isInCall = Codec.IsInCall,
                calls = Codec.ActiveCalls,
                info = new
                {
                    phoneNumber = info.PhoneNumber
                }
            });
        }
    }
}