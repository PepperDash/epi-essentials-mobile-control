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
            : base(key, messagePath)
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
            AppServerController.SendMessageObjectToServer(new
            {
                type = MessagePath + "/passwordPrompt",
                content = new
                {
                    message = args.Message,
                    lastAttemptWasIncorrect = args.LastAttemptWasIncorrect,
                    loginAttemptFailed = args.LoginAttemptFailed,
                    loginAttemptCancelled = args.LoginAttemptCancelled,
                }
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CallHistory_RecentCallsListHasChanged(object sender, EventArgs e)
        {
            var codecCallHistory = sender as CodecCallHistory;
            if (codecCallHistory == null) return;
            var recents = codecCallHistory.RecentCalls;

            if (recents != null)
            {
                PostStatusMessage(new
                {
                    recentCalls = recents
                });
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dirCodec_DirectoryResultReturned(object sender, DirectoryEventArgs e)
        {
            var hasDirectory = Codec as IHasDirectory;
            if (hasDirectory != null)
                SendDirectory(e.Directory, e.DirectoryIsOnRoot);
        }

        /// <summary>
        /// Posts the current directory
        /// </summary>
        private void SendDirectory(CodecDirectory directory, bool isRoot)
        {
            var dirCodec = Codec as IHasDirectory;

            if (dirCodec != null)
            {
                var prefixedDirectoryResults = PrefixDirectoryFolderItems(directory);

                var directoryMessage = new
                {
                    currentDirectory = new
                    {
                        directoryResults = prefixedDirectoryResults,
                        isRootDirectory = isRoot
                    }
                };

                //Spool up a thread in case this is a large quantity of data
                CrestronInvoke.BeginInvoke((o) => PostStatusMessage(directoryMessage));            
            }
        }

        /// <summary>
        /// Iterates a directory object and prefixes any folder items with "[+] "
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        private List<DirectoryItem> PrefixDirectoryFolderItems(CodecDirectory directory)
        {
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

            return tempDirectoryList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void codec_IsReadyChange(object sender, EventArgs e)
        {
            PostStatusMessage(new
            {
                isReady = true
            });
        }

        /// <summary>
        /// Called from base's RegisterWithAppServer method
        /// </summary>
        /// <param name="appServerController"></param>
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
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

            // Directory actions
            var dirCodec = Codec as IHasDirectory;
            if (dirCodec != null)
            {
                appServerController.AddAction(MessagePath + "/getDirectory", new Action(GetDirectoryRoot));
                appServerController.AddAction(MessagePath + "/directoryById", new Action<string>(GetDirectory));
                appServerController.AddAction(MessagePath + "/directorySearch", new Action<string>(DirectorySearch));
                appServerController.AddAction(MessagePath + "/directoryBack", new Action(GetPreviousDirectory));
            }

            // History actions
            var recCodec = Codec as IHasCallHistory;
            if (recCodec != null)
            {
                appServerController.AddAction(MessagePath + "/getCallHistory", new Action(GetCallHistory));
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

        private void GetCallHistory()
        {
            var codec = (Codec as IHasCallHistory);

            if (codec != null)
            {
                var recents = codec.CallHistory.RecentCalls;

                if (recents != null)
                {
                    PostStatusMessage(new
                    {
                        recentCalls = recents
                    });
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
                PostStatusMessage(new
                {
                    initialSyncComplete = false
                });
                return;
            }

            dirCodec.SetCurrentDirectoryToRoot();

            //PostStatusMessage(new
            //{
            //    currentDirectory = dirCodec.DirectoryRoot
            //});
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
            PostStatusMessage(new
            {
                isReady = Codec.IsReady
            });
        }

        /// <summary>
        /// Helper method to build call status for vtc
        /// </summary>
        /// <returns></returns>
        protected VideoCodecBaseStatus GetStatus()
        {
            var status = new VideoCodecBaseStatus();

            var camerasCodec = Codec as IHasCodecCameras;
            if (camerasCodec != null)
            {
                status.Cameras = new VideoCodecBaseStatus.CameraStatus();

                status.Cameras.CameraManualIsSupported = true;
                status.Cameras.CameraAutoIsSupported = Codec.SupportsCameraAutoMode;
                status.Cameras.CameraOffIsSupported = Codec.SupportsCameraOff;
                status.Cameras.CameraMode = GetCameraMode();
                status.Cameras.Cameras = camerasCodec.Cameras;
                status.Cameras.SelectedCamera = GetSelectedCamera(camerasCodec);
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
            status.HasDirectory = Codec is IHasDirectory;
            status.HasDirectorySearch = true;
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

            return status;
        }

        protected virtual void SendFullStatus()
        {
            if (!Codec.IsReady)
            {
                return;
            }

            PostStatusMessage(GetStatus());        
        }

        private void PostReceivingContent(bool receivingContent)
        {
            PostStatusMessage(new {receivingContent});
        }

        private void PostCameraSelfView()
        {
            var selfView = Codec is IHasCodecSelfView
                ? (Codec as IHasCodecSelfView).SelfviewIsOnFeedback.BoolValue
                : false;

            PostStatusMessage(new
            {
                cameraSelfView = selfView
                
            });
        }

        /// <summary>
        /// 
        /// </summary>
        private void PostCameraMode()
        {
            PostStatusMessage(new
            {
                cameras = new
                {
                    cameraMode = GetCameraMode()
                }
            });
        }

        private void PostSelectedCamera()
        {
            var camerasCodec = Codec as IHasCodecCameras;

            PostStatusMessage(new
            {
                cameras = new
                {
                    selectedCamera = GetSelectedCamera(camerasCodec)
                },
                presets = GetCurrentPresets()
            });
        }

        private void PostCameraPresets()
        {
            var status = new VideoCodecBaseStatus();

            status.Presets = GetCurrentPresets();

            PostStatusMessage(status);
        }

        private VideoCodecBaseStatus.CameraStatus.Camera GetSelectedCamera(IHasCodecCameras camerasCodec)
        {
            var camera = new VideoCodecBaseStatus.CameraStatus.Camera();

            camera.Key = camerasCodec.SelectedCameraFeedback.StringValue;
            camera.IsFarEnd = camerasCodec.ControllingFarEndCameraFeedback.BoolValue;
            camera.Capabilities = new VideoCodecBaseStatus.CameraStatus.Camera.CameraCapabilities()
                {
                    CanPan = camerasCodec.SelectedCamera.CanPan,
                    CanTilt = camerasCodec.SelectedCamera.CanTilt,
                    CanZoom = camerasCodec.SelectedCamera.CanZoom,
                    CanFocus = camerasCodec.SelectedCamera.CanFocus,
                };
           
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
    public class VideoCodecBaseStatus
    {
        [JsonProperty("calls", NullValueHandling = NullValueHandling.Ignore)]
        public List<CodecActiveCallItem> Calls {get; set;}

        [JsonProperty("camerMode", NullValueHandling = NullValueHandling.Ignore)]
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

        //[JsonProperty("currentDirectory", NullValueHandling = NullValueHandling.Ignore)]
        //public DirectoryResult CurrentDirectory { get; set; }

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
                [JsonProperty("Key", NullValueHandling = NullValueHandling.Ignore)]
                public string Key { get; set; }

                [JsonProperty("IsFarEnd", NullValueHandling = NullValueHandling.Ignore)]
                public bool? IsFarEnd { get; set; }

                [JsonProperty("Capabilities", NullValueHandling = NullValueHandling.Ignore)]
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
}