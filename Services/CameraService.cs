using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace Quality_Vision.Services
{
    public class CameraService : IDisposable
    {
        private VideoCapture? _capture;

        public bool IsConnected =>
            _capture != null && _capture.IsOpened();

        public bool Start(int cameraIndex = 0)
        {
            Stop();

            _capture = new VideoCapture(cameraIndex);

            if (!_capture.IsOpened())
            {
                _capture.Dispose();
                _capture = null;

                return false;
            }

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
