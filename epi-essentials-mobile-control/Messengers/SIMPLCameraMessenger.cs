﻿using System;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Bridges;
using PepperDash.Essentials.Devices.Common.Cameras;

namespace PepperDash.Essentials.AppServer.Messengers
{
// ReSharper disable once InconsistentNaming
    public class SIMPLCameraMessenger : MessengerBase
    {
        private readonly BasicTriList _eisc;

        private readonly CameraControllerJoinMap _joinMap;


        public SIMPLCameraMessenger(string key, BasicTriList eisc, string messagePath, uint joinStart)
            : base(key, messagePath)
        {
            _eisc = eisc;

            _joinMap = new CameraControllerJoinMap(joinStart);

            _eisc.SetUShortSigAction(_joinMap.NumberOfPresets.JoinNumber, u => SendCameraFullMessageObject());

            _eisc.SetBoolSigAction(_joinMap.CameraModeAuto.JoinNumber, b => PostCameraMode());
            _eisc.SetBoolSigAction(_joinMap.CameraModeManual.JoinNumber, b => PostCameraMode());
            _eisc.SetBoolSigAction(_joinMap.CameraModeOff.JoinNumber, b => PostCameraMode());
        }


        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
            var asc = appServerController;

            asc.AddAction(MessagePath + "/fullStatus", new Action(SendCameraFullMessageObject));

            // Add press and holds using helper action
            Action<string, uint> addPhAction = (s, u) =>
                asc.AddAction(MessagePath + s, new PressAndHoldAction(b => _eisc.SetBool(u, b)));
            addPhAction("/cameraUp", _joinMap.TiltUp.JoinNumber);
            addPhAction("/cameraDown", _joinMap.TiltDown.JoinNumber);
            addPhAction("/cameraLeft", _joinMap.PanLeft.JoinNumber);
            addPhAction("/cameraRight", _joinMap.PanRight.JoinNumber);
            addPhAction("/cameraZoomIn", _joinMap.ZoomIn.JoinNumber);
            addPhAction("/cameraZoomOut", _joinMap.ZoomOut.JoinNumber);

            Action<string, uint> addAction = (s, u) =>
                asc.AddAction(MessagePath + s, new Action(() => _eisc.PulseBool(u, 100)));

            addAction("/cameraModeAuto", _joinMap.CameraModeAuto.JoinNumber);
            addAction("/cameraModeManual", _joinMap.CameraModeManual.JoinNumber);
            addAction("/cameraModeOff", _joinMap.CameraModeOff.JoinNumber);

            var presetStart = _joinMap.PresetRecallStart.JoinNumber;
            var presetEnd = _joinMap.PresetRecallStart.JoinNumber + _joinMap.PresetRecallStart.JoinSpan;

            int presetId = 1;
            // camera presets
            for (uint i = presetStart; i <= presetEnd; i++)
            {
                addAction("/cameraPreset" + (presetId), i);
                presetId++;
            }
        }

        public void CustomUnregsiterWithAppServer(MobileControlSystemController appServerController)
        {
            appServerController.RemoveAction(MessagePath + "/fullStatus");

            appServerController.RemoveAction(MessagePath + "/cameraUp");
            appServerController.RemoveAction(MessagePath + "/cameraDown");
            appServerController.RemoveAction(MessagePath + "/cameraLeft");
            appServerController.RemoveAction(MessagePath + "/cameraRight");
            appServerController.RemoveAction(MessagePath + "/cameraZoomIn");
            appServerController.RemoveAction(MessagePath + "/cameraZoomOut");
            appServerController.RemoveAction(MessagePath + "/cameraModeAuto");
            appServerController.RemoveAction(MessagePath + "/cameraModeManual");
            appServerController.RemoveAction(MessagePath + "/cameraModeOff");

            _eisc.SetUShortSigAction(_joinMap.NumberOfPresets.JoinNumber, null);

            _eisc.SetBoolSigAction(_joinMap.CameraModeAuto.JoinNumber, null);
            _eisc.SetBoolSigAction(_joinMap.CameraModeManual.JoinNumber, null);
            _eisc.SetBoolSigAction(_joinMap.CameraModeOff.JoinNumber, null);
        }

        /// <summary>
        /// Helper method to update the full status of the camera
        /// </summary>
        private void SendCameraFullMessageObject()
        {
            var presetList = new List<CameraPreset>();

            // Build a list of camera presets based on the names and count 
            if (_eisc.GetBool(_joinMap.SupportsPresets.JoinNumber))
            {
                var presetStart = _joinMap.PresetLabelStart.JoinNumber;
                var presetEnd = _joinMap.PresetLabelStart.JoinNumber + _joinMap.NumberOfPresets.JoinNumber;

                var presetId = 1;
                for (uint i = presetStart; i < presetEnd; i++)
                {
                    var presetName = _eisc.GetString(i);
                    var preset = new CameraPreset(presetId, presetName, string.IsNullOrEmpty(presetName), true);
                    presetList.Add(preset);
                    presetId++;
                }
            }

            PostStatusMessage(new
            {
                cameraMode = GetCameraMode(),
                hasPresets = _eisc.GetBool(_joinMap.SupportsPresets.JoinNumber),
                presets = presetList
            });
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
        /// Computes the current camera mode
        /// </summary>
        /// <returns></returns>
        private string GetCameraMode()
        {
            string m;
            if (_eisc.GetBool(_joinMap.CameraModeAuto.JoinNumber)) m = eCameraControlMode.Auto.ToString().ToLower();
            else if (_eisc.GetBool(_joinMap.CameraModeManual.JoinNumber))
                m = eCameraControlMode.Manual.ToString().ToLower();
            else m = eCameraControlMode.Off.ToString().ToLower();
            return m;
        }
    }
}