using System;
using System.Collections.Generic;
using System.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.Cameras;
using PepperDash.Essentials.Devices.Common.VideoCodec;
using PepperDash.Essentials.Devices.Common.VideoCodec.Interfaces;
using Crestron.SimplSharp;
using PepperDash.Essentials.Devices.Common.VideoCodec.ZoomRoom;
using Newtonsoft.Json;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Provides a messaging bridge for a VideoCodecBase device
    /// </summary>
    public class VideoCodecBaseMessenger : MessengerBase
    {
        /// <summary>
        /// 
        /// </summary>
        protected VideoCodecBase Codec { get; private set; }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="codec"></param>
        /// <param name="messagePath"></param>
        public VideoCodecBaseMessenger(string key, VideoCodecBase codec, string messagePath)
            : base(key, messagePath, codec)
        {
            if (codec == null)
                throw new ArgumentNullException("codec");

            Codec = codec;
            codec.CallStatusChange += codec_CallStatusChange;
            codec.IsReadyChange += codec_IsReadyChange;

            var dirCodec = codec as IHasDirectory;
            if (dirCodec != null)
            {
                dirCodec.DirectoryResultReturned += dirCodec_DirectoryResultReturned;
            }

            var recCodec = codec as IHasCallHistory;
            if (recCodec != null)
            {
                recCodec.CallHistory.RecentCallsListHasChanged += CallHistory_RecentCallsListHasChanged;
            }

            var pwPromptCodec = codec as IPasswordPrompt;
            if (pwPromptCodec != null)
            {
                pwPromptCodec.PasswordRequired += OnPasswordRequired;
            }
        }

        private void OnPasswordRequired(object sender, PasswordPromptEventArgs args)
        {
            var eventMsg = new PasswordPromptEventMessage()
            {
                Message = args.Message,
                LastAttemptWasIncorrect = args.LastAttemptWasIncorrect,
                LoginAttemptFailed = args.LoginAttemptFailed,
                LoginAttemptCancelled = args.LoginAttemptCancelled,
            };

            eventMsg.EventType = "passwordPrompt";

            PostEventMessage(eventMsg);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CallHistory_RecentCallsListHasChanged(object sender, EventArgs e)
        {
            var state = new VideoCodecBaseStateMessage();

            var codecCallHistory = sender as CodecCallHistory;
            if (codecCallHistory == null) return;
            var recents = codecCallHistory.RecentCalls;

            if (recents != null)
            {
                state.RecentCalls = recents;

                PostStatusMessage(state);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void dirCodec_DirectoryResultReturned(object sender, DirectoryEventArgs e)
        {
            var hasDirectory = Codec as IHasDirectory;
            if (hasDirectory != null)
                SendDirectory(e.Directory);
        }

        /// <summary>
        /// Posts the current directory
        /// </summary>
        protected void SendDirectory(CodecDirectory directory)
        {
            var state = new VideoCodecBaseStateMessage();

            var dirCodec = Codec as IHasDirectory;

            if (dirCodec != null)
            {
                Debug.Console(2, this, "Sending Directory.  Directory Item Count: {0}", directory.CurrentDirectoryResults.Count);

                //state.CurrentDirectory = PrefixDirectoryFolderItems(directory);
                state.CurrentDirectory = directory;
                CrestronInvoke.BeginInvoke((o) => PostStatusMessage(state));

/*                var directoryMessage = new
                {
                    currentDirectory = new
                    {
                        directoryResults = prefixedDirectoryResults,
                        isRootDirectory = isRoot
                    }
                };

                //Spool up a thread in case this is a large quantity of data
                CrestronInvoke.BeginInvoke((o) => PostStatusMessage(directoryMessage));           */ 
            }
        }

        /// <summary>
        /// Iterates a directory object and prefixes any folder items with "[+] "
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        [Obsolete("Deprected in favour of processing in the Angular App")]
        private CodecDirectory PrefixDirectoryFolderItems(CodecDirectory directory)
        {
            var tempCodecDirectory = new CodecDirectory();
            var tempDirectoryList = new List<DirectoryItem>();

            if (directory.CurrentDirectoryResults.Count > 0)
            {
                foreach (var item in directory.CurrentDirectoryResults)
                {
                    if (item is DirectoryFolder)
                    {
                        var newFolder = (DirectoryFolder) item.Clone();

                        var prefixName = "[+] " + newFolder.Name;

                        newFolder.Name = prefixName;

                        tempDirectoryList.Add(newFolder);
                    }
                    else
                    {
                        tempDirectoryList.Add(item);
                    }
                }
            }
            //else
            //{
            //    DirectoryItem noResults = new DirectoryItem() { Name = "No Results Found" };

            //    tempDirectoryList.Add(noResults);
            //}

            tempCodecDirectory.AddContactsToDirectory(tempDirectoryList.OfType<DirectoryContact>().Cast<DirectoryItem>().ToList());
            tempCodecDirectory.AddFoldersToDirectory(tempDirectoryList.OfType<DirectoryFolder>().Cast<DirectoryItem>().ToList());

            return tempCodecDirectory;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void codec_IsReadyChange(object sender, EventArgs e)
        {
            var state = new VideoCodecBaseStateMessage();

            state.IsReady = true;

            PostStatusMessage(state);

            SendFullStatus();
        }

        /// <summary>
        /// Called from base's RegisterWithAppServer method
        /// </summary>
        /// <param name="appServerController"></param>
#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            try
            {
                base.CustomRegisterWithAppServer(appServerController);

                appServerController.AddAction(String.Format("{0}/isReady", MessagePath), new Action(SendIsReady));
                appServerController.AddAction(String.Format("{0}/fullStatus", MessagePath), new Action(SendFullStatus));
                appServerController.AddAction(String.Format("{0}/dial", MessagePath), new Action<string>(s => Codec.Dial(s)));
                appServerController.AddAction(String.Format("{0}/dialMeeting", MessagePath), new Action<Meeting>(m => Codec.Dial(m)));
                appServerController.AddAction(String.Format("{0}/endCallById", MessagePath), new Action<string>(s =>
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

                Codec.SharingContentIsOnFeedback.OutputChange += SharingContentIsOnFeedback_OutputChange;
                Codec.SharingSourceFeedback.OutputChange += SharingSourceFeedback_OutputChange;

                // Directory actions
                var dirCodec = Codec as IHasDirectory;
                if (dirCodec != null)
                {
                    appServerController.AddAction(MessagePath + "/getDirectory", new Action(GetDirectoryRoot));
                    appServerController.AddAction(MessagePath + "/directoryById", new Action<string>(GetDirectory));
                    appServerController.AddAction(MessagePath + "/directorySearch", new Action<string>(DirectorySearch));
                    appServerController.AddAction(MessagePath + "/directoryBack", new Action(GetPreviousDirectory));

                    dirCodec.PhonebookSyncState.InitialSyncCompleted += PhonebookSyncState_InitialSyncCompleted;
                }

                // History actions
                var recCodec = Codec as IHasCallHistory;
                if (recCodec != null)
                {
                    appServerController.AddAction(MessagePath + "/getCallHistory", new Action(PostCallHistory));
                }
                var cameraCodec = Codec as IHasCodecCameras;
                if (cameraCodec != null)
                {
                    Debug.Console(2, this, "Adding IHasCodecCameras Actions");

                    cameraCodec.CameraSelected += cameraCodec_CameraSelected;

                    appServerController.AddAction(MessagePath + "/cameraSelect",
                        new Action<string>(cameraCodec.SelectCamera));

                    MapCameraActions();

                    var presetsCodec = Codec as IHasCodecRoomPresets;
                    if (presetsCodec != null)
                    {
                        Debug.Console(2, this, "Adding IHasCodecRoomPresets Actions");

                        presetsCodec.CodecRoomPresetsListHasChanged += presetsCodec_CameraPresetsListHasChanged;

                        appServerController.AddAction(MessagePath + "/cameraPreset",
                            new Action<int>(presetsCodec.CodecRoomPresetSelect));
                        appServerController.AddAction(MessagePath + "/cameraPresetStore",
                            new Action<CodecRoomPreset>(p => presetsCodec.CodecRoomPresetStore(p.ID, p.Description)));
                    }

                    var speakerTrackCodec = Codec as IHasCameraAutoMode;
                    if (speakerTrackCodec != null)
                    {
                        Debug.Console(2, this, "Adding IHasCameraAutoMode Actions");

                        speakerTrackCodec.CameraAutoModeIsOnFeedback.OutputChange += CameraAutoModeIsOnFeedback_OutputChange;

                        appServerController.AddAction(MessagePath + "/cameraModeAuto",
                            new Action(speakerTrackCodec.CameraAutoModeOn));
                        appServerController.AddAction(MessagePath + "/cameraModeManual",
                            new Action(speakerTrackCodec.CameraAutoModeOff));
                    }

                    var cameraOffCodec = Codec as IHasCameraOff;
                    if (cameraOffCodec != null)
                    {
                        Debug.Console(2, this, "Adding IHasCameraOff Actions");

                        cameraOffCodec.CameraIsOffFeedback.OutputChange += (CameraIsOffFeedback_OutputChange);

                        appServerController.AddAction(MessagePath + "/cameraModeOff",
                            new Action(cameraOffCodec.CameraOff));
                    }
                }


                var selfViewCodec = Codec as IHasCodecSelfView;

                if (selfViewCodec != null)
                {
                    Debug.Console(2, this, "Adding IHasCodecSelfView Actions");

                    appServerController.AddAction(MessagePath + "/cameraSelfView",
                        new Action(selfViewCodec.SelfViewModeToggle));

                    selfViewCodec.SelfviewIsOnFeedback.OutputChange += new EventHandler<FeedbackEventArgs>(SelfviewIsOnFeedback_OutputChange);
                }

                var layoutsCodec = Codec as IHasCodecLayouts;

                if (layoutsCodec != null)
                {
                    Debug.Console(2, this, "Adding IHasCodecLayouts Actions");

                    appServerController.AddAction(MessagePath + "/cameraRemoteView",
                        new Action(layoutsCodec.LocalLayoutToggle));

                    appServerController.AddAction(MessagePath + "/cameraLayout",
                        new Action(layoutsCodec.LocalLayoutToggle));

                }

                var pwCodec = Codec as IPasswordPrompt;
                if (pwCodec != null)
                {
                    Debug.Console(2, this, "Adding IPasswordPrompt Actions");

                    appServerController.AddAction(MessagePath + "/password", new Action<string>((s) => pwCodec.SubmitPassword(s)));
                }

                var farEndContentStatus = Codec as IHasFarEndContentStatus;

                if (farEndContentStatus != null)
                {
                    farEndContentStatus.ReceivingContent.OutputChange +=
                        (sender, args) => PostReceivingContent(args.BoolValue);
                }

                Debug.Console(2, this, "Adding Privacy & Standby Actions");

                appServerController.AddAction(MessagePath + "/privacyModeOn", new Action(Codec.PrivacyModeOn));
                appServerController.AddAction(MessagePath + "/privacyModeOff", new Action(Codec.PrivacyModeOff));
                appServerController.AddAction(MessagePath + "/privacyModeToggle", new Action(Codec.PrivacyModeToggle));
                appServerController.AddAction(MessagePath + "/sharingStart", new Action(Codec.StartSharing));
                appServerController.AddAction(MessagePath + "/sharingStop", new Action(Codec.StopSharing));
                appServerController.AddAction(MessagePath + "/standbyOn", new Action(Codec.StandbyActivate));
                appServerController.AddAction(MessagePath + "/standbyOff", new Action(Codec.StandbyDeactivate));
            }
            catch (Exception e)
            {
                Debug.Console(2, this, "Error: {0}", e);
            }
        }

        private void SharingSourceFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var state = new VideoCodecBaseStateMessage();
            state.SharingSource = e.StringValue;

            PostStatusMessage(state);
        }

        private void SharingContentIsOnFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var state = new VideoCodecBaseStateMessage();
            state.SharingContentIsOn = e.BoolValue;

            PostStatusMessage(state);
        }

        private void PhonebookSyncState_InitialSyncCompleted(object sender, EventArgs e)
        {
            var state = new VideoCodecBaseStateMessage();
            state.InitialPhonebookSyncComplete = true;

            PostStatusMessage(state);
        }

        void CameraIsOffFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            PostCameraMode();
        }

        void SelfviewIsOnFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            PostCameraSelfView();
        }

        private void presetsCodec_CameraPresetsListHasChanged(object sender, EventArgs e)
        {
            PostCameraPresets();
        }

        private void CameraAutoModeIsOnFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            PostCameraMode();
        }


        private void cameraCodec_CameraSelected(object sender, CameraSelectedEventArgs e)
        {
            MapCameraActions();
            PostSelectedCamera();
        }

        /// <summary>
        /// Maps the camera control actions to the current selected camera on the codec
        /// </summary>
        private void MapCameraActions()
        {
            var cameraCodec = Codec as IHasCameras;

            if (cameraCodec != null && cameraCodec.SelectedCamera != null)
            {
                AppServerController.RemoveAction(MessagePath + "/cameraUp");
                AppServerController.RemoveAction(MessagePath + "/cameraDown");
                AppServerController.RemoveAction(MessagePath + "/cameraLeft");
                AppServerController.RemoveAction(MessagePath + "/cameraRight");
                AppServerController.RemoveAction(MessagePath + "/cameraZoomIn");
                AppServerController.RemoveAction(MessagePath + "/cameraZoomOut");
                AppServerController.RemoveAction(MessagePath + "/cameraHome");

                var camera = cameraCodec.SelectedCamera as IHasCameraPtzControl;
                if (camera != null)
                {
                    AppServerController.AddAction(MessagePath + "/cameraUp", new PressAndHoldAction(b =>
                    {
                        if (b) camera.TiltUp();
                        else camera.TiltStop();
                    }));
                    AppServerController.AddAction(MessagePath + "/cameraDown", new PressAndHoldAction(b =>
                    {
                        if (b) camera.TiltDown();
                        else camera.TiltStop();
                    }));
                    AppServerController.AddAction(MessagePath + "/cameraLeft", new PressAndHoldAction(b =>
                    {
                        if (b) camera.PanLeft();
                        else camera.PanStop();
                    }));
                    AppServerController.AddAction(MessagePath + "/cameraRight", new PressAndHoldAction(b =>
                    {
                        if (b) camera.PanRight();
                        else camera.PanStop();
                    }));
                    AppServerController.AddAction(MessagePath + "/cameraZoomIn", new PressAndHoldAction(b =>
                    {
                        if (b) camera.ZoomIn();
                        else camera.ZoomStop();
                    }));
                    AppServerController.AddAction(MessagePath + "/cameraZoomOut", new PressAndHoldAction(b =>
                    {
                        if (b) camera.ZoomOut();
                        else camera.ZoomStop();
                    }));
                    AppServerController.AddAction(MessagePath + "/cameraHome", new Action(camera.PositionHome));

                    var focusCamera = cameraCodec as IHasCameraFocusControl;

                    AppServerController.RemoveAction(MessagePath + "/cameraAutoFocus");
                    AppServerController.RemoveAction(MessagePath + "/cameraFocusNear");
                    AppServerController.RemoveAction(MessagePath + "/cameraFocusFar");

                    if (focusCamera != null)
                    {
                        AppServerController.AddAction(MessagePath + "/cameraAutoFocus",
                            new Action(focusCamera.TriggerAutoFocus));
                        AppServerController.AddAction(MessagePath + "/cameraFocusNear", new PressAndHoldAction(b =>
                        {
                            if (b) focusCamera.FocusNear();
                            else focusCamera.FocusStop();
                        }));
                        AppServerController.AddAction(MessagePath + "/cameraFocusFar", new PressAndHoldAction(b =>
                        {
                            if (b) focusCamera.FocusFar();
                            else focusCamera.FocusStop();
                        }));
                    }
                }
            }
        }

        private string GetCameraMode()
        {
            string m = "";

            var speakerTrackCodec = Codec as IHasCameraAutoMode;
            if (speakerTrackCodec != null)
            {
                m = speakerTrackCodec.CameraAutoModeIsOnFeedback.BoolValue
                    ? eCameraControlMode.Auto.ToString().ToLower()
                    : eCameraControlMode.Manual.ToString().ToLower();
            }

            var cameraOffCodec = Codec as IHasCameraOff;
            if (cameraOffCodec != null)
            {
                if (cameraOffCodec.CameraIsOffFeedback.BoolValue)
                    m = eCameraControlMode.Off.ToString().ToLower();
            }

            return m;
        }

        private void PostCallHistory()
        {
            var codec = (Codec as IHasCallHistory);

            if (codec != null)
            {
                var status = new VideoCodecBaseStateMessage();

                var recents = codec.CallHistory.RecentCalls;

                if (recents != null)
                {
                    status.RecentCalls = codec.CallHistory.RecentCalls;

                    PostStatusMessage(status);
                }
            }
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        private void DirectorySearch(string s)
        {
            var dirCodec = Codec as IHasDirectory;
            if (dirCodec != null)
            {
                dirCodec.SearchDirectory(s);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        private void GetDirectory(string id)
        {
            var dirCodec = Codec as IHasDirectory;
            if (dirCodec == null)
            {
                return;
            }
            dirCodec.GetDirectoryFolderContents(id);
        }

        /// <summary>
        /// 
        /// </summary>
        private void GetDirectoryRoot()
        {
            var dirCodec = Codec as IHasDirectory;
            if (dirCodec == null)
            {
                // do something else?
                return;
            }
            if (!dirCodec.PhonebookSyncState.InitialSyncComplete)
            {
                var state = new VideoCodecBaseStateMessage();
                state.InitialPhonebookSyncComplete = false;

                PostStatusMessage(state);
                return;
            }

            dirCodec.SetCurrentDirectoryToRoot();
        }

        /// <summary>
        /// Requests the parent folder contents
        /// </summary>
        private void GetPreviousDirectory()
        {
            var dirCodec = Codec as IHasDirectory;
            if (dirCodec == null)
            {
                return;
            }

            dirCodec.GetDirectoryParentFolderContents();
        }

        /// <summary>
        /// Handler for codec changes
        /// </summary>
        private void codec_CallStatusChange(object sender, CodecCallStatusItemChangeEventArgs e)
        {
            SendFullStatus();
        }

        /// <summary>
        /// 
        /// </summary>
        private void SendIsReady()
        {
            var status = new VideoCodecBaseStateMessage();

            status.IsReady = Codec.IsReady;
            status.IsZoomRoom = Codec is ZoomRoom;

            PostStatusMessage(status);
        }

        /// <summary>
        /// Helper method to build call status for vtc
        /// </summary>
        /// <returns></returns>
        protected VideoCodecBaseStateMessage GetStatus()
        {
            var status = new VideoCodecBaseStateMessage();

            status.CommMonitor = GetCommunicationMonitorState();

            var camerasCodec = Codec as IHasCodecCameras;
            if (camerasCodec != null)
            {
                status.Cameras = new VideoCodecBaseStateMessage.CameraStatus();

                status.Cameras.CameraManualIsSupported = true;
                status.Cameras.CameraAutoIsSupported = Codec.SupportsCameraAutoMode;
                status.Cameras.CameraOffIsSupported = Codec.SupportsCameraOff;
                status.Cameras.CameraMode = GetCameraMode();
                status.Cameras.Cameras = camerasCodec.Cameras;
                status.Cameras.SelectedCamera = GetSelectedCamera(camerasCodec);
            }

            var directoryCodec = Codec as IHasDirectory;
            if (directoryCodec != null)
            {
                status.HasDirectory = true;
                status.HasDirectorySearch = true;
                status.CurrentDirectory = directoryCodec.CurrentDirectoryResult;
            }

            status.CameraSelfViewIsOn = Codec is IHasCodecSelfView && (Codec as IHasCodecSelfView).SelfviewIsOnFeedback.BoolValue;
            status.IsInCall = Codec.IsInCall;
            status.PrivacyModeIsOn = Codec.PrivacyModeIsOnFeedback.BoolValue;
            status.SharingContentIsOn = Codec.SharingContentIsOnFeedback.BoolValue;
            status.SharingSource = Codec.SharingSourceFeedback.StringValue;
            status.StandbyIsOn = Codec.StandbyIsOnFeedback.BoolValue;
            status.Calls = Codec.ActiveCalls;
            status.Info = Codec.CodecInfo;
            status.ShowSelfViewByDefault = Codec.ShowSelfViewByDefault;
            status.SupportsAdHocMeeting = Codec is IHasStartMeeting;
            status.HasRecents = Codec is IHasCallHistory;
            status.HasCameras = Codec is IHasCameras;
            status.Presets = GetCurrentPresets();
            status.IsZoomRoom = Codec is ZoomRoom;
            status.ReceivingContent = Codec is IHasFarEndContentStatus && (Codec as IHasFarEndContentStatus).ReceivingContent.BoolValue;

            var meetingInfoCodec = Codec as IHasMeetingInfo;
            if (meetingInfoCodec != null)
            {
                status.MeetingInfo = meetingInfoCodec.MeetingInfo;
            }

            //Debug.Console(2, this, "VideoCodecBaseStatus:\n{0}", JsonConvert.SerializeObject(status)); 

            return status;
        }

        protected virtual void SendFullStatus()
        {
            if (!Codec.IsReady)
            {
                return;
            }

            CrestronInvoke.BeginInvoke((o) => PostStatusMessage(GetStatus()));
        }

        private void PostReceivingContent(bool receivingContent)
        {
            var state = new VideoCodecBaseStateMessage();
            state.ReceivingContent = receivingContent;
            PostStatusMessage(state);
        }

        private void PostCameraSelfView()
        {
            var status = new VideoCodecBaseStateMessage();

            status.CameraSelfViewIsOn = Codec is IHasCodecSelfView
                ? (Codec as IHasCodecSelfView).SelfviewIsOnFeedback.BoolValue
                : false;

            PostStatusMessage(status);
        }

        /// <summary>
        /// 
        /// </summary>
        private void PostCameraMode()
        {
            var status = new VideoCodecBaseStateMessage();

            status.CameraMode = GetCameraMode();

            PostStatusMessage(status);
        }

        private void PostSelectedCamera()
        {
            var camerasCodec = Codec as IHasCodecCameras;

            var status = new VideoCodecBaseStateMessage();

            status.Cameras = new VideoCodecBaseStateMessage.CameraStatus() { SelectedCamera = GetSelectedCamera(camerasCodec) };
            status.Presets = GetCurrentPresets();
            PostStatusMessage(status);
        }

        private void PostCameraPresets()
        {
            var status = new VideoCodecBaseStateMessage();

            status.Presets = GetCurrentPresets();

            PostStatusMessage(status);
        }

        private VideoCodecBaseStateMessage.CameraStatus.Camera GetSelectedCamera(IHasCodecCameras camerasCodec)
        {
            var camera = new VideoCodecBaseStateMessage.CameraStatus.Camera();

            if (camerasCodec.SelectedCameraFeedback != null)
                camera.Key = camerasCodec.SelectedCameraFeedback.StringValue;
            if (camerasCodec.SelectedCamera != null)
            {
                camera.Name = camerasCodec.SelectedCamera.Name;

                camera.Capabilities = new VideoCodecBaseStateMessage.CameraStatus.Camera.CameraCapabilities()
                {
                    CanPan = camerasCodec.SelectedCamera.CanPan,
                    CanTilt = camerasCodec.SelectedCamera.CanTilt,
                    CanZoom = camerasCodec.SelectedCamera.CanZoom,
                    CanFocus = camerasCodec.SelectedCamera.CanFocus,
                };
            }

            if (camerasCodec.ControllingFarEndCameraFeedback != null)
                camera.IsFarEnd = camerasCodec.ControllingFarEndCameraFeedback.BoolValue;

           
           return camera; 
        }

        private List<CodecRoomPreset> GetCurrentPresets()
        {
            var presetsCodec = Codec as IHasCodecRoomPresets;

            List<CodecRoomPreset> currentPresets = null;

            if (presetsCodec != null && Codec is IHasFarEndCameraControl &&
                (Codec as IHasFarEndCameraControl).ControllingFarEndCameraFeedback.BoolValue)
                currentPresets = presetsCodec.FarEndRoomPresets;
            else if (presetsCodec != null) currentPresets = presetsCodec.NearEndPresets;

            return currentPresets;
        }
    }

    /// <summary>
    /// A class that represents the state data to be sent to the user app
    /// </summary>
    public class VideoCodecBaseStateMessage : DeviceStateMessageBase
    {

        [JsonProperty("calls", NullValueHandling = NullValueHandling.Ignore)]
        public List<CodecActiveCallItem> Calls {get; set;}

        [JsonProperty("cameraMode", NullValueHandling = NullValueHandling.Ignore)]
        public string CameraMode { get; set; }

        [JsonProperty("cameraSelfView", NullValueHandling = NullValueHandling.Ignore)]
        public bool? CameraSelfViewIsOn { get; set; }

        [JsonProperty("cameras", NullValueHandling = NullValueHandling.Ignore)]
        public CameraStatus Cameras { get; set; }

        [JsonProperty("cameraSupportsAutoMode", NullValueHandling = NullValueHandling.Ignore)]
        public bool? CameraSupportsAutoMode { get; set; }

        [JsonProperty("cameraSupportsOffMode", NullValueHandling = NullValueHandling.Ignore)]
        public bool? CameraSupportsOffMode { get; set; }

        [JsonProperty("currentDialString", NullValueHandling = NullValueHandling.Ignore)]
        public string CurrentDialString { get; set; }

        [JsonProperty("currentDirectory", NullValueHandling = NullValueHandling.Ignore)]
        public CodecDirectory CurrentDirectory { get; set; }

        [JsonProperty("directorySelectedFolderName", NullValueHandling = NullValueHandling.Ignore)]
        public string DirectorySelectedFolderName { get; set; }

        [JsonProperty("hasCameras", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasCameras { get; set; }

        [JsonProperty("hasDirectory", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasDirectory { get; set; }

        [JsonProperty("hasDirectorySearch", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasDirectorySearch { get; set; }

        [JsonProperty("hasPresets", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasPresets { get; set; }

        [JsonProperty("hasRecents", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasRecents { get; set; }

        [JsonProperty("initialPhonebookSyncComplete", NullValueHandling = NullValueHandling.Ignore)]
        public bool? InitialPhonebookSyncComplete { get; set; }

        [JsonProperty("info", NullValueHandling = NullValueHandling.Ignore)]
        public VideoCodecInfo Info { get; set; }

        [JsonProperty("isInCall", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsInCall { get; set; }

        [JsonProperty("isReady", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsReady { get; set; }

        [JsonProperty("isZoomRoom", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsZoomRoom { get; set; }

        [JsonProperty("meetingInfo", NullValueHandling = NullValueHandling.Ignore)]
        public MeetingInfo MeetingInfo { get; set; }

        [JsonProperty("presets", NullValueHandling = NullValueHandling.Ignore)]
        public List<CodecRoomPreset> Presets { get; set; }

        [JsonProperty("privacyModeIsOn", NullValueHandling = NullValueHandling.Ignore)]
        public bool? PrivacyModeIsOn { get; set; }

        [JsonProperty("receivingContent", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ReceivingContent { get; set; }

        [JsonProperty("recentCalls", NullValueHandling = NullValueHandling.Ignore)]
        public List<CodecCallHistory.CallHistoryEntry> RecentCalls { get; set; }

        [JsonProperty("sharingContentIsOn", NullValueHandling = NullValueHandling.Ignore)]
        public bool? SharingContentIsOn { get; set; }

        [JsonProperty("sharingSource", NullValueHandling = NullValueHandling.Ignore)]
        public string SharingSource { get; set; }

        [JsonProperty("showCamerasWhenNotInCall", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ShowCamerasWhenNotInCall { get; set; }

        [JsonProperty("showSelfViewByDefault", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ShowSelfViewByDefault { get; set; }

        [JsonProperty("standbyIsOn", NullValueHandling = NullValueHandling.Ignore)]
        public bool? StandbyIsOn { get; set; }

        [JsonProperty("supportsAdHocMeeting", NullValueHandling = NullValueHandling.Ignore)]
        public bool? SupportsAdHocMeeting { get; set; }

        public class CameraStatus 
        {
            [JsonProperty("cameraManualSupported", NullValueHandling = NullValueHandling.Ignore)]
            public bool? CameraManualIsSupported { get; set; }

            [JsonProperty("cameraAutoSupported", NullValueHandling = NullValueHandling.Ignore)]
            public bool? CameraAutoIsSupported { get; set; }

            [JsonProperty("cameraOffSupported", NullValueHandling = NullValueHandling.Ignore)]
            public bool? CameraOffIsSupported { get; set; }

            [JsonProperty("cameraMode", NullValueHandling = NullValueHandling.Ignore)]
            public string CameraMode { get; set; }

            [JsonProperty("cameraList", NullValueHandling = NullValueHandling.Ignore)]
            public List<CameraBase> Cameras { get; set; }

            [JsonProperty("selectedCamera", NullValueHandling = NullValueHandling.Ignore)]
            public Camera SelectedCamera { get; set; }

            public class Camera
            {
                [JsonProperty("key", NullValueHandling = NullValueHandling.Ignore)]
                public string Key { get; set; }

                [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
                public string Name { get; set; }

                [JsonProperty("isFarEnd", NullValueHandling = NullValueHandling.Ignore)]
                public bool? IsFarEnd { get; set; }

                [JsonProperty("capabilities", NullValueHandling = NullValueHandling.Ignore)]
                public CameraCapabilities Capabilities { get; set; }

                public class CameraCapabilities
                {
                    [JsonProperty("canPan", NullValueHandling = NullValueHandling.Ignore)]
                    public bool? CanPan { get; set; }

                    [JsonProperty("canTilt", NullValueHandling = NullValueHandling.Ignore)]
                    public bool? CanTilt { get; set; }

                    [JsonProperty("canZoom", NullValueHandling = NullValueHandling.Ignore)]
                    public bool? CanZoom { get; set; }

                    [JsonProperty("canFocus", NullValueHandling = NullValueHandling.Ignore)]
                    public bool? CanFocus { get; set; }

                }
            }

        }

    }

    public class VideoCodecBaseEventMessage: DeviceEventMessageBase
    {

    }

    public class PasswordPromptEventMessage : VideoCodecBaseEventMessage
    {
        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }
        [JsonProperty("lastAttemptWasIncorrect", NullValueHandling = NullValueHandling.Ignore)]
        public bool LastAttemptWasIncorrect { get; set; }

        [JsonProperty("loginAttemptFailed", NullValueHandling = NullValueHandling.Ignore)]
        public bool LoginAttemptFailed { get; set; }

        [JsonProperty("loginAttemptCancelled", NullValueHandling = NullValueHandling.Ignore)]
        public bool LoginAttemptCancelled { get; set; }
    }
}