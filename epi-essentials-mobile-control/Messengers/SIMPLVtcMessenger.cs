using System;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.Cameras;

namespace PepperDash.Essentials.AppServer.Messengers
{
// ReSharper disable once InconsistentNaming
    public class SIMPLVtcMessenger : MessengerBase
    {
        private readonly BasicTriList _eisc;

        public SIMPLVtcJoinMap JoinMap { get; private set; }

        ///********* Bools *********/
        ///// <summary>
        ///// 724
        ///// </summary>
        //const uint BDialHangup = 724;
        ///// <summary>
        ///// 750
        ///// </summary>
        //const uint BCallIncoming = 750;
        ///// <summary>
        ///// 751
        ///// </summary>
        //const uint BIncomingAnswer = 751;
        ///// <summary>
        ///// 752
        ///// </summary>
        //const uint BIncomingReject = 752;
        ///// <summary>
        ///// 741
        ///// </summary>
        //const uint BSpeedDial1 = 741;
        ///// <summary>
        ///// 742
        ///// </summary>
        //const uint BSpeedDial2 = 742;
        ///// <summary>
        ///// 743
        ///// </summary>
        //const uint BSpeedDial3 = 743;
        ///// <summary>
        ///// 744
        ///// </summary>
        //const uint BSpeedDial4 = 744;
        ///// <summary>
        ///// 800
        ///// </summary>
        //const uint BDirectorySearchBusy = 800;
        ///// <summary>
        ///// 801 
        ///// </summary>
        //const uint BDirectoryLineSelected = 801;
        ///// <summary>
        ///// 801 when selected entry is a contact
        ///// </summary>
        //const uint BDirectoryEntryIsContact = 801;
        ///// <summary>
        ///// 802 To show/hide back button
        ///// </summary>
        //const uint BDirectoryIsRoot = 802;
        ///// <summary>
        ///// 803 Pulse from system to inform us when directory is ready
        ///// </summary>
        //const uint BDirectoryHasChanged = 803;
        ///// <summary>
        ///// 804
        ///// </summary>
        //const uint BDirectoryRoot = 804;
        ///// <summary>
        ///// 805
        ///// </summary>
        //const uint BDirectoryFolderBack = 805;
        ///// <summary>
        ///// 806
        ///// </summary>
        //const uint BDirectoryDialSelectedLine = 806;
        ///// <summary>
        ///// 811
        ///// </summary>
        //const uint BCameraControlUp = 811;
        ///// <summary>
        ///// 812
        ///// </summary>
        //const uint BCameraControlDown = 812;
        ///// <summary>
        ///// 813
        ///// </summary>
        //const uint BCameraControlLeft = 813;
        ///// <summary>
        ///// 814
        ///// </summary>
        //const uint BCameraControlRight = 814;
        ///// <summary>
        ///// 815
        ///// </summary>
        //const uint BCameraControlZoomIn = 815;
        ///// <summary>
        ///// 816
        ///// </summary>
        //const uint BCameraControlZoomOut = 816;
        ///// <summary>
        ///// 821 - 826
        ///// </summary>
        //const uint BCameraPresetStart = 821;

        ///// <summary>
        ///// 831
        ///// </summary>
        //const uint BCameraModeAuto = 831;
        ///// <summary>
        ///// 832
        ///// </summary>
        //const uint BCameraModeManual = 832;
        ///// <summary>
        ///// 833
        ///// </summary>
        //const uint BCameraModeOff = 833;

        ///// <summary>
        ///// 841
        ///// </summary>
        //const uint BCameraSelfView = 841;

