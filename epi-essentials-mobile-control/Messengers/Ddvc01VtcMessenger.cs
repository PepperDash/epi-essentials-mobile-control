using System;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common.Codec;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class SIMPL01VtcMessenger : MessengerBase
    {
        private readonly BasicTriList _eisc;

        /********* Bools *********/

        /// <summary>
        /// 724
        /// </summary>
        private const uint BDialHangup = 724;

        /// <summary>
        /// 750
        /// </summary>
        private const uint BCallIncoming = 750;

        /// <summary>
        /// 751
        /// </summary>
        private const uint BIncomingAnswer = 751;

        /// <summary>
        /// 752
        /// </summary>
        private const uint BIncomingReject = 752;

        /// <summary>
        /// 741
        /// </summary>
        private const uint BSpeedDial1 = 741;

        /// <summary>
        /// 742
        /// </summary>
        private const uint BSpeedDial2 = 742;

        /// <summary>
        /// 743
        /// </summary>
        private const uint BSpeedDial3 = 743;

        /// <summary>
        /// 744
        /// </summary>
        private const uint BSpeedDial4 = 744;

        /// <summary>
        /// 800
        /// </summary>
        private const uint BDirectorySearchBusy = 800;

        /// <summary>
        /// 801 
        /// </summary>
        private const uint BDirectoryLineSelected = 801;

        /// <summary>
        /// 801 when selected entry is a contact
        /// </summary>
        private const uint BDirectoryEntryIsContact = 801;

        /// <summary>
        /// 802 To show/hide back button
        /// </summary>
        private const uint BDirectoryIsRoot = 802;

        /// <summary>
        /// 803 Pulse from system to inform us when directory is ready
        /// </summary>
        private const uint DDirectoryHasChanged = 803;

        /// <summary>
        /// 804
        /// </summary>
        private const uint BDirectoryRoot = 804;

        /// <summary>
        /// 805
        /// </summary>
        private const uint BDirectoryFolderBack = 805;

        /// <summary>
        /// 806
        /// </summary>
        private const uint BDirectoryDialSelectedLine = 806;

        /// <summary>
        /// 811
        /// </summary>
        private const uint BCameraControlUp = 811;

        /// <summary>
        /// 812
        /// </summary>
        private const uint BCameraControlDown = 812;

        /// <summary>
        /// 813
        /// </summary>
        private const uint BCameraControlLeft = 813;

        /// <summary>
        /// 814
        /// </summary>
        private const uint BCameraControlRight = 814;

        /// <summary>
        /// 815
        /// </summary>
        private const uint BCameraControlZoomIn = 815;

        /// <summary>
        /// 816
        /// </summary>
        private const uint BCameraControlZoomOut = 816;

        /// <summary>
        /// 821 - 826
        /// </summary>
        private const uint BCameraPresetStart = 821;

        /// <summary>
        /// 831
        /// </summary>
        private const uint BCameraModeAuto = 831;

        /// <summary>
        /// 832
        /// </summary>
        private const uint BCameraModeManual = 832;

        /// <summary>
        /// 833
        /// </summary>
        private const uint BCameraModeOff = 833;

        /// <summary>
        /// 841
        /// </summary>
        private const uint BCameraSelfView = 841;

        /// <summary>
        /// 842
        /// </summary>
        private const uint BCameraLayout = 842;

        /// <summary>
        /// 843
        /// </summary>
        private const uint BCameraSupportsAutoMode = 843;

        /// <summary>
        /// 844
        /// </summary>
        private const uint BCameraSupportsOffMode = 844;


        /********* Ushorts *********/

        /// <summary>
        /// 760
        /// </summary>
        private const uint UCameraNumberSelect = 760;

        /// <summary>
        /// 801
        /// </summary>
        private const uint UDirectorySelectRow = 801;

        /// <summary>
        /// 801
        /// </summary>
        private const uint UDirectoryRowCount = 801;


        /********* Strings *********/

        /// <summary>
        /// 701
        /// </summary>
        private const uint SCurrentDialString = 701;

        /// <summary>
        /// 702
        /// </summary>
        private const uint SCurrentCallName = 702;

        /// <summary>
        /// 703
        /// </summary>
        private const uint SCurrentCallNumber = 703;

        /// <summary>
        /// 731
        /// </summary>
        private const uint SHookState = 731;

        /// <summary>
        /// 722
        /// </summary>
        private const uint SCallDirection = 722;

        /// <summary>
        /// 751
        /// </summary>
        private const uint SIncomingCallName = 751;

        /// <summary>
        /// 752
        /// </summary>
        private const uint SIncomingCallNumber = 752;

        /// <summary>
        /// 800
        /// </summary>
        private const uint SDirectorySearchString = 800;

        /// <summary>
        /// 801-1055
        /// </summary>
        private const uint SDirectoryEntriesStart = 801;

        /// <summary>
        /// 1056
        /// </summary>
        private const uint SDirectoryEntrySelectedName = 1056;

        /// <summary>
        /// 1057
        /// </summary>
        private const uint SDirectoryEntrySelectedNumber = 1057;

        /// <summary>
        /// 1058
        /// </summary>
        private const uint SDirectorySelectedFolderName = 1058;


        /// <summary>
        /// 701-712 0-9*#
        /// </summary>
        private readonly Dictionary<string, uint> _dtmfMap = new Dictionary<string, uint>
        {
            {"1", 701},
            {"2", 702},
            {"3", 703},
            {"4", 704},
            {"5", 705},
            {"6", 706},
            {"7", 707},
            {"8", 708},
            {"9", 709},
            {"0", 710},
            {"*", 711},
            {"#", 712},
        };

        private readonly CodecActiveCallItem _currentCallItem;
        private CodecActiveCallItem _incomingCallItem;

        private ushort _previousDirectoryLength;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="eisc"></param>
        /// <param name="messagePath"></param>
        public SIMPL01VtcMessenger(string key, BasicTriList eisc, string messagePath)
            : base(key, messagePath)
        {
            _eisc = eisc;

            _currentCallItem = new CodecActiveCallItem {Type = eCodecCallType.Video, Id = "-video-"};
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appServerController"></param>
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
            var asc = appServerController;
            _eisc.SetStringSigAction(SHookState, s =>
            {
                _currentCallItem.Status = (eCodecCallStatus) Enum.Parse(typeof (eCodecCallStatus), s, true);
                PostFullStatus(); // SendCallsList();
            });

            _eisc.SetStringSigAction(SCurrentCallNumber, s =>
            {
                _currentCallItem.Number = s;
                PostCallsList();
            });

            _eisc.SetStringSigAction(SCurrentCallName, s =>
            {
                _currentCallItem.Name = s;
                PostCallsList();
            });

            //EISC.SetStringSigAction(SCallDirection, s =>
            //{
            //    CurrentCallItem.Direction = (eCodecCallDirection)Enum.Parse(typeof(eCodecCallDirection), s, true);
            //    PostCallsList();
            //});

            _eisc.SetBoolSigAction(BCallIncoming, b =>
            {
                if (b)
                {
                    var ica = new CodecActiveCallItem
                    {
                        Direction = eCodecCallDirection.Incoming,
                        Id = "-video-incoming",
                        Name = _eisc.GetString(SIncomingCallName),
                        Number = _eisc.GetString(SIncomingCallNumber),
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

            _eisc.SetBoolSigAction(BCameraSupportsAutoMode, b => PostStatusMessage(new
            {
                cameraSupportsAutoMode = b
            }));
            _eisc.SetBoolSigAction(BCameraSupportsOffMode, b => PostStatusMessage(new
            {
                cameraSupportsOffMode = b
            }));

            // Directory insanity
            _eisc.SetUShortSigAction(UDirectoryRowCount, u =>
            {
                // The length of the list comes in before the list does.
                // Splice the sig change operation onto the last string sig that will be changing
                // when the directory entries make it through.
                if (_previousDirectoryLength > 0)
                {
                    _eisc.ClearStringSigAction(SDirectoryEntriesStart + _previousDirectoryLength - 1);
                }
                _eisc.SetStringSigAction(SDirectoryEntriesStart + u - 1, s => PostDirectory());
                _previousDirectoryLength = u;
            });

            _eisc.SetStringSigAction(SDirectoryEntrySelectedName, s => PostStatusMessage(new
            {
                directoryContactSelected = new
                {
                    name = _eisc.GetString(SDirectoryEntrySelectedName),
                }
            }));

            _eisc.SetStringSigAction(SDirectoryEntrySelectedNumber, s => PostStatusMessage(new
            {
                directoryContactSelected = new
                {
                    number = _eisc.GetString(SDirectoryEntrySelectedNumber),
                }
            }));

            _eisc.SetStringSigAction(SDirectorySelectedFolderName, s => PostStatusMessage(new
            {
                directorySelectedFolderName = _eisc.GetString(SDirectorySelectedFolderName)
            }));

            _eisc.SetSigTrueAction(BCameraModeAuto, PostCameraMode);
            _eisc.SetSigTrueAction(BCameraModeManual, PostCameraMode);
            _eisc.SetSigTrueAction(BCameraModeOff, PostCameraMode);

            _eisc.SetBoolSigAction(BCameraSelfView, b => PostStatusMessage(new
            {
                cameraSelfView = b
            }));

            _eisc.SetUShortSigAction(UCameraNumberSelect, u => PostSelectedCamera());


            // Add press and holds using helper action
            Action<string, uint> addPhAction = (s, u) =>
                AppServerController.AddAction(MessagePath + s, new PressAndHoldAction(b => _eisc.SetBool(u, b)));
            addPhAction("/cameraUp", BCameraControlUp);
            addPhAction("/cameraDown", BCameraControlDown);
            addPhAction("/cameraLeft", BCameraControlLeft);
            addPhAction("/cameraRight", BCameraControlRight);
            addPhAction("/cameraZoomIn", BCameraControlZoomIn);
            addPhAction("/cameraZoomOut", BCameraControlZoomOut);

            // Add straight pulse calls using helper action
            Action<string, uint> addAction = (s, u) =>
                AppServerController.AddAction(MessagePath + s, new Action(() => _eisc.PulseBool(u, 100)));
            addAction("/endCallById", BDialHangup);
            addAction("/endAllCalls", BDialHangup);
            addAction("/acceptById", BIncomingAnswer);
            addAction("/rejectById", BIncomingReject);
            addAction("/speedDial1", BSpeedDial1);
            addAction("/speedDial2", BSpeedDial2);
            addAction("/speedDial3", BSpeedDial3);
            addAction("/speedDial4", BSpeedDial4);
            addAction("/cameraModeAuto", BCameraModeAuto);
            addAction("/cameraModeManual", BCameraModeManual);
            addAction("/cameraModeOff", BCameraModeOff);
            addAction("/cameraSelfView", BCameraSelfView);
            addAction("/cameraLayout", BCameraLayout);

            asc.AddAction("/cameraSelect", new Action<string>(SelectCamera));

            // camera presets
            for (uint i = 0; i < 6; i++)
            {
                addAction("/cameraPreset" + (i + 1), BCameraPresetStart + i);
            }

            asc.AddAction(MessagePath + "/isReady", new Action(PostIsReady));
            // Get status
            asc.AddAction(MessagePath + "/fullStatus", new Action(PostFullStatus));
            // Dial on string
            asc.AddAction(MessagePath + "/dial", new Action<string>(s =>
                _eisc.SetString(SCurrentDialString, s)));
            // Pulse DTMF
            asc.AddAction(MessagePath + "/dtmf", new Action<string>(s =>
            {
                if (_dtmfMap.ContainsKey(s))
                {
                    _eisc.PulseBool(_dtmfMap[s], 100);
                }
            }));

            // Directory madness
            asc.AddAction(MessagePath + "/directoryRoot", new Action(() => _eisc.PulseBool(BDirectoryRoot)));
            asc.AddAction(MessagePath + "/directoryBack", new Action(() => _eisc.PulseBool(BDirectoryFolderBack)));
            asc.AddAction(MessagePath + "/directoryById", new Action<string>(s =>
            {
                // the id should contain the line number to forward to simpl
                try
                {
                    var u = ushort.Parse(s);
                    _eisc.SetUshort(UDirectorySelectRow, u);
                    _eisc.PulseBool(BDirectoryLineSelected);
                }
                catch (Exception)
                {
                    Debug.Console(1, this, Debug.ErrorLogLevel.Warning,
                        "/directoryById request contains non-numeric ID incompatible with SIMPL bridge");
                }
            }));
            asc.AddAction(MessagePath + "/directorySelectContact", new Action<string>(s =>
            {
                try
                {
                    var u = ushort.Parse(s);
                    _eisc.SetUshort(UDirectorySelectRow, u);
                    _eisc.PulseBool(BDirectoryLineSelected);
                }
                catch (FormatException)
                {
                    Debug.Console(2, this, "error parsing string to ushort for path /directorySelectContact");
                }
            }));
            asc.AddAction(MessagePath + "/directoryDialContact",
                new Action(() => _eisc.PulseBool(BDirectoryDialSelectedLine)));
            asc.AddAction(MessagePath + "/getDirectory", new Action(() =>
            {
                if (_eisc.GetUshort(UDirectoryRowCount) > 0)
                {
                    PostDirectory();
                }
                else
                {
                    _eisc.PulseBool(BDirectoryRoot);
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
                cameraSelfView = _eisc.GetBool(BCameraSelfView),
                cameraSupportsAutoMode = _eisc.GetBool(BCameraSupportsAutoMode),
                cameraSupportsOffMode = _eisc.GetBool(BCameraSupportsOffMode),
                currentCallString = _eisc.GetString(SCurrentCallNumber),
                currentDialString = _eisc.GetString(SCurrentDialString),
                directoryContactSelected = new
                {
                    name = _eisc.GetString(SDirectoryEntrySelectedName),
                    number = _eisc.GetString(SDirectoryEntrySelectedNumber)
                },
                directorySelectedFolderName = _eisc.GetString(SDirectorySelectedFolderName),
                isInCall = _eisc.GetString(SHookState) == "Connected",
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
            var u = _eisc.GetUshort(UDirectoryRowCount);
            var items = new List<object>();
            for (uint i = 0; i < u; i++)
            {
                var name = _eisc.GetString(SDirectoryEntriesStart + i);
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
                    isRootDirectory = _eisc.GetBool(BDirectoryIsRoot),
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
            if (_eisc.GetBool(BCameraModeAuto)) m = "auto";
            else if (_eisc.GetBool(BCameraModeManual)) m = "manual";
            else m = "off";
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
            var num = _eisc.GetUshort(UCameraNumberSelect);
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
            _eisc.SetUshort(UCameraNumberSelect, (ushort) (cam.ToLower() == "far" ? 100 : UInt16.Parse(cam)));
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
                if (_currentCallItem.Direction != eCodecCallDirection.Incoming)
                {
                    _currentCallItem.Direction = eCodecCallDirection.Outgoing;
                }
                list.Add(_currentCallItem);
            }
            if (_eisc.GetBool(BCallIncoming))
            {
            }
            return list;
        }
    }
}