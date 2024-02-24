using Newtonsoft.Json.Linq;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Devices.Common.Cameras;
using System;
using System.Collections.Generic;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class CameraBaseMessenger : MessengerBase
    {
        /// <summary>
        /// Device being bridged
        /// </summary>
        public CameraBase Camera { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="key"></param>
        /// <param name="camera"></param>
        /// <param name="messagePath"></param>
        public CameraBaseMessenger(string key, CameraBase camera, string messagePath)
            : base(key, messagePath, camera)
        {
            Camera = camera ?? throw new ArgumentNullException("camera");


            if (Camera is IHasCameraPresets presetsCamera)
            {
                presetsCamera.PresetsListHasChanged += PresetsCamera_PresetsListHasChanged;
            }
        }

        private void PresetsCamera_PresetsListHasChanged(object sender, EventArgs e)
        {
            var presetList = new List<CameraPreset>();

            if (Camera is IHasCameraPresets presetsCamera)
                presetList = presetsCamera.Presets;

            PostStatusMessage(JToken.FromObject(new
                {
                    presets = presetList
                })
            );
        }

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            base.CustomRegisterWithAppServer(appServerController);

            appServerController.AddAction(MessagePath + "/fullStatus", (id, content) => SendCameraFullMessageObject());


            if (Camera is IHasCameraPtzControl ptzCamera)
            {
                //  Need to evaluate how to pass through these P&H actions.  Need a method that takes a bool maybe?
                AppServerController.AddAction(MessagePath + "/cameraUp", (id, content) => HandleCameraPressAndHold(content, (b) =>
                {
                    if (b)
                    {
                        ptzCamera.TiltUp();
                        return;
                    }

                    ptzCamera.TiltStop();
                }));
                AppServerController.AddAction(MessagePath + "/cameraDown", (id, content) => HandleCameraPressAndHold(content, (b) =>
                {
                    if (b)
                    {
                        ptzCamera.TiltDown();
                        return;
                    }

                    ptzCamera.TiltStop();
                }));
                AppServerController.AddAction(MessagePath + "/cameraLeft", (id, content) => HandleCameraPressAndHold(content, (b) =>
                {
                    if (b)
                    {
                        ptzCamera.PanLeft();
                        return;
                    }

                    ptzCamera.PanStop();
                }));
                AppServerController.AddAction(MessagePath + "/cameraRight", (id, content) => HandleCameraPressAndHold(content, (b) =>
                {
                    if (b)
                    {
                        ptzCamera.PanRight();
                        return;
                    }

                    ptzCamera.PanStop();
                }));
                AppServerController.AddAction(MessagePath + "/cameraZoomIn", (id, content) => HandleCameraPressAndHold(content, (b) =>
                {
                    if (b)
                    {
                        ptzCamera.ZoomIn();
                        return;
                    }

                    ptzCamera.ZoomStop();
                }));
                AppServerController.AddAction(MessagePath + "/cameraZoomOut", (id, content) => HandleCameraPressAndHold(content, (b) =>
                {
                    if (b)
                    {
                        ptzCamera.ZoomOut();
                        return;
                    }

                    ptzCamera.ZoomStop();
                }));
            }

            if (Camera is IHasCameraAutoMode)
            {
                appServerController.AddAction(MessagePath + "/cameraModeAuto", (id, content) => (Camera as IHasCameraAutoMode).CameraAutoModeOn());

                appServerController.AddAction(MessagePath + "/cameraModeManual", (id, content) => (Camera as IHasCameraAutoMode).CameraAutoModeOff());

            }

            if (Camera is IHasPowerControl)
            {
                appServerController.AddAction(MessagePath + "/cameraModeOff", (id, content) => (Camera as IHasPowerControl).PowerOff());
                appServerController.AddAction(MessagePath + "/cameraModeManual", (id, content) => (Camera as IHasPowerControl).PowerOn());
            }


            if (Camera is IHasCameraPresets presetsCamera)
            {
                for (int i = 1; i <= 6; i++)
                {
                    var preset = i;
                    appServerController.AddAction(MessagePath + "/cameraPreset" + i, (id, content) =>
                    {
                        var msg = content.ToObject<MobileControlSimpleContent<int>>();

                        presetsCamera.PresetSelect(msg.Value);
                    });

                }
            }
        }

        private void HandleCameraPressAndHold(JToken content, Action<bool> cameraAction)
        {
            var state = content.ToObject<MobileControlSimpleContent<string>>();

            var timerHandler = PressAndHoldHandler.GetPressAndHoldHandler(state.Value);
            if (timerHandler == null)
            {
                return;
            }

            timerHandler(state.Value, cameraAction);

            cameraAction(state.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Helper method to update the full status of the camera
        /// </summary>
        private void SendCameraFullMessageObject()
        {
            var presetList = new List<CameraPreset>();

            if (Camera is IHasCameraPresets presetsCamera)
                presetList = presetsCamera.Presets;

            PostStatusMessage(JToken.FromObject(new
                {
                    cameraManualSupported = Camera is IHasCameraControls,
                    cameraAutoSupported = Camera is IHasCameraAutoMode,
                    cameraOffSupported = Camera is IHasCameraOff,
                    cameraMode = GetCameraMode(),
                    hasPresets = Camera is IHasCameraPresets,
                    presets = presetList
                })
            );
        }

        /// <summary>
        /// Computes the current camera mode
        /// </summary>
        /// <returns></returns>
        private string GetCameraMode()
        {
            string m;
            if (Camera is IHasCameraAutoMode && (Camera as IHasCameraAutoMode).CameraAutoModeIsOnFeedback.BoolValue)
                m = eCameraControlMode.Auto.ToString().ToLower();
            else if (Camera is IHasPowerControlWithFeedback && !(Camera as IHasPowerControlWithFeedback).PowerIsOnFeedback.BoolValue)
                m = eCameraControlMode.Off.ToString().ToLower();
            else
                m = eCameraControlMode.Manual.ToString().ToLower();
            return m;
        }
    }
}