        ///// <summary>
        ///// 842
        ///// </summary>
        //const uint BCameraLayout = 842;
        ///// <summary>
        ///// 843
        ///// </summary>
        //const uint BCameraSupportsAutoMode = 843;
        ///// <summary>
        ///// 844
        ///// </summary>
        //const uint BCameraSupportsOffMode = 844;
        ///********* Ushorts *********/
        ///// <summary>
        ///// 760
        ///// </summary>
        //const uint UCameraNumberSelect = 760;
        ///// <summary>
        ///// 801
        ///// </summary>
        //const uint UDirectorySelectRow = 801;
        ///// <summary>
        ///// 801
        ///// </summary>
        //const uint UDirectoryRowCount = 801;
        ///********* Strings *********/
        ///// <summary>
        ///// 701
        ///// </summary>
        //const uint SCurrentDialString = 701;
        ///// <summary>
        ///// 702
        ///// </summary>
        //const uint SCurrentCallName = 702;
        ///// <summary>
        ///// 703
        ///// </summary>
        //const uint SCurrentCallNumber = 703;
        ///// <summary>
        ///// 731
        ///// </summary>
        //const uint SHookState = 731;
        ///// <summary>
        ///// 722
        ///// </summary>
        //const uint SCallDirection = 722;
        ///// <summary>
        ///// 751
        ///// </summary>
        //const uint SIncomingCallName = 751;
        ///// <summary>
        ///// 752
        ///// </summary>
        //const uint SIncomingCallNumber = 752;

        ///// <summary>
        ///// 800
        ///// </summary>
        //const uint SDirectorySearchString = 800;
        ///// <summary>
        ///// 801-1055
        ///// </summary>
        //const uint SDirectoryEntriesStart = 801;
        ///// <summary>
        ///// 1056
        ///// </summary>
        //const uint SDirectoryEntrySelectedName = 1056;
        ///// <summary>
        ///// 1057
        ///// </summary>
        //const uint SDirectoryEntrySelectedNumber = 1057;
        ///// <summary>
        ///// 1058
        ///// </summary>
        //const uint SDirectorySelectedFolderName = 1058;

        ///// <summary>
        ///// 701-712 0-9*#
        ///// </summary>
        //Dictionary<string, uint> DTMFMap = new Dictionary<string, uint>
        //{
        //    { "1", 701 },
        //    { "2", 702 },
        //    { "3", 703 },
        //    { "4", 704 },
        //    { "5", 705 },
        //    { "6", 706 },
        //    { "7", 707 },
        //    { "8", 708 },
        //    { "9", 709 },
        //    { "0", 710 },
        //    { "*", 711 },
        //    { "#", 712 },
        //};
        private readonly CodecActiveCallItem _currentCallItem;

        private CodecActiveCallItem _incomingCallItem;

