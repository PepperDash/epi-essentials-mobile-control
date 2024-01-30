using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.VideoCodec.ZoomRoom;
using PepperDash.Essentials.Devices.Common.VideoCodec;
using PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces;
using PepperDash.Core;

using Newtonsoft.Json;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;


namespace PepperDash.Essentials.AppServer.Messengers
{
    public class ZoomRoomMessenger : VideoCodecBaseMessenger
    {

        private readonly ZoomRoom _codec;

        public ZoomRoomMessenger(string key, ZoomRoom codec, string messagePath)
            : base(key, codec, messagePath)
        {
            _codec = codec;
        }

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            try
            {
                base.CustomRegisterWithAppServer(appServerController);

                appServerController.AddAction(String.Format("{0}/startMeeting", MessagePath),
                    new Action<ushort>((duration) =>
                    {
                        _codec.StartMeeting(duration);
                    }));

                appServerController.AddAction(String.Format("{0}/invite", MessagePath),
                    new Action<InvitableDirectoryContact>((c) =>
                    {
                        Codec.Dial((c));
                    }));

                appServerController.AddAction(String.Format("{0}/inviteContactsToNewMeeting", MessagePath),
                    new Action<Invitation>((c) =>
                    {
                        _codec.InviteContactsToNewMeeting(c.Invitees, c.Duration);
                    }));

                appServerController.AddAction(String.Format("{0}/inviteContactsToExistingMeeting", MessagePath),
                    new Action<Invitation>((c) =>
                    {
                        _codec.InviteContactsToExistingMeeting(c.Invitees);
                    }));

                appServerController.AddAction(MessagePath + "/muteVideo", new Action(() => _codec.CameraMuteOn()));

                appServerController.AddAction(MessagePath + "/toggleVideoMute", new Action(() => _codec.CameraMuteToggle()));

                _codec.CameraIsMutedFeedback.OutputChange += new EventHandler<PepperDash.Essentials.Core.FeedbackEventArgs>(CameraIsMutedFeedback_OutputChange);

                _codec.VideoUnmuteRequested += codec_VideoUnmuteRequested;

                var presentOnlyMeetingCodec = _codec as IHasPresentationOnlyMeeting;
                if (presentOnlyMeetingCodec != null)
                {
                    Debug.Console(2, this, "Adding IHasPresentationOnlyMeeting");

                    appServerController.AddAction(MessagePath + "/dialPresent", new Action(() => presentOnlyMeetingCodec.StartSharingOnlyMeeting(eSharingMeetingMode.Laptop)));
                    appServerController.AddAction(MessagePath + "/dialConvert", new Action(presentOnlyMeetingCodec.StartNormalMeetingFromSharingOnlyMeeting));
                }

                var startMeetingCodec = _codec as IHasStartMeeting;
                if (startMeetingCodec != null)
                {
                    Debug.Console(2, this, "Adding IStartMeeting Actions");

                    appServerController.AddAction(String.Format("{0}/startMeeting", MessagePath), new Action(
                        () => startMeetingCodec.StartMeeting(startMeetingCodec.DefaultMeetingDurationMin)));
                    appServerController.AddAction(String.Format("{0}/leaveMeeting", MessagePath), new Action(
                        startMeetingCodec.LeaveMeeting));
                }

                appServerController.AddAction(String.Format("{0}/endMeeting", MessagePath), new Action(
                    _codec.EndAllCalls));

                var participantsCodec = _codec as IHasParticipants;
                if (participantsCodec != null)
                {
                    Debug.Console(2, this, "Adding IHasParticipant Actions");

                    participantsCodec.Participants.ParticipantsListHasChanged += Participants_ParticipantsListHasChanged;

                    appServerController.AddAction(String.Format("{0}/removeParticipant", MessagePath), new Action<int>((i) =>
                        participantsCodec.RemoveParticipant(i)));

                    appServerController.AddAction(String.Format("{0}/setParticipantAsHost", MessagePath), new Action<int>((i) =>
                        participantsCodec.SetParticipantAsHost(i)));


                    var audioMuteCodec = _codec as IHasParticipantAudioMute;
                    if (audioMuteCodec != null)
                    {
                        appServerController.AddAction(String.Format("{0}/muteAllParticipants", MessagePath), new Action(
                            audioMuteCodec.MuteAudioForAllParticipants));

                        appServerController.AddAction(String.Format("{0}/toggleParticipantAudioMute", MessagePath), new Action<int>((i) =>
                            audioMuteCodec.ToggleAudioForParticipant(i)));

                        appServerController.AddAction(String.Format("{0}/toggleParticipantVideoMute", MessagePath), new Action<int>((i) =>
                            audioMuteCodec.ToggleVideoForParticipant(i)));
                    }
                }

                var lockCodec = _codec as IHasMeetingLock;
                if (lockCodec != null)
                {
                    Debug.Console(2, this, "Adding IHasMeetingLock Actions");

                    lockCodec.MeetingIsLockedFeedback.OutputChange += MeetingIsLockedFeedback_OutputChange;

                    appServerController.AddAction(String.Format("{0}/toggleMeetingLock", MessagePath), new Action(
                        lockCodec.ToggleMeetingLock));
                }

                var recordCodec = _codec as IHasMeetingRecordingWithPrompt;
                if (recordCodec != null)
                {
                    Debug.Console(2, this, "Adding IHasMeetingRecording Actions");

                    recordCodec.MeetingIsRecordingFeedback.OutputChange += MeetingIsRecordingFeedback_OutputChange;

                    recordCodec.RecordConsentPromptIsVisible.OutputChange += RecordConsentPromptIsVisible_OutputChange;

                    appServerController.AddAction(String.Format("{0}/recordPromptAcknowledge", MessagePath), new Action<bool>((b) => 
                    {
                        Debug.Console(2, this, "recordPromptAcknowledge: {0}", b);
                        recordCodec.RecordingPromptAcknowledgement(b);
                    }
                        ));

                    appServerController.AddAction(String.Format("{0}/toggleRecording", MessagePath), new Action(
                        recordCodec.ToggleRecording));
                }

                var layoutsCodec = _codec as IHasZoomRoomLayouts;
                if (layoutsCodec != null)
                {
                    Debug.Console(2, this, "Adding IHasZoomRoomLayouts Actions");

                    layoutsCodec.LayoutInfoChanged += layoutsCodec_LayoutInfoChanged;

                    appServerController.AddAction(String.Format("{0}/selectLayout", MessagePath), new Action<string>(s =>
                        layoutsCodec.SetLayout((zConfiguration.eLayoutStyle)Enum.Parse(typeof(zConfiguration.eLayoutStyle), s, true))));

                    appServerController.AddAction(String.Format("{0}/particpantsNextPage", MessagePath), new Action(
                        layoutsCodec.LayoutTurnNextPage));

                    appServerController.AddAction(String.Format("{0}/participantsPreviousPage", MessagePath), new Action(
                        layoutsCodec.LayoutTurnPreviousPage));
                }

                var meetingInfoCodec = Codec as IHasMeetingInfo;
                if (meetingInfoCodec != null)
                {
                    Debug.Console(2, this, "Adding IHasMeetingInfo Actions");

                    meetingInfoCodec.MeetingInfoChanged += new EventHandler<MeetingInfoEventArgs>(meetingInfoCodec_MeetingInfoChanged);
                }

                var scheduleCodec = Codec as IHasScheduleAwareness;
                if (scheduleCodec != null)
                {
                    Debug.Console(2, this, "Adding IHasScheduleAwareness Subscriptions");
                    scheduleCodec.CodecSchedule.MeetingsListHasChanged += CodecSchedule_MeetingsListHasChanged;
                }

                var sharingInfo = Codec as IZoomWirelessShareInstructions;
                if (sharingInfo != null)
                {
                    Debug.Console(2, this, "Adding IZoomWirelessSharingInstructions Subscriptions");
                    sharingInfo.ShareInfoChanged += SharingInfo_ShareInfoChanged;
                }

            }
            catch (Exception e)
            {
                Debug.Console(2, this, "Error: {0}", e);
            }
        }

