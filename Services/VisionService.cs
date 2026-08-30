using OpenCvSharp;
using OpenCvSharp.Aruco;
using Quality_Vision.Models;
using System.Linq;

namespace Quality_Vision.Services
{
    public class VisionService : IDisposable
    {
        private readonly Dictionary _dictionary;
        private readonly ArucoDetector _detector;
        private readonly ArUcoSettings _arucoSettings;
        private readonly DetectionSettings _detectionSettings;
        private readonly CalibrationSettings _calibrationSettings;

        public VisionService(
            ArUcoSettings arucoSettings,
            DetectionSettings detectionSettings,
            CalibrationSettings calibrationSettings)
        {
            _arucoSettings = arucoSettings;
            _detectionSettings = detectionSettings;
            _calibrationSettings = calibrationSettings;

            PredefinedDictionaryType dictionaryType =
                GetDictionaryType(
                    _arucoSettings.Dictionary
                );

            _dictionary =
                CvAruco.GetPredefinedDictionary(
                    dictionaryType
                );

            _detector =
                new ArucoDetector(
                    _dictionary
                );
        }

        public MarkerDetectionResult DetectMarkers(Mat frame)
        {
            _detector.DetectMarkers(
                frame,
                out Point2f[][] corners,
                out int[] ids,
                out Point2f[][] rejectedPoints
            );

            return new MarkerDetectionResult
            {
                Ids = ids ?? Array.Empty<int>(),
                Corners = corners ?? Array.Empty<Point2f[]>()
            };
        }
        private static PredefinedDictionaryType GetDictionaryType(
    string dictionaryName)
        {
            return dictionaryName switch
            {
                "Dict4X4_50" =>
                    PredefinedDictionaryType.Dict4X4_50,

                "Dict4X4_100" =>
                    PredefinedDictionaryType.Dict4X4_100,

                "Dict4X4_250" =>
                    PredefinedDictionaryType.Dict4X4_250,

                "Dict4X4_1000" =>
                    PredefinedDictionaryType.Dict4X4_1000,

                "Dict5X5_50" =>
                    PredefinedDictionaryType.Dict5X5_50,

                "Dict5X5_100" =>
                    PredefinedDictionaryType.Dict5X5_100,

                "Dict5X5_250" =>
                    PredefinedDictionaryType.Dict5X5_250,

                "Dict5X5_1000" =>
                    PredefinedDictionaryType.Dict5X5_1000,

                "Dict6X6_50" =>
                    PredefinedDictionaryType.Dict6X6_50,

                "Dict6X6_100" =>
                    PredefinedDictionaryType.Dict6X6_100,

                "Dict6X6_250" =>
                    PredefinedDictionaryType.Dict6X6_250,

                "Dict6X6_1000" =>
                    PredefinedDictionaryType.Dict6X6_1000,

                _ =>
                    throw new InvalidOperationException(
                        $"Unsupported ArUco dictionary: {dictionaryName}"
                    )
            };
        }

        public Mat? CreateRectifiedMeasurementArea(
    Mat frame,
    MarkerDetectionResult detection)
        {
            int[] requiredIds =
            {
                _arucoSettings.TopLeftMarkerId,
                _arucoSettings.TopRightMarkerId,
                _arucoSettings.BottomRightMarkerId,
                _arucoSettings.BottomLeftMarkerId
            };

            if (!requiredIds.All(id => detection.Ids.Contains(id)))
            {
                return null;
            }

            var markerCenters = new Dictionary<int, Point2f>();

            // Find center of every reference marker
            for (int i = 0; i < detection.Ids.Length; i++)
            {
                int id = detection.Ids[i];

                if (!requiredIds.Contains(id))
                {
                    continue;
                }

                Point2f[] corners = detection.Corners[i];

                markerCenters[id] = new Point2f(
                    corners.Average(p => p.X),
                    corners.Average(p => p.Y)
                );
            }

            // Center of the complete measurement area
            Point2f areaCenter = new Point2f(
                markerCenters.Values.Average(p => p.X),
                markerCenters.Values.Average(p => p.Y)
            );

            var innerCorners = new Dictionary<int, Point2f>();

            // Select the corner of each marker closest to the center
            for (int i = 0; i < detection.Ids.Length; i++)
            {
                int id = detection.Ids[i];

                if (!requiredIds.Contains(id))
                {
                    continue;
                }

                Point2f[] corners = detection.Corners[i];

                Point2f innerCorner = corners
                    .OrderBy(p =>
                        Math.Pow(p.X - areaCenter.X, 2) +
                        Math.Pow(p.Y - areaCenter.Y, 2))
                    .First();

                innerCorners[id] = innerCorner;
            }

            Point2f topLeft =
                innerCorners[_arucoSettings.TopLeftMarkerId];

            Point2f topRight =
                innerCorners[_arucoSettings.TopRightMarkerId];

            Point2f bottomRight =
                innerCorners[_arucoSettings.BottomRightMarkerId];

            Point2f bottomLeft =
                innerCorners[_arucoSettings.BottomLeftMarkerId];

            // Calculate approximate dimensions in pixels
            double pixelsPerMm =
                _calibrationSettings.RectifiedPixelsPerMm;

            if (pixelsPerMm <= 0)
            {
                return null;
            }

            int outputWidth =
                (int)Math.Round(
                    _calibrationSettings.MeasurementAreaWidthMm *
                    pixelsPerMm
                );

            int outputHeight =
                (int)Math.Round(
                    _calibrationSettings.MeasurementAreaHeightMm *
                    pixelsPerMm
                );

            if (outputWidth <= 0 || outputHeight <= 0)
            {
                return null;
            }

            Point2f[] sourcePoints =
            {
        topLeft,
        topRight,
        bottomRight,
        bottomLeft
    };

            Point2f[] destinationPoints =
            {
        new Point2f(0, 0),
        new Point2f(outputWidth - 1, 0),
        new Point2f(outputWidth - 1, outputHeight - 1),
        new Point2f(0, outputHeight - 1)
    };

            using Mat transformation =
                Cv2.GetPerspectiveTransform(
                    sourcePoints,
                    destinationPoints
                );

            var rectified = new Mat();

            Cv2.WarpPerspective(
                frame,
                rectified,
                transformation,
                new OpenCvSharp.Size(outputWidth, outputHeight)
            );

            return rectified;
        }