        private ushort _previousDirectoryLength = 701;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="eisc"></param>
        /// <param name="messagePath"></param>
        public SIMPLVtcMessenger(string key, BasicTriList eisc, string messagePath)
            : base(key, messagePath)
        {
            _eisc = eisc;

            JoinMap = new SIMPLVtcJoinMap(701);

            _currentCallItem = new CodecActiveCallItem {Type = eCodecCallType.Video, Id = "-video-"};
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appServerController"></param>
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
            var asc = appServerController;
            _eisc.SetStringSigAction(JoinMap.HookState.JoinNumber, s =>
            {
                _currentCallItem.Status = (eCodecCallStatus) Enum.Parse(typeof (eCodecCallStatus), s, true);
                PostFullStatus(); // SendCallsList();
            });

            _eisc.SetStringSigAction(JoinMap.CurrentCallNumber.JoinNumber, s =>
            {
                _currentCallItem.Number = s;
                PostCallsList();
            });

            _eisc.SetStringSigAction(JoinMap.CurrentCallName.JoinNumber, s =>
            {
                _currentCallItem.Name = s;
                PostCallsList();
            });

            _eisc.SetStringSigAction(JoinMap.CallDirection.JoinNumber, s =>
            {
                _currentCallItem.Direction = (eCodecCallDirection) Enum.Parse(typeof (eCodecCallDirection), s, true);
                PostCallsList();
            });

            _eisc.SetBoolSigAction(JoinMap.IncomingCall.JoinNumber, b =>
            {
                if (b)
                {
                    var ica = new CodecActiveCallItem
                    {
                        Direction = eCodecCallDirection.Incoming,
                        Id = "-video-incoming",
                        Name = _eisc.GetString(JoinMap.IncomingCallName.JoinNumber),
                        Number = _eisc.GetString(JoinMap.IncomingCallNumber.JoinNumber),
                        Status = eCodecCallStatus.Ringing,
                        Type = eCodecCallType.Video
                    };
                    _incomingCallItem = ica;
                }
                else
                {
                    _incomingCallItem = null;
                }
                PostCallsList();
            });

            _eisc.SetBoolSigAction(JoinMap.CameraSupportsAutoMode.JoinNumber, b => PostStatusMessage(new
            {
                cameraSupportsAutoMode = b
            }));
            _eisc.SetBoolSigAction(JoinMap.CameraSupportsOffMode.JoinNumber, b => PostStatusMessage(new
            {
                cameraSupportsOffMode = b
            }));

            // Directory insanity
            _eisc.SetUShortSigAction(JoinMap.DirectoryRowCount.JoinNumber, u =>
            {
                // The length of the list comes in before the list does.
                // Splice the sig change operation onto the last string sig that will be changing
                // when the directory entries make it through.
                if (_previousDirectoryLength > 0)
                {
                    _eisc.ClearStringSigAction(JoinMap.DirectoryEntriesStart.JoinNumber + _previousDirectoryLength - 1);
                }
                _eisc.SetStringSigAction(JoinMap.DirectoryEntriesStart.JoinNumber + u - 1, s => PostDirectory());
                _previousDirectoryLength = u;
            });

            _eisc.SetStringSigAction(JoinMap.DirectoryEntrySelectedName.JoinNumber, s => PostStatusMessage(new
            {
                directoryContactSelected = new
                {
                    name = _eisc.GetString(JoinMap.DirectoryEntrySelectedName.JoinNumber),
                }
            }));

            _eisc.SetStringSigAction(JoinMap.DirectoryEntrySelectedNumber.JoinNumber, s => PostStatusMessage(new
            {
                directoryContactSelected = new
                {
                    number = _eisc.GetString(JoinMap.DirectoryEntrySelectedNumber.JoinNumber),
                }
            }));

            _eisc.SetStringSigAction(JoinMap.DirectorySelectedFolderName.JoinNumber, s => PostStatusMessage(new
            {
                directorySelectedFolderName = _eisc.GetString(JoinMap.DirectorySelectedFolderName.JoinNumber)
            }));

            _eisc.SetSigTrueAction(JoinMap.CameraModeAuto.JoinNumber, PostCameraMode);
            _eisc.SetSigTrueAction(JoinMap.CameraModeManual.JoinNumber, PostCameraMode);
            _eisc.SetSigTrueAction(JoinMap.CameraModeOff.JoinNumber, PostCameraMode);

            _eisc.SetBoolSigAction(JoinMap.CameraSelfView.JoinNumber, b => PostStatusMessage(new
            {
                cameraSelfView = b
            }));

            _eisc.SetUShortSigAction(JoinMap.CameraNumberSelect.JoinNumber, u => PostSelectedCamera());


            // Add press and holds using helper action
            Action<string, uint> addPhAction = (s, u) =>
                AppServerController.AddAction(MessagePath + s, new PressAndHoldAction(b => _eisc.SetBool(u, b)));
            addPhAction("/cameraUp", JoinMap.CameraTiltUp.JoinNumber);
            addPhAction("/cameraDown", JoinMap.CameraTiltDown.JoinNumber);
            addPhAction("/cameraLeft", JoinMap.CameraPanLeft.JoinNumber);
            addPhAction("/cameraRight", JoinMap.CameraPanRight.JoinNumber);
            addPhAction("/cameraZoomIn", JoinMap.CameraZoomIn.JoinNumber);
            addPhAction("/cameraZoomOut", JoinMap.CameraZoomOut.JoinNumber);

            // Add straight pulse calls using helper action
            Action<string, uint> addAction = (s, u) =>
                AppServerController.AddAction(MessagePath + s, new Action(() => _eisc.PulseBool(u, 100)));
            addAction("/endCallById", JoinMap.EndCall.JoinNumber);
            addAction("/endAllCalls", JoinMap.EndCall.JoinNumber);
            addAction("/acceptById", JoinMap.IncomingAnswer.JoinNumber);
            addAction("/rejectById", JoinMap.IncomingReject.JoinNumber);

            var speeddialStart = JoinMap.SpeedDialStart.JoinNumber;
            var speeddialEnd = JoinMap.SpeedDialStart.JoinNumber + JoinMap.SpeedDialStart.JoinSpan;

            var speedDialIndex = 1;
            for (uint i = speeddialStart; i < speeddialEnd; i++)
            {
                addAction(string.Format("/speedDial{0}", speedDialIndex), i);
                speedDialIndex++;
            }

            addAction("/cameraModeAuto", JoinMap.CameraModeAuto.JoinNumber);
            addAction("/cameraModeManual", JoinMap.CameraModeManual.JoinNumber);
            addAction("/cameraModeOff", JoinMap.CameraModeOff.JoinNumber);
            addAction("/cameraSelfView", JoinMap.CameraSelfView.JoinNumber);
            addAction("/cameraLayout", JoinMap.CameraLayout.JoinNumber);

            asc.AddAction("/cameraSelect", new Action<string>(SelectCamera));

            // camera presets
            for (uint i = 0; i < 6; i++)
            {
                addAction("/cameraPreset" + (i + 1), JoinMap.CameraPresetStart.JoinNumber + i);
            }

            asc.AddAction(MessagePath + "/isReady", new Action(PostIsReady));
            // Get status
            asc.AddAction(MessagePath + "/fullStatus", new Action(PostFullStatus));
            // Dial on string
            asc.AddAction(MessagePath + "/dial", new Action<string>(s =>
                _eisc.SetString(JoinMap.CurrentDialString.JoinNumber, s)));
            // Pulse DTMF
            AppServerController.AddAction(MessagePath + "/dtmf", new Action<string>(s =>
            {
                var join = JoinMap.Joins[s];
                if (join != null)
                {
                    if (join.JoinNumber > 0)
                    {
                        _eisc.PulseBool(join.JoinNumber, 100);
                    }
                }
            }));

            // Directory madness
            asc.AddAction(MessagePath + "/directoryRoot",
                new Action(() => _eisc.PulseBool(JoinMap.DirectoryRoot.JoinNumber)));
            asc.AddAction(MessagePath + "/directoryBack",
                new Action(() => _eisc.PulseBool(JoinMap.DirectoryFolderBack.JoinNumber)));
            asc.AddAction(MessagePath + "/directoryById", new Action<string>(s =>
            {
                // the id should contain the line number to forward to simpl
                try
                {
                    var u = ushort.Parse(s);
                    _eisc.SetUshort(JoinMap.DirectorySelectRow.JoinNumber, u);
                    _eisc.PulseBool(JoinMap.DirectoryLineSelected.JoinNumber);
                }
                catch (Exception)
                {
                    Debug.Console(1, this, Debug.ErrorLogLevel.Warning,
                        "/directoryById request contains non-numeric ID incompatible with DDVC bridge");
                }
            }));
            asc.AddAction(MessagePath + "/directorySelectContact", new Action<string>(s =>
            {
                try
                {
                    var u = ushort.Parse(s);
                    _eisc.SetUshort(JoinMap.DirectorySelectRow.JoinNumber, u);
                    _eisc.PulseBool(JoinMap.DirectoryLineSelected.JoinNumber);
                }
                catch
                {
                    Debug.Console(2, this, "Error parsing contact from {0} for path /directorySelectContact", s);
                }
            }));
            asc.AddAction(MessagePath + "/directoryDialContact",
                new Action(() => _eisc.PulseBool(JoinMap.DirectoryDialSelectedLine.JoinNumber)));
            asc.AddAction(MessagePath + "/getDirectory", new Action(() =>
            {
                if (_eisc.GetUshort(JoinMap.DirectoryRowCount.JoinNumber) > 0)
                {
                    PostDirectory();
                }
                else
                {
                    _eisc.PulseBool(JoinMap.DirectoryRoot.JoinNumber);
                }
            }));
        }