        private void SharingInfo_ShareInfoChanged(object sender, ShareInfoEventArgs e)
        {
            var status = new ZoomRoomStateMessage();

            status.ShareInfo = e.SharingStatus;

            PostStatusMessage(status);
        }

        private void CodecSchedule_MeetingsListHasChanged(object sender, EventArgs e)
        {
            var status = new ZoomRoomStateMessage();

            status.Meetings = (Codec as IHasScheduleAwareness).CodecSchedule.Meetings;

            PostStatusMessage(status);
        }

            private void RecordConsentPromptIsVisible_OutputChange(object sender, Core.FeedbackEventArgs e)
        {
            var status = new ZoomRoomStateMessage();

            status.RecordConsentPromptIsVisible = e.BoolValue;

            PostStatusMessage(status);
        }

        void CameraIsMutedFeedback_OutputChange(object sender, PepperDash.Essentials.Core.FeedbackEventArgs e)
        {
            var status = new ZoomRoomStateMessage();

            status.CameraIsMuted = e.BoolValue;

            PostStatusMessage(status);
        }

        void meetingInfoCodec_MeetingInfoChanged(object sender, MeetingInfoEventArgs e)
        {
            PostMeetingInfo(e.Info);
        }

        void codec_VideoUnmuteRequested(object sender, EventArgs e)
        {
            var eventMsg = new ZoomRoomEventMessage();

            eventMsg.EventType = "videoUnmuteRequested";

            PostEventMessage(eventMsg);
        }

