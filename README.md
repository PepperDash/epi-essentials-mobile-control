# Mobile Control Essentials Plugin

This plugin enables Essentials to communicate with the Mobile Control User App.

This happens via two methods:
 * An Edge Server running the Mobile Control API stack to enable user provided mobile devices to choose and control rooms using either the QR code or numeric user code mechanism
 * If using a 4-series processor, the processor can run a websocket server that allows direct communication from dedicated in-room devices.

 Both methods are optionally configurable and can operate independently or in parallel.

 A user gets served the Mobile Control User App to their browser via HTTP and subsequently a websocket connection is made to the plugin to allow serial communcation for realtime system control and feedback.

## Download Links for Dependencies

[PepperDash Essentials - Releases](https://github.com/PepperDash/Essentials/releases) v1.10.0 minimum

[PepperDash Mobile Control App - Releases](https://github.com/PepperDash-Products/mobile-control-app/releases) v3.0.1 minimum

## Direct Connection to 4-Series Processor with or without Edge Server

The steps below detail how to load Mobile Control on a 4-Series processor running a websocket server to be served to local devices like an in-room touchpanel or a dedicated room PC or tablet.

An Edge Server can still optionally be used for mobile 

## Folder Structure

```plaintext
Processor
├── html
├── program[xx] // where xx is the two digit slot number
│   └── PepperDashEssentials-X.YY.Z.cpz
└── user
    └── program[x] // where x is the slot number
        ├── configurationFile.json
        ├── ir
        ├── mcUserApp // optional folder where user app is served from
        │   ├── _local-config
        │   │   └── _config.local.json
        │   ├── {Contents of mobile-control-app-directServer-vX.Y.Z.zip file}
        ├── plugins // plugin must be loaded to this folder
        │   └── epi-essentials-mobile-control-4s-X.Y.Z.cplz // the 4s version is for 4-Series and is required for direct websocket commmunications to work.
        └── sgd
```

## Configuration Snippets

### _config.local.json
This file is loaded to the processor and served up to the client device as part of the Mobile Control app.  It contains the information to configure the app and to connect to the processor via websocket.

```json
{
    "apiPath":"http://{processor-ip}:{50000 + Essentials Slot #}/mc/api",  // This value must be set to the IP of the processor and the port configured for the websocket
    "gatewayAppPath":"https://{processor-ip}:{50000 + Essentials Slot #}/mc/gateway", // Not used in direct connection scenarios
    "enableDev": true,
    "logoPath":"logo/logo.png",
    "iconSet": "GOOGLE", // Set the icon set to be used.  Valid values are "GOOGLE", "HABANERO" or "NEO
    "loginMode": "room-list",
    "modes": {
        "room-list": {
            "listPageText": "Please select your room",
            "loginHelpText": "Please select your room from the list, then enter the code shown on the display in the room. (Configurable message)",
            "passcodePageText": "Please enter the code shown on this room's display"
        }
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
    "uid": 25,
    "id": "c9138b74-ae67-4837-89ee-e1fe91e0f0d8",
    "properties": {
        "clientAppUrl": "http://{server-hostname}/mc/gateway", // url of the gateway app on an Edge Server
        "serverUrl": "http://{server-hostname}/mc/api", // url of the MC API on an Edge Server
        "enableApiServer": false, // set to true to enable communication with an Edge Server
        "directServer": { // Optional object to configure for direct communication
            "enableDirectServer": true, // set to true to enable direct communication to the plugins websocket server
            "port": 50001 // Optional custom port number for the websocket communication.  If not specified, default port will be 50000 + the program slot number
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

```



The `room-key` value must match the key of a room defined in the `rooms` array of the running Essentials configuration file.

Each connection requires a unique `Token` that serves to differentiate between unique user interfaces, allowing selective updates and unique user experience sessions on unique devices.

### Get Mobile Control Info

The following commands gets the current mobile control info.  The `Client URL` is needed for the Crestron TS(W) General Web URL field to run on a TS(W) panel directly.

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
## Setting up a TSXX70 Panel Using Console Commands

### Crestron TS(W)-x70

Set the `appmode` to `generalweb`

```plaintext
appmode generalweb
```

Restart the app

```plaintext
apprestart
```

Set the generalweb URL
setserialjoin 28535 {url}

```plaintext
// example set
setserialjoin 28535 http://10.0.0.223:50001/mc/app?token=81c4eb3c-dbc5-410c-8816-90500f474236
Get the generalweb URL
getserialjoin 28535
```

```plaintext
// example return
SERIAL=http://10.0.0.223:50001/mc/app?token=81c4eb3c-dbc5-410c-8816-90500f474236
```


