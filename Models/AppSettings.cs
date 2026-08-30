using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quality_Vision.Models
{
    public class AppSettings
    {
        public StationSettings Station { get; set; } = new();
        public CameraSettings Camera { get; set; } = new();
        public ArUcoSettings ArUco { get; set; } = new();
        public CalibrationSettings Calibration { get; set; } = new();
        public DetectionSettings Detection { get; set; } = new();
        public VisionSettings Vision { get; set; } = new();
        public InsightApiSettings InsightApi { get; set; } = new();
        public StationApiSettings StationApi { get; set; } = new();
    }

    public class StationSettings
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
    }

    public class CameraSettings
    {
        public string Type { get; set; } = "OpenCv";

        public int CameraIndex { get; set; }

        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public int Fps { get; set; } = 30;

        public bool AutoFocus { get; set; } = true;
        public double Focus { get; set; }

        public bool AutoExposure { get; set; } = true;
        public double Exposure { get; set; } = -6;

        public double Gain { get; set; }
    }

    public class ArUcoSettings
    {
        public string Dictionary { get; set; } = "Dict4X4_50";

        public int TopLeftMarkerId { get; set; } = 1;
        public int TopRightMarkerId { get; set; } = 2;
        public int BottomRightMarkerId { get; set; } = 3;
        public int BottomLeftMarkerId { get; set; } = 4;
    }

    public class CalibrationSettings
    {
        public double MeasurementAreaWidthMm { get; set; }
        public double MeasurementAreaHeightMm { get; set; }

        public double RectifiedPixelsPerMm { get; set; } = 1.0;

        public double CameraDistanceMm { get; set; }
        public double MaterialThicknessMm { get; set; } = 16.0;
    }

    public class DetectionSettings
    {
        public double CannyThreshold1 { get; set; } = 50;
        public double CannyThreshold2 { get; set; } = 150;
        public int GaussianBlurSize { get; set; } = 5;

        public double MinimumObjectAreaPercent { get; set; } = 0.01;
        public double MaximumObjectAreaPercent { get; set; } = 0.95;

        public int MorphologyKernelSize { get; set; } = 5;
        public int MorphologyIterations { get; set; } = 2;

        public int MeasurementMissToleranceFrames { get; set; } = 10;
    }

    public class VisionSettings
    {
        public int PreviewFps { get; set; } = 30;
    }

    public class InsightApiSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string MeasurementEndpoint { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 10;
    }

    public class StationApiSettings
    {
        public string Url { get; set; } =
            "http://0.0.0.0:5000";
    }
}