        void Participants_ParticipantsListHasChanged(object sender, EventArgs e)
        {
            var status = new ZoomRoomStateMessage();

            status.Participants = _codec.Participants.CurrentParticipants;

            PostStatusMessage(status);
        }

        /// <summary>
        /// For ZoomRoom we simply want to refresh the root directory data whenever this event fires
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void dirCodec_DirectoryResultReturned(object sender, DirectoryEventArgs e)
        {
            var dirCodec = Codec as IHasDirectory;

            if (dirCodec != null)
            {
                SendDirectory(dirCodec.DirectoryRoot);
            }
        }

        void MeetingIsRecordingFeedback_OutputChange(object sender, PepperDash.Essentials.Core.FeedbackEventArgs e)
        {
            PostMeetingInfo(_codec.MeetingInfo);
        }

        void MeetingIsLockedFeedback_OutputChange(object sender, PepperDash.Essentials.Core.FeedbackEventArgs e)
        {
            PostMeetingInfo(_codec.MeetingInfo);
        }

        void layoutsCodec_LayoutInfoChanged(object sender, LayoutInfoChangedEventArgs e)
        {
            var status = new ZoomRoomStateMessage();

            status.Layouts = e;

            PostStatusMessage(status);
        }


        private void PostMeetingInfo(MeetingInfo info)
        {
            var status = new ZoomRoomStateMessage();

            status.MeetingInfo = _codec.MeetingInfo;

            PostStatusMessage(status);
        }


        protected override void SendFullStatus()
        {
            if (!Codec.IsReady)
            {
                return;
            }

            var baseStatus = GetStatus();

            var zoomStatus = new ZoomRoomStateMessage();

            PropertyCopier<VideoCodecBaseStateMessage, ZoomRoomStateMessage>.Copy(baseStatus, zoomStatus);

            // We always want to override the base CurrentDirectory value with DirectoryRoot because the ZoomRoom
            // has a flat directory that we manually add the Rooms and Contacts folders to to match the Zoom UI
            zoomStatus.CurrentDirectory = _codec.DirectoryRoot;

            zoomStatus.Layouts = new LayoutInfoChangedEventArgs()
                {
                    AvailableLayouts = _codec.AvailableLayouts,
                    //CurrentSelectedLayout = (zConfiguration.eLayoutStyle)Enum.Parse(typeof(zConfiguration.eLayoutStyle), _codec.LocalLayoutFeedback.StringValue, true),
                    LayoutViewIsOnFirstPage = _codec.LayoutViewIsOnFirstPageFeedback.BoolValue,
                    LayoutViewIsOnLastPage = _codec.LayoutViewIsOnLastPageFeedback.BoolValue,
                    CanSwapContentWithThumbnail = _codec.CanSwapContentWithThumbnailFeedback.BoolValue,
                    ContentSwappedWithThumbnail = _codec.ContentSwappedWithThumbnailFeedback.BoolValue,
                };
            zoomStatus.Meetings = _codec.CodecSchedule.Meetings;
            zoomStatus.Participants = _codec.Participants.CurrentParticipants;
            zoomStatus.CameraIsMuted = _codec.CameraIsMutedFeedback.BoolValue;
            zoomStatus.RecordConsentPromptIsVisible = _codec.RecordConsentPromptIsVisible.BoolValue;
            zoomStatus.ShareInfo = _codec.SharingState;

            PostStatusMessage(zoomStatus);
        }
    }

    public class Invitation
    {
        [JsonProperty("duration")]
        public uint Duration { get; set; }
        [JsonProperty("invitees")]
        public List<InvitableDirectoryContact> Invitees { get; set; }
    }

    public class ZoomRoomStateMessage : VideoCodecBaseStateMessage
    {
        [JsonProperty("layouts", NullValueHandling = NullValueHandling.Ignore)]
        public LayoutInfoChangedEventArgs Layouts { get; set; }

        [JsonProperty("meetings", NullValueHandling = NullValueHandling.Ignore)]
        public List<Meeting> Meetings { get; set; }

        [JsonProperty("participants", NullValueHandling = NullValueHandling.Ignore)]
        public List<Participant> Participants { get; set; }

        [JsonProperty("cameraIsMuted", NullValueHandling = NullValueHandling.Ignore)]
        public bool? CameraIsMuted { get; set; }

        [JsonProperty("recordConsentPromptIsVisible", NullValueHandling = NullValueHandling.Ignore)]
        public bool? RecordConsentPromptIsVisible { get; set; }

        [JsonProperty("shareInfo", NullValueHandling = NullValueHandling.Ignore)]
        public zStatus.Sharing ShareInfo { get; set; }
    }

    public class ZoomRoomEventMessage: DeviceEventMessageBase
    {

    }

}