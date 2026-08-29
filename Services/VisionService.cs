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

        public VisionService()
        {
            _dictionary = CvAruco.GetPredefinedDictionary(
                PredefinedDictionaryType.Dict4X4_50
            );

            _detector = new ArucoDetector(_dictionary);
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

        public Mat? CreateRectifiedMeasurementArea(
    Mat frame,
    MarkerDetectionResult detection)
        {
            int[] requiredIds = { 1, 2, 3, 4 };

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

            Point2f topLeft = innerCorners[1];
            Point2f topRight = innerCorners[2];
            Point2f bottomRight = innerCorners[3];
            Point2f bottomLeft = innerCorners[4];

            // Calculate approximate dimensions in pixels
            double topWidth = Distance(topLeft, topRight);
            double bottomWidth = Distance(bottomLeft, bottomRight);

            double leftHeight = Distance(topLeft, bottomLeft);
            double rightHeight = Distance(topRight, bottomRight);

            int outputWidth = (int)Math.Max(topWidth, bottomWidth);
            int outputHeight = (int)Math.Max(leftHeight, rightHeight);

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

        public OpenCvSharp.Rect? DetectObject(Mat rectified)
        {
            if (rectified.Empty())
            {
                return null;
            }

            using Mat gray = new Mat();
            using Mat blurred = new Mat();
            using Mat edges = new Mat();

            // Convert to grayscale
            Cv2.CvtColor(
                rectified,
                gray,
                ColorConversionCodes.BGR2GRAY
            );

            // Remove some small image noise
            Cv2.GaussianBlur(
                gray,
                blurred,
                new OpenCvSharp.Size(5, 5),
                0
            );

            // Find strong edges
            Cv2.Canny(
                blurred,
                edges,
                50,
                150
            );

            // Find external contours
            Cv2.FindContours(
                edges,
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

            OpenCvSharp.Rect? bestRectangle = null;
            double bestArea = 0;

            foreach (OpenCvSharp.Point[] contour in contours)
            {
                double contourArea = Cv2.ContourArea(contour);

                // Ignore very small things/noise
                if (contourArea < imageArea * 0.01)
                {
                    continue;
                }

                // Ignore something that basically fills the whole image
                if (contourArea > imageArea * 0.95)
                {
                    continue;
                }

                OpenCvSharp.Rect rectangle =
                    Cv2.BoundingRect(contour);

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