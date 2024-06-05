## Description

This is a C# Windows Forms application that captures video from a camera using the Emgu CV library and DirectShowLib. It allows the user to start/stop capturing video and record the video to a file.
It also creates LSL outlet for each captured camera frame number.

- TODO: Get FPS from camera, now default 30Hz is assumed , actual camera FPS is not used.
- TODO: Add compression for video records.

## Usage

1. Select the desired camera from the dropdown menu.
2. Click "Start" to begin capturing video from the selected camera.
3. Click "Record" to start recording the video to a file.
4. Click "Stop" to stop the video capture and recording.

