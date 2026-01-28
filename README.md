# Mobile Control Essentials Plugin

# !!!!As of Essentials 2.1.0 this plugin is no longer necessary as it was added to Essentials as a project!!!

This plugin enables Essentials to communicate with the Mobile Control User App.

This happens via two methods:

- An Edge Server running the Mobile Control API stack to enable user provided mobile devices to choose and control rooms using either the QR code or numeric user code mechanism
- If using a 4-series processor, the processor can run a websocket server that allows direct communication from dedicated in-room devices.

Both methods are optionally configurable and can operate independently or in parallel.

A user gets served the Mobile Control User App to their browser via HTTP and subsequently a websocket connection is made to the plugin to allow serial communcation for realtime system control and feedback.

## Download Links for Dependencies

[PepperDash Essentials - Releases](https://github.com/PepperDash/Essentials/releases) (v2.0.0 minimum)

[PepperDash Mobile Control React App Core - Releases](https://github.com/PepperDash/mobile-control-react-app-core)

## Direct Connection to 4-Series Processor with or without Edge Server

The steps below detail how to load Mobile Control on a 4-Series processor running a websocket server to be served to local devices like an in-room touchpanel or a dedicated room PC or tablet. Devices that connect in this manner can only connect to a single room, configured by generating a token via console commands on this plugin.

An Edge Server can still optionally be used for mobile devices to connect to any room configured to communicate with the Edge Server.

## Folder Structure

```plaintext
Processor
├── html
├── program[xx] // where xx is the two digit slot number
│   └── PepperDashEssentials-X.Y.Z.cpz // Essentials application file
└── user
    └── program[x] // where x is the slot number
        ├── configurationFile*.json // Essentials configuration file
        ├── ir
        ├── mcUserApp // optional folder where user app is served from
        │   ├── _local-config
        │   │   └── _config.local.json // config file for the user app
        │   ├── {Contents of mobile-control-app-directServer-vX.Y.Z.zip file}
        ├── plugins // plugin must be loaded to this folder
        │   └── epi-essentials-mobile-control-4s-X.Y.Z.cplz // the 4s version is for 4-Series and is required for direct websocket commmunications to work.
        └── sgd
```

## Configuration Snippets

### \_config.local.json

This file is created automatically when program starts up and served up to the client device as part of the Mobile Control app. It contains the information to configure the app and to connect to the processor via websocket.

```json
{
  "apiPath": "http://{processor-ip}:{50000 + Essentials Slot #}/mc/api", // This value must be set to the IP of the processor and the port configured for the websocket
  "gatewayAppPath": "https://{processor-ip}:{50000 + Essentials Slot #}/mc/gateway", // Not used in direct connection scenarios
  "enableDev": true,
  "logoPath": "logo/logo.png",
  "iconSet": "GOOGLE", // Set the icon set to be used.  Valid values are "GOOGLE", "HABANERO" or "NEO
  "loginMode": "room-list",
  "modes": {
    "room-list": {
      "listPageText": "Please select your room",
      "loginHelpText": "Please select your room from the list, then enter the code shown on the display in the room. (Configurable message)",
      "passcodePageText": "Please enter the code shown on this room's display"
    },
    "partnerMetadata": [
    {
        "role": "consultant",
        "description": "Design and consulting by [Consultant Name]",
        "logoPath": "logo/consultant.png"
    },
    {
        "role": "integrator",
        "description": "Integration services by [Integrator Name]",
        "logoPath": "logo/integrator.png"
    }
]
}
}
```

### Essentials Mobile Control Device Snippet

```json
{
    "key": "mobileControl-25",
    "name": "Mobile Control",
    "type": "mobileControl",
    "group": "api",
    "id": "c9138b74-ae67-4837-89ee-e1fe91e0f0d8",
    "properties": {
        "clientAppUrl": "http://{server-hostname}/mc/gateway", // url of the gateway app on an Edge Server
        "serverUrl": "http://{server-hostname}/mc/api", // url of the MC API on an Edge Server
        "enableApiServer": false, // set to true to enable communication with an Edge Server
        "directServer": { // Optional object to configure for direct communication
            "enableDirectServer": true, // set to true to enable direct communication to the plugins websocket server
            "port": 50001 // Optional custom port number for the websocket communication.  If not specified, default port will be 50000 + the program slot number
        },
        "applicationConfig": { // Optional object to create configuration for the MC Application
            "enableDev": false, // Enables dev information in the application
            "logoPath": "logo/logo.png", // path to the logo for the background in the application
            "iconSet": "GOOGLE", // icon set to be used. Valued values are "GOOGLE", "HABANERO", or "NEO"
            "loginMode": "room-list", // should always be room-list
            "modes": {
                "room-list": {
                    "listPageText": "Please select your room",
                    "loginHelpText": "Please select your room from the list, then enter the code shown on the display.",
                    "passcodePageText": "Please enter the code shown on this room's display"
                }
            },
            "partnerMetadata": [
                {
                    "role": "consultant",
                    "description": "Design and consulting by [Consultant Name]",
                    "logoPath": "logo/consultant.png"
                },
                {
                    "role": "integrator",
                    "description": "Integration services by [Integrator Name]",
                    "logoPath": "logo/integrator.png"
                }
            ]
        }
}
}
```

`properties.directServer.port` is 50,000 + Essentials Slot # and should be the same port used in the `apiPath` and `gatewayAppPath` URLs of the `_config.local.json` file.

## Mobile Control Plugin Configuration on 4-Series Processor Using Console Commands

### Add/Remove UI Client for Direct Communication

```plaintext
mobileadduiclient(:essentials-slot-#) {room-key} {grant-code} // currently the grant code is not enforced. Any string can be used

// example
mobileadduiclient:1 room1 AOIUYGHG

mobileremoveuiclient(:essentials-slot-#) {token} // removes the ui client matching the specified token

// example
mobileremoveuiclient:1 81c4eb3c-dbc5-410c-8816-90500f474236
```

The `room-key` value must match the key of a room defined in the `rooms` array of the running Essentials configuration file.

Each connection requires a unique `Token` that serves to differentiate between unique user interfaces, allowing selective updates and unique user experience sessions on unique devices.

### Get Mobile Control Info

The following commands gets the current mobile control info. The `Client URL` is needed for the Crestron TS(W) General Web URL field to run on a TS(W) panel directly.

```plaintext
// get mobile control info
mobileinfo

// exmaple mobile control info response
DIN-AP4>mobileinfo

Mobile Control Edge Server API Information:
    Not Enabled in Config.

Mobile Control Direct Server Infromation:
    User App URL: http://10.0.0.223:50001/mc/app?token=[insert_client_token]
    Server port: 50001

    UI Client Info:
    Tokens Defined: 1
    Clients Connected: 0

Client 1:
Room Key: room1
Token: 81c4eb3c-dbc5-410c-8816-90500f474236
Client URL: http://10.0.0.223:50001/mc/app?token=81c4eb3c-dbc5-410c-8816-90500f474236
Connected: False
Duration: Not Connected


DIN-AP4>
```

## Messengers for communicating between Essentials Devices and the Client User Interface

The included library `mobile-control-messengers` contains the `MessengerBase` class and a set of messengers that correspond either to common abstract base classes or to specific interfaces and are used to generate a dynamic API for the User Interface client applications to integrate with.

As part of the Essentials program startup cycle, this plugin will iterate through the loaded devices and rooms and attempt to create messengers for each device for every base class or interface that device can successfully be cast as.

In addition to the automatically instantiated messengers, plugin devices can reference the `mobile-control-messengers` project via nuget as a dependency and thus inherit from the `MessengerBase` class to create custom messengers.  These custom messengers will have to be manually added to the `MobileControlSystemController` instance.

To view the paths at runtime, the console command `mobilecontrolshowactionpaths:[slotnumber]` will print all the action paths for the mobile control API. 


<!-- START Minimum Essentials Framework Versions -->
### Minimum Essentials Framework Versions

- 2.0.0
- 1.12.5
<!-- END Minimum Essentials Framework Versions -->
<!-- START Config Example -->
### Config Example

```json
{
    "key": "GeneratedKey",
    "uid": 1,
    "name": "GeneratedName",
    "type": "mctsw750",
    "group": "Group",
    "properties": {
        "apiPath": "SampleString",
        "gatewayAppPath": "SampleString",
        "enableDev": true,
        "LogoPath": "SampleString",
        "iconSet": "SampleValue",
        "loginMode": "SampleString",
        "modes": {
            "SampleString": {
                "listPageText": "SampleString",
                "loginHelpText": "SampleString",
                "passcodePageText": "SampleString"
            }
        },
        "enableRemoteLogging": true,
        "PartnerMetadata": [
            {
                "role": "SampleString",
                "description": "SampleString",
                "logoPath": "SampleString"
            }
        ]
    }
}
```
<!-- END Config Example -->
<!-- START Supported Types -->
### Supported Types

- mctsw750
- mcts770
- mctsw760
- mcts1070
- mctsw1070
- mctsw1050
- mccrestronapp
- mcxpanel
- mctsw1060
- mctsw770
- mctsw550
- mctsw570
- mctsw560
<!-- END Supported Types -->
<!-- START Join Maps -->
### Join Maps

#### Digitals

| Join | Type (RW) | Description |
| --- | --- | --- |
| 1 | R | Use Advanced Sharing Mode |
| 1 | R | Use Advanced Sharing Mode |
| 2 | R | Use Advanced Sharing Mode |
| 3 | R | Use Advanced Sharing Mode |
| 21 | R | Hang Up |
| 51 | R | Answer Incoming Call |
| 52 | R | Reject Incoming Call |
| 41 | R | Speed Dial |
| 10 | R | DTMF 0 |
| 1 | R | DTMF 1 |
| 2 | R | DTMF 2 |
| 3 | R | DTMF 3 |
| 4 | R | DTMF 4 |
| 5 | R | DTMF 5 |
| 6 | R | DTMF 6 |
| 7 | R | DTMF 7 |
| 8 | R | DTMF 8 |
| 9 | R | DTMF 9 |
| 11 | R | DTMF * |
| 12 | R | DTMF # |
| 1 | R | Master Volume Mute Toggle/FB/Level/Label |
| 2 | R | Volume Mute Toggle/FB/Level/Label |
| 12 | R | Privacy Mute Toggle/FB |
| 41 | R | Prompt User for Code |
| 42 | R | Client Joined |
| 48 | R | Enable Activity Phone Call |
| 49 | R | Enable Activity Video Call |
| 51 | R | Activity Share |
| 52 | R | Activity Phone Call |
| 53 | R | Activity Video Call |
| 61 | R | Shutdown Cancel |
| 62 | R | Shutdown End |
| 63 | R | Shutdown Start |
| 71 | R | Source Changed |
| 100 | R | Config is local to Essentials |
| 261 | R | Speed Dial Visible |
| 301 | R | Room Is On |
| 500 | R | Config info from SIMPL is ready |
| 501 | R | Config info from SIMPL is ready |
| 501 | R | Config info from SIMPL is ready |
| 502 | R | Hide Video Conference Recents |
| 503 | R | Show camera when not in call |
| 504 | R | Use Source Enabled Joins |
| 601 | R | Source is not sharable |
| 621 | R | Source is enabled/visible |
| 641 | R | Source is controllable |
| 661 | R | Source is Audio Source |
| 505 | R | Supports Advanced Sharing |
| 506 | R | Use Destination Enable |
| 507 | R | Share Mode Toggle Visible to User |
| 801 | R | Show Destination on UI |
| 24 | R | Hang Up |
| 50 | R | Incoming Call |
| 51 | R | Answer Incoming Call |
| 52 | R | Reject Incoming Call |
| 41 | R | Speed Dial |
| 100 | R | Directory Search Busy FB |
| 101 | R | Directory Line Selected FB |
| 101 | R | Directory Selected Entry Is Contact FB |
| 102 | R | Directory is on Root FB |
| 103 | R | Directory has changed FB |
| 104 | R | Go to Directory Root |
| 105 | R | Go back one directory level |
| 106 | R | Dial selected directory line |
| 111 | R | Camera Tilt Up |
| 112 | R | Camera Tilt Down |
| 113 | R | Camera Pan Left |
| 114 | R | Camera Pan Right |
| 115 | R | Camera Zoom In |
| 116 | R | Camera Zoom Out |
| 121 | R | Camera Presets |
| 131 | R | Camera Mode Auto |
| 132 | R | Camera Mode Manual |
| 133 | R | Camera Mode Off |
| 141 | R | Camera Self View Toggle/FB |
| 142 | R | Camera Layout Toggle |
| 143 | R | Camera Supports Auto Mode FB |
| 144 | R | Camera Supports Off Mode FB |
| 60 | R | Camera Number Select/FB |
| 1 | R | DTMF 1 |
| 2 | R | DTMF 2 |
| 3 | R | DTMF 3 |
| 4 | R | DTMF 4 |
| 5 | R | DTMF 5 |
| 6 | R | DTMF 6 |
| 7 | R | DTMF 7 |
| 8 | R | DTMF 8 |
| 9 | R | DTMF 9 |
| 10 | R | DTMF 0 |
| 11 | R | DTMF * |
| 12 | R | DTMF # |

#### Analogs

| Join | Type (RW) | Description |
| --- | --- | --- |
| 61 | R | Shutdown Cancel |
| 101 | R | Number of Auxilliary Faders |
| 101 | R | Directory Select Row |
| 101 | R | Directory Row Count FB |

#### Serials

| Join | Type (RW) | Description |
| --- | --- | --- |
| 51 | R | Source to Route to Destination & FB |
| 61 | R | Source to Route to Destination & FB |
| 1 | R | Current Dial String |
| 11 | R | Current Call Number |
| 12 | R | Current Call Name |
| 21 | R | Current Hook State |
| 22 | R | Current Call Direction |
| 51 | R | Incoming Call Name |
| 52 | R | Incoming Call Number |
| 403 | R | QR Code URL |
| 404 | R | Portal System URL |
| 71 | R | Key of selected source |
| 241 | R | Speed Dial names |
| 251 | R | Speed Dial numbers |
| 401 | R | User Code |
| 402 | R | Server URL |
| 501 | R | Room Name |
| 502 | R | Room help message |
| 503 | R | Room help number |
| 504 | R | Room phone number |
| 505 | R | Room URI |
| 601 | R | Source Names |
| 621 | R | Source Icons |
| 641 | R | Source Keys |
| 701 | R | Source Control Device Keys |
| 661 | R | Source Types |
| 761 | R | Near End Camera Names |
| 771 | R | Far End Camera Name |
| 801 | R | Destination Name |
| 811 | R | Destination Device Key |
| 821 | R | Destination type. Should be Audio, Video, AudioVideo |
| 1 | R | Current Dial String |
| 2 | R | Current Call Name |
| 3 | R | Current Call Number |
| 31 | R | Current Hook State |
| 22 | R | Current Call Direction |
| 51 | R | Incoming Call Name |
| 52 | R | Incoming Call Number |
| 100 | R | Directory Search String |
| 101 | R | Directory Entries |
| 356 | R | Selected Directory Entry Name |
| 357 | R | Selected Directory Entry Number |
| 358 | R | Selected Directory Folder Name |
<!-- END Join Maps -->
<!-- START Interfaces Implemented -->
### Interfaces Implemented

- IMobileControlMessage
#else
    public class MobileControlMessage
#endif
- IMobileControlAction
- IHasFeedback
- ITswAppControl
- ITswZoomControl
- IDeviceInfoProvider
- IMobileControlTouchpanelController
- ITheme
- IChannel
- INumericKeypad
- IQueueMessage
- IMobileControl
- IMobileControlMessenger
#else
    public abstract class MessengerBase: EssentialsDevice
#endif
- IMobileControlRoomMessenger
- IDelayedConfiguration
<!-- END Interfaces Implemented -->
<!-- START Base Classes -->
### Base Classes

- MessengerBase
- TouchpanelBase
- CrestronTouchpanelPropertiesConfig
- WebSocketBehavior
- CrestronLocalSecretsProvider
- WebApiBaseRequestAsyncHandler
- WebApiBaseRequestHandler
- Device
- EssentialsConfig
- EssentialsDevice
- JoinMapBaseAdvanced
- EiscApiPropertiesConfig.ApiDevicePropertiesConfig
- VideoCodecBaseMessenger
- MobileControlBridgeBase
- Dictionary<string
- uint>
<!-- END Base Classes -->
<!-- START Public Methods -->
### Public Methods

- public void SendFullStatus()
- public void SendFullStatus()
- public void UpdateTheme(string theme)
- public void SetAppUrl(string url)
- public void HideOpenApp()
- public void OpenApp()
- public void CloseOpenApp()
- public void EndZoomCall()
- public void UpdateDeviceInfo()
- public void UpdateSecret()
- public void StopServer()
- public void SendMessageToAllClients(string message)
- public void SendMessageToClient(object clientId, string message)
- public void SetClient(UiClient client)
- public ServerTokenSecrets DeserializeSecret()
- public void ChannelUp(bool pressRelease)
- public void ChannelDown(bool pressRelease)
- public void LastChannel(bool pressRelease)
- public void Guide(bool pressRelease)
- public void Info(bool pressRelease)
- public void Exit(bool pressRelease)
- public void Digit0(bool pressRelease)
- public void Digit1(bool pressRelease)
- public void Digit2(bool pressRelease)
- public void Digit3(bool pressRelease)
- public void Digit4(bool pressRelease)
- public void Digit5(bool pressRelease)
- public void Digit6(bool pressRelease)
- public void Digit7(bool pressRelease)
- public void Digit8(bool pressRelease)
- public void Digit9(bool pressRelease)
- public void KeypadAccessoryButton1(bool pressRelease)
- public void KeypadAccessoryButton2(bool pressRelease)
- public void Dispatch()
- public void Dispatch()
- public bool CheckForDeviceMessenger(string key)
- public void AddDeviceMessenger(IMobileControlMessenger messenger)
- public void AddDeviceMessenger(MessengerBase messenger)
- public void LinkSystemMonitorToAppServer()
- public void CreateMobileControlRoomBridge(IEssentialsRoom room, IMobileControl parent)
- public void PrintActionDictionaryPaths(object o)
- public void RemoveAction(string key)
- public MobileControlBridgeBase GetRoomBridge(string key)
- public IMobileControlRoomMessenger GetRoomMessenger(string key)
- public void RegisterSystemToServer()
- public MobileControlEssentialsConfig GetConfigWithPluginVersion()
- public void SetClientUrl(string path, string roomKey = null)
- public void SendMessageObject(IMobileControlMessage o)
- public void SendMessageObjectToDirectClient(object o)
- public void HandleClientMessage(string message)
- public void RegisterWithAppServer(IMobileControl appServerController)
- public void RegisterWithAppServer(MobileControlSystemController appServerController)
- public void SetInterfaces(List<string> interfaces)
- public void RegisterForDestinationPaths()
- public void SendFullStatus()
- public void CustomUnregsiterWithAppServer(IMobileControl appServerController)
- public void CustomUnregsiterWithAppServer(MobileControlSystemController appServerController)
- public void CustomUnregsiterWithAppServer(IMobileControl appServerController)
- public void CustomUnregsiterWithAppServer(MobileControlSystemController appServerController)
- public void SetUserCode(string code)
- public void SetUserCode(string code, string qrChecksum)
<!-- END Public Methods -->
<!-- START Bool Feedbacks -->
### Bool Feedbacks

- AppOpenFeedback
- ZoomIncomingCallFeedback
- ZoomInCallFeedback
- ApiOnlineAndAuthorized
<!-- END Bool Feedbacks -->
<!-- START Int Feedbacks -->

<!-- END Int Feedbacks -->
<!-- START String Feedbacks -->
### String Feedbacks

- AppUrlFeedback
- ThemeFeedback
<!-- END String Feedbacks -->
