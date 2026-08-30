using OpenCvSharp;
using Quality_Vision.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quality_Vision.Services
{
    public class CameraService : IDisposable
    {
        private VideoCapture? _capture;

        public bool IsConnected =>
            _capture != null && _capture.IsOpened();

        public bool Start(CameraSettings settings)
        {
            Stop();

            _capture = new VideoCapture(settings.CameraIndex);

            if (!_capture.IsOpened())
            {
                _capture.Dispose();
                _capture = null;

                return false;
            }

            // Resolution
            _capture.Set(
                VideoCaptureProperties.FrameWidth,
                settings.Width);

            _capture.Set(
                VideoCaptureProperties.FrameHeight,
                settings.Height);

            // FPS
            _capture.Set(
                VideoCaptureProperties.Fps,
                settings.Fps);

            // Autofocus
            _capture.Set(
                VideoCaptureProperties.AutoFocus,
                settings.AutoFocus ? 1 : 0);

            // Manual focus
            if (!settings.AutoFocus)
            {
                _capture.Set(
                    VideoCaptureProperties.Focus,
                    settings.Focus);
            }

            // Auto exposure
            _capture.Set(
                VideoCaptureProperties.AutoExposure,
                settings.AutoExposure ? 1 : 0);

            // Manual exposure
            if (!settings.AutoExposure)
            {
                _capture.Set(
                    VideoCaptureProperties.Exposure,
                    settings.Exposure);
            }

            // Gain
            _capture.Set(
                VideoCaptureProperties.Gain,
                settings.Gain);

            return true;
        }

        public Mat? GetFrame()
        {
            if (_capture == null || !_capture.IsOpened())
            {
                return null;
            }

            var frame = new Mat();

            bool success = _capture.Read(frame);

            if (!success || frame.Empty())
            {
                frame.Dispose();
                return null;
            }

            return frame;
        }

        public void Stop()
        {
            if (_capture != null)
            {
                _capture.Release();
                _capture.Dispose();
                _capture = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
