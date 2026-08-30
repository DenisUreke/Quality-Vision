using OpenCvSharp;
using Quality_Vision.Models;

namespace Quality_Vision.Services
{
    public class CameraService : IDisposable
    {
        private VideoCapture? _capture;

        // Protects access to the physical camera.
        // Only one thread may Start, Stop or Read the camera at a time.
        private readonly object _cameraLock = new();


        public bool IsConnected
        {
            get
            {
                lock (_cameraLock)
                {
                    return _capture != null &&
                           _capture.IsOpened();
                }
            }
        }


        public bool Start(CameraSettings settings)
        {
            lock (_cameraLock)
            {
                StopInternal();

                _capture =
                    new VideoCapture(
                        settings.CameraIndex
                    );

                if (!_capture.IsOpened())
                {
                    _capture.Dispose();
                    _capture = null;

                    return false;
                }


                // -----------------------------
                // CAMERA RESOLUTION
                // -----------------------------

                _capture.Set(
                    VideoCaptureProperties.FrameWidth,
                    settings.Width
                );

                _capture.Set(
                    VideoCaptureProperties.FrameHeight,
                    settings.Height
                );


                // -----------------------------
                // CAMERA FPS
                // -----------------------------

                _capture.Set(
                    VideoCaptureProperties.Fps,
                    settings.Fps
                );


                // -----------------------------
                // AUTOFOCUS
                // -----------------------------

                _capture.Set(
                    VideoCaptureProperties.AutoFocus,
                    settings.AutoFocus ? 1 : 0
                );

                // Manual focus is only used when
                // autofocus has been disabled.
                if (!settings.AutoFocus)
                {
                    _capture.Set(
                        VideoCaptureProperties.Focus,
                        settings.Focus
                    );
                }


                // -----------------------------
                // AUTO EXPOSURE
                // -----------------------------

                _capture.Set(
                    VideoCaptureProperties.AutoExposure,
                    settings.AutoExposure ? 1 : 0
                );

                // Manual exposure is only used
                // when auto exposure is disabled.
                if (!settings.AutoExposure)
                {
                    _capture.Set(
                        VideoCaptureProperties.Exposure,
                        settings.Exposure
                    );
                }


                // -----------------------------
                // GAIN
                // -----------------------------

                _capture.Set(
                    VideoCaptureProperties.Gain,
                    settings.Gain
                );


                return true;
            }
        }


        public Mat? GetFrame()
        {
            lock (_cameraLock)
            {
                if (_capture == null ||
                    !_capture.IsOpened())
                {
                    return null;
                }

                var frame = new Mat();

                bool success =
                    _capture.Read(frame);

                if (!success ||
                    frame.Empty())
                {
                    frame.Dispose();

                    return null;
                }

                return frame;
            }
        }


        public void Stop()
        {
            lock (_cameraLock)
            {
                StopInternal();
            }
        }


        // Internal version is used because Start()
        // already owns the camera lock.
        private void StopInternal()
        {
            if (_capture == null)
            {
                return;
            }

            _capture.Release();
            _capture.Dispose();
            _capture = null;
        }


        public void Dispose()
        {
            Stop();
        }
    }
}