        public RotatedRect? DetectObject(Mat rectified)
        {
            if (rectified.Empty())
            {
                return null;
            }

            using Mat gray = new Mat();
            using Mat blurred = new Mat();
            using Mat edges = new Mat();
            using Mat closedEdges = new Mat();

            // Convert to grayscale.
            Cv2.CvtColor(
                rectified,
                gray,
                ColorConversionCodes.BGR2GRAY
            );

            // Gaussian blur size must be odd.
            int blurSize =
                Math.Max(
                    1,
                    _detectionSettings.GaussianBlurSize
                );

            if (blurSize % 2 == 0)
            {
                blurSize++;
            }

            Cv2.GaussianBlur(
                gray,
                blurred,
                new OpenCvSharp.Size(
                    blurSize,
                    blurSize
                ),
                0
            );

            // Detect object edges.
            Cv2.Canny(
                blurred,
                edges,
                _detectionSettings.CannyThreshold1,
                _detectionSettings.CannyThreshold2
            );

            // ------------------------------------------------
            // CLOSE SMALL GAPS IN THE DETECTED OBJECT EDGES
            // ------------------------------------------------

            int kernelSize =
                Math.Max(
                    1,
                    _detectionSettings.MorphologyKernelSize
                );

            if (kernelSize % 2 == 0)
            {
                kernelSize++;
            }

            using Mat kernel =
                Cv2.GetStructuringElement(
                    MorphShapes.Rect,
                    new OpenCvSharp.Size(
                        kernelSize,
                        kernelSize
                    )
                );

            Cv2.MorphologyEx(
                edges,
                closedEdges,
                MorphTypes.Close,
                kernel,
                iterations:
                    Math.Max(
                        1,
                        _detectionSettings.MorphologyIterations
                    )
            );

            // ------------------------------------------------
            // FIND OBJECT CONTOURS
            // ------------------------------------------------

            Cv2.FindContours(
                closedEdges,
                out OpenCvSharp.Point[][] contours,
                out HierarchyIndex[] hierarchy,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple
            );

            if (contours.Length == 0)
            {
                return null;
            }

            double imageArea =
                rectified.Width * rectified.Height;

            RotatedRect? bestRectangle = null;

            double bestArea = 0;

            foreach (OpenCvSharp.Point[] contour in contours)
            {
                double contourArea =
                    Cv2.ContourArea(contour);

                if (contourArea <
                    imageArea *
                    _detectionSettings.MinimumObjectAreaPercent)
                {
                    continue;
                }

                if (contourArea >
                    imageArea *
                    _detectionSettings.MaximumObjectAreaPercent)
                {
                    continue;
                }

                RotatedRect rectangle =
                    Cv2.MinAreaRect(contour);

                if (contourArea > bestArea)
                {
                    bestArea = contourArea;
                    bestRectangle = rectangle;
                }
            }

            return bestRectangle;
        }

        private static double Distance(Point2f a, Point2f b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        public void Dispose()
        {
            _detector.Dispose();
            _dictionary.Dispose();
        }
    }
}