        /// <summary>
        /// 
        /// </summary>
        private void PostFullStatus()
        {
            PostStatusMessage(new
            {
                calls = GetCurrentCallList(),
                cameraMode = GetCameraMode(),
                cameraSelfView = _eisc.GetBool(JoinMap.CameraSelfView.JoinNumber),
                cameraSupportsAutoMode = _eisc.GetBool(JoinMap.CameraSupportsAutoMode.JoinNumber),
                cameraSupportsOffMode = _eisc.GetBool(JoinMap.CameraSupportsOffMode.JoinNumber),
                currentCallString = _eisc.GetString(JoinMap.CurrentCallNumber.JoinNumber),
                currentDialString = _eisc.GetString(JoinMap.CurrentDialString.JoinNumber),
                directoryContactSelected = new
                {
                    name = _eisc.GetString(JoinMap.DirectoryEntrySelectedName.JoinNumber),
                    number = _eisc.GetString(JoinMap.DirectoryEntrySelectedNumber.JoinNumber)
                },
                directorySelectedFolderName = _eisc.GetString(JoinMap.DirectorySelectedFolderName.JoinNumber),
                isInCall = _eisc.GetString(JoinMap.HookState.JoinNumber) == "Connected",
                hasDirectory = true,
                hasDirectorySearch = false,
                hasRecents = !_eisc.BooleanOutput[502].BoolValue,
                hasCameras = true,
                showCamerasWhenNotInCall = _eisc.BooleanOutput[503].BoolValue,
                selectedCamera = GetSelectedCamera(),
            });
        }

