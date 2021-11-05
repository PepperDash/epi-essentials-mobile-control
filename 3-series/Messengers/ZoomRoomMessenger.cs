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


namespace PepperDash.Essentials.AppServer.Messengers
{
    public class ZoomRoomMessenger : VideoCodecBaseMessenger
    {

        private ZoomRoom _codec;

        public ZoomRoomMessenger(string key, ZoomRoom codec, string messagePath)
            : base(key, codec, messagePath)
        {
            _codec = codec;
        }

        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
            base.CustomRegisterWithAppServer(appServerController);

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

            var recordCodec = _codec as IHasMeetingRecording;
            if (recordCodec != null)
            {
                Debug.Console(2, this, "Adding IHasMeetingRecording Actions");

                recordCodec.MeetingIsRecordingFeedback.OutputChange += MeetingIsRecordingFeedback_OutputChange;

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

            
        }

        void meetingInfoCodec_MeetingInfoChanged(object sender, MeetingInfoEventArgs e)
        {
            PostMeetingInfo(e.Info);
        }

        void Participants_ParticipantsListHasChanged(object sender, EventArgs e)
        {
            PostStatusMessage(new
            {
                participants = _codec.Participants.CurrentParticipants,
            }
        );

        }

        void MeetingIsRecordingFeedback_OutputChange(object sender, PepperDash.Essentials.Core.FeedbackEventArgs e)
        {
            PostStatusMessage(new
            {
                meetingInfo = new
                {
                    isRecording = e.BoolValue,
                },
            }
            );
        }

        void MeetingIsLockedFeedback_OutputChange(object sender, PepperDash.Essentials.Core.FeedbackEventArgs e)
        {
            PostStatusMessage(new
                {
                    meetingInfo = new 
                    {
                        isLocked = e.BoolValue,
                    },
                }
            );
        }

        void layoutsCodec_LayoutInfoChanged(object sender, LayoutInfoChangedEventArgs e)
        {
            PostStatusMessage(new
            {
                layouts = new
                {
                    availableLayouts = e.AvailableLayouts,
                    lastSelectedLayout = e.CurrentSelectedLayout,
                    isOnFirstPage = e.LayoutViewIsOnFirstPage,
                    isOnLastPage = e.LayoutViewIsOnLastPage,
                    canSwapContentWithThumbnail = e.CanSwapContentWithThumbnail,
                    contentIsSwappedWithThumbnail = e.ContentSwappedWithThumbnail,
                },
            });
        }


        private void PostMeetingInfo(MeetingInfo info)
        {
            PostStatusMessage(new
            {
                meetingInfo = info
            });
        }


        protected override void SendFullStatus()
        {
            var baseStatus = GetStatus();

            var zoomStatus = new ZoomRoomStatus();

            zoomStatus = baseStatus as ZoomRoomStatus;

        }
    }

    public class Invitation
    {
        [JsonProperty("duration")]
        public uint Duration { get; set; }
        [JsonProperty("invitees")]
        public List<InvitableDirectoryContact> Invitees { get; set; }


    }

    public class ZoomRoomStatus : VideoCodecBaseStatus
    {
        [JsonProperty("meetings", NullValueHandling = NullValueHandling.Ignore)]
        public List<Meeting> Meetings { get; set; }


        
    }

}