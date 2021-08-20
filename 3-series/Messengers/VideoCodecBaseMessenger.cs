using System;
using System.Collections.Generic;
using System.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.Cameras;
using PepperDash.Essentials.Devices.Common.VideoCodec;
using Crestron.SimplSharp;

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
        public VideoCodecBase Codec { get; private set; }

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
            appServerController.AddAction(String.Format("{0}/isReady",MessagePath), new Action(SendIsReady));
            appServerController.AddAction(String.Format("{0}/fullStatus",MessagePath), new Action(SendVtcFullMessageObject));
            appServerController.AddAction(String.Format("{0}/dial", MessagePath), new Action<string>(s => Codec.Dial(s)));
            appServerController.AddAction(String.Format("{0}/invite", MessagePath), 
                new Action<InvitableDirectoryContact>((c) => 
                {
                    Codec.Dial((c));
                }));
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

        public void GetFullStatusMessage()
        {
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
            SendVtcFullMessageObject();
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
        private void SendVtcFullMessageObject()
        {
            if (!Codec.IsReady)
            {
                return;
            }

            object cameraInfo = null;

            var camerasCodec = Codec as IHasCodecCameras;
            if (camerasCodec != null)
            {
                cameraInfo = new
                {
                    cameraManualSupported = true,
                    // For now, we assume manual mode is supported and selectively hide controls based on camera selection
                    cameraAutoSupported = Codec.SupportsCameraAutoMode,
                    cameraOffSupported = Codec.SupportsCameraOff,
                    cameraMode = GetCameraMode(),
                    cameraList = camerasCodec.Cameras,
                    selectedCamera = GetSelectedCamera(camerasCodec)
                };
            }

            var selfView = Codec is IHasCodecSelfView && (Codec as IHasCodecSelfView).SelfviewIsOnFeedback.BoolValue;

            var info = Codec.CodecInfo;
            PostStatusMessage(new
            {
                cameraSelfView = selfView,
                isInCall = Codec.IsInCall,
                privacyModeIsOn = Codec.PrivacyModeIsOnFeedback.BoolValue,
                sharingContentIsOn = Codec.SharingContentIsOnFeedback.BoolValue,
                sharingSource = Codec.SharingSourceFeedback.StringValue,
                standbyIsOn = Codec.StandbyIsOnFeedback.StringValue,
                calls = Codec.ActiveCalls,
                info = new
                {
                    autoAnswerEnabled = info.AutoAnswerEnabled,
                    e164Alias = info.E164Alias,
                    h323Id = info.H323Id,
                    ipAddress = info.IpAddress,
                    sipPhoneNumber = info.SipPhoneNumber,
                    sipURI = info.SipUri
                },
                showSelfViewByDefault = Codec.ShowSelfViewByDefault,
                hasDirectory = Codec is IHasDirectory,
                hasDirectorySearch = true,
                hasRecents = Codec is IHasCallHistory,
                hasCameras = Codec is IHasCameras,
                cameras = cameraInfo,
                presets = GetCurrentPresets()
            });
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
            PostStatusMessage(new
            {
                presets = GetCurrentPresets()
            });
        }

        private object GetSelectedCamera(IHasCodecCameras camerasCodec)
        {
            return new
            {
                Key = camerasCodec.SelectedCameraFeedback.StringValue,
                IsFarEnd = camerasCodec.ControllingFarEndCameraFeedback.BoolValue,
                Capabilites = new
                {
                    camerasCodec.SelectedCamera.CanPan, camerasCodec.SelectedCamera.CanTilt, camerasCodec.SelectedCamera.CanZoom, camerasCodec.SelectedCamera.CanFocus
                }
            };
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
}