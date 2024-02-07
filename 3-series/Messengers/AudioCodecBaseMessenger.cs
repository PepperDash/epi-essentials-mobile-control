﻿using System;
using System.Linq;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.AudioCodec;

using PepperDash.Essentials.Core.DeviceTypeInterfaces;

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

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            base.CustomRegisterWithAppServer(appServerController);

            appServerController.AddAction(MessagePath + "/fullStatus", (id, content) => SendAtcFullMessageObject());
            appServerController.AddAction(MessagePath + "/dial", (id, content) => {
                var msg = content.ToObject<MobileControlSimpleContent<string>>();

                Codec.Dial(msg.Value);
            }); 

            appServerController.AddAction(MessagePath + "/endCallById", (id, content) =>
            {
                var msg = content.ToObject<MobileControlSimpleContent<string>>();

                var call = GetCallWithId(msg.Value);
                if (call != null)
                    Codec.EndCall(call);
            });

            appServerController.AddAction(MessagePath + "/endAllCalls", (id, content) => Codec.EndAllCalls());
            appServerController.AddAction(MessagePath + "/dtmf", (id, content) => {
                var msg = content.ToObject<MobileControlSimpleContent<string>>();

                Codec.SendDtmf(msg.Value);
            });

            appServerController.AddAction(MessagePath + "/rejectById", (id, content) =>
            {
                var msg = content.ToObject<MobileControlSimpleContent<string>>();

                var call = GetCallWithId(msg.Value);

                if (call != null)
                    Codec.RejectCall(call);
            });

            appServerController.AddAction(MessagePath + "/acceptById", (id, content) =>
            {
                var msg = content.ToObject<MobileControlSimpleContent<string>>();
                var call = GetCallWithId(msg.Value);
                if (call != null)
                    Codec.AcceptCall(call);
            });
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