        /// <summary>
        /// 
        /// </summary>
        private void PostDirectory()
        {
            var u = _eisc.GetUshort(JoinMap.DirectoryRowCount.JoinNumber);
            var items = new List<object>();
            for (uint i = 0; i < u; i++)
            {
                var name = _eisc.GetString(JoinMap.DirectoryEntriesStart.JoinNumber + i);
                var id = (i + 1).ToString();
                // is folder or contact?
                if (name.StartsWith("[+]"))
                {
                    items.Add(new
                    {
                        folderId = id,
                        name
                    });
                }
                else
                {
                    items.Add(new
                    {
                        contactId = id,
                        name
                    });
                }
            }

            var directoryMessage = new
            {
                currentDirectory = new
                {
                    isRootDirectory = _eisc.GetBool(JoinMap.DirectoryIsRoot.JoinNumber),
                    directoryResults = items
                }
            };
            PostStatusMessage(directoryMessage);
        }

        /// <summary>
        /// 
        /// </summary>
        private void PostCameraMode()
        {
            PostStatusMessage(new
            {
                cameraMode = GetCameraMode()
            });
        }

        /// <summary>
        /// 
        /// </summary>
        private string GetCameraMode()
        {
            string m;
            if (_eisc.GetBool(JoinMap.CameraModeAuto.JoinNumber)) m = eCameraControlMode.Auto.ToString().ToLower();
            else if (_eisc.GetBool(JoinMap.CameraModeManual.JoinNumber))
                m = eCameraControlMode.Manual.ToString().ToLower();
            else m = eCameraControlMode.Off.ToString().ToLower();
            return m;
        }

        private void PostSelectedCamera()
        {
            PostStatusMessage(new
            {
                selectedCamera = GetSelectedCamera()
            });
        }

        /// <summary>
        /// 
        /// </summary>
        private string GetSelectedCamera()
        {
            var num = _eisc.GetUshort(JoinMap.CameraNumberSelect.JoinNumber);
            string m;
            if (num == 100)
            {
                m = "cameraFar";
            }
            else
            {
                m = "camera" + num;
            }
            return m;
        }

        /// <summary>
        /// 
        /// </summary>
        private void PostIsReady()
        {
            PostStatusMessage(new
            {
                isReady = true
            });
        }

        /// <summary>
        /// 
        /// </summary>
        private void PostCallsList()
        {
            PostStatusMessage(new
            {
                calls = GetCurrentCallList(),
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        private void SelectCamera(string s)
        {
            var cam = s.Substring(6);
            _eisc.SetUshort(JoinMap.CameraNumberSelect.JoinNumber,
                (ushort) (cam.ToLower() == "far" ? 100 : UInt16.Parse(cam)));
        }

        /// <summary>
        /// Turns the 
        /// </summary>
        /// <returns></returns>
        private List<CodecActiveCallItem> GetCurrentCallList()
        {
            var list = new List<CodecActiveCallItem>();
            if (_currentCallItem.Status != eCodecCallStatus.Disconnected)
            {
                list.Add(_currentCallItem);
            }
            if (_eisc.GetBool(JoinMap.IncomingCall.JoinNumber))
            {
            }
            return list;
        }
    }
}