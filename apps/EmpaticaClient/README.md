## Description
Reads stream from Empatica software and creates an LSL outlet with the data.

---
## Issues & Todos: 
> **Warning**: Empatica E4 will not work after Mart 2024.

---
## Data Stream 

Sources:
- https://www.empatica.com/blog/decoding-wearable-sensor-signals-what-to-expect-from-your-e4-data.html
- https://developer.empatica.com/windows-streaming-server-data.html
- BVP: https://support.empatica.com/hc/en-us/articles/360029719792-E4-data-BVP-expected-signal


### Timestamp
The timestamp for the sample in seconds defined as time interval between the sample received and the reference date, 1 January 1970, GMT. The value contains a fractional part to represent microseconds.

The sample timestamps are calculated with reference to the first packet received by the E4 streaming server. Upon reception of the first packet, the system timestamp is recorded and the sample timestamps of the first and any further packets are calculated from the reference timestamp and the sample frequency of the respective stream.
Since the E4 starts streaming once the BTLE connection is established, the E4 streaming server starts receiving packets even without any TCP clients subscribed to streams.

### Data
Each stream type has a different data format as described below:

#### Acceleration Data
The acceleration value for x axis. The x axis is defined by the vector whose starting point is set to the center of the device and whose direction points towards the USB slot.
The acceleration value for y axis. The y axis is defined by the vector whose starting point is set to the center of the device and whose direction points towards the shorter strap.
The acceleration value for z axis. The z axis is defined by the vector whose starting point is set to the center of the device and whose direction points towards the bottom of the device.
Example:
E4_Acc 123345627891.123 51 -2 -10

#### Blood Volume Pulse Data
The value of the BVP sample. The value is derived from the light absorbance of the arterial blood. The raw signal is filtered to remove movement artifacts.

Example:
E4_Bvp 123345627891.123 31.128


#### Galvanic Skin Response Data
The value of the GSR sample. The value is expressed in microsiemens.

Example:
E4_Gsr 123345627891.123 3.129

#### Temperature Data
The value of the temperature sample in Celsius degrees. The value is derived from the optical temperature sensor placed on the wrist.

Example:
E4_Temperature 123345627891.123 35.82

#### Interbeat Interval Data
The value of the IBI sample. The value is the distance from the previous detected heartbeat in seconds.

Example:
E4_Ibi 123345627891.123 0.822

#### Heartbeat Data
The value of the detected heartbeat, returned together with the interbeat interval data.

Example:
E4_Hr 123345627891.123 142.2156

#### Battery Level Data
The battery level of the device. Values: [0.0 - 1.0]

Example:
E4_Battery 123345627891.123 0.2

#### Tag Data
The tags taken from the device.

Example:
E4_Tag 123345627891.123

---
## User Guide

This user guide provides an overview of the EmpaticaClient software application and how to use it.

### Getting Started
1. Launch EmpaticaBLEServer application
2. Power up E4 Smartwatch and wait until it connects to EmpaticaBLEServer application
3. Launch the EmpaticaClient application.

### Application Controls
The EmpaticaClient application has several controls for managing data streaming and configuration:

#### Starting/stopping LSL data stream
On the LSL client application:
- Click the "Start" button to begin LSL data streaming.
- During streaming, the "Start" button will change to "Stop". Click it again to stop data streaming.

#### Settings Button
- Click the "Settings" button to open the configuration file (config.json) in Notepad.
- Edit the configuration file as needed and save your changes.
- Close Notepad to return to the EmpaticaClient application.

#### Debug Area
- Click the "Debug ⏬" label to expand or collapse the debug area.

#### Data Logging Checkbox
- Check the "Enable Log" checkbox to save the streamed data to a file when stopping the data streaming.

#### Status Information
During operation, the EmpaticaClient application displays status information:
- Elapsed Time: Shows the elapsed time since data streaming started.
- Data Packets: Shows the amount of data received in kilobytes (kB) or megabytes (MB).
- Status: Shows the current status of the application, including any critical events or errors.
