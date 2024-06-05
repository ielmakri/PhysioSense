# DataManager Application 

This document provides an overview of the DataManager Application, a Windows Forms application that allows users to subscribe to LSL data streams, display plots of the streams, and record the data.

## Features

- List available data streams
- Subscribe to data streams
- Display plots of subscribed data streams
- Record data streams to disk
- Manage data stream recording sessions

## Issues and Todos:
- TODO: Record data in segments to prevent data loss in case of a power outage or software failure
- TODO: Replay recorded data feature can be added.
- TODO: When data received is NaN , record it as NaN too. In the current implementation, it is plotted & recorded as 0. 
- TODO: Add indication which sensor is actively streaming (green light) and which does not (red?)
- TODO: Average sample time / frequency display feature

## How to Use

### List available data streams

1. Launch the application.
2. Click on the **List Streams** button to display available data streams.
3. The available streams will be shown in the **Streams** list.

### Subscribe to data streams

1. Check the box next to the desired data stream in the **Streams** list.
2. Click on the **Subscribe** button to subscribe to the selected data streams.

### Display plots of subscribed data streams

1. Double-click on the desired data stream in the **Streams** list.
2. A plot window will open, displaying the data stream in real-time.

### Record data streams to disk

1. Set up the recording session:
   - Click on the **Data Path** button and select the desired folder for data storage.
   - Enter a project name, experiment name, session name, and subject name in the corresponding textboxes.
   - Press **Enter** after each entry and click on the **Confirm** button.
2. Click on the **Record** button to start recording the data.
3. Click on the **Record** button again to stop recording.

### Manage data stream recording sessions

1. Use the **Project**, **Experiment**, **Session**, and **Subject** textboxes to enter the details of the recording session.
2. Press **Enter** after each entry to register the session details.
3. When all session details have been entered, click on the **Confirm** button.

## Debugging

- Click on the **Debug ⏬** label to show the debug area for displaying messages related to application events, errors, and warnings.
- Click on the **Debug ⏫** label to hide the debug area.

## Additional Features

### Multiplotting data streams

1. After subscribing to data streams, you can view multiple streams on a single plot.
2. Check the boxes next to the desired data streams in the **Multiplot Streams** list.
3. Click on the **Multiplot** button to display a plot window with the selected data streams.

### Enable or disable UDP stream

1. After subscribing to data streams, you can enable or disable the UDP stream feature.
2. Check or uncheck the **Enable UDP Stream** checkbox to enable or disable the feature accordingly.

## Troubleshooting

- If the application fails to list available data streams, ensure that your LSL-enabled devices are connected and streaming data.
- If data streams are not being plotted, double-check that you have subscribed to the desired streams.
- If data streams are not being recorded, ensure that you have provided valid session details and confirmed the recording session.

## Application Limitations

- The application is designed to work with LSL data streams and may not be compatible with other data streaming protocols.
- Data stream plotting and recording performance may vary depending on system resources and the number of active data streams.
- Loss of data streams during recording may occur due to connection issues or device malfunctions. Monitor the debug area for warnings or errors.



