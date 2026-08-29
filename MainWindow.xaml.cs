using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Quality_Vision.Models;
using Quality_Vision.Services;
using System.Linq;
using System.Windows.Threading;

namespace Quality_Vision
{
    public partial class MainWindow : System.Windows.Window
    {
        private readonly CameraService _cameraService;
        private readonly VisionService _visionService;
        private readonly DispatcherTimer _cameraTimer;

        public MainWindow()
        {
            InitializeComponent();

            _cameraService = new CameraService();
            _visionService = new VisionService();

            _cameraTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };

            _cameraTimer.Tick += CameraTimer_Tick;

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(
            object sender,
            System.Windows.RoutedEventArgs e)
        {
            bool started = _cameraService.Start(1);

            if (!started)
            {
                CameraStatusText.Text = "● Disconnected";
                CameraStatusText.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(215, 18, 19));

                SystemStatusText.Text = "● System not ready";
                SystemStatusText.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(215, 18, 19));

                return;
            }

            CameraStatusText.Text = "● Connected";
            CameraStatusText.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(34, 197, 94));

            SystemStatusText.Text = "● System not ready";
            SystemStatusText.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(215, 18, 19));

            _cameraTimer.Start();
        }

        private void DrawMeasurementArea(
    Mat frame,
    MarkerDetectionResult detection)
        {
            int[] requiredIds = { 1, 2, 3, 4 };

            // We need all four reference markers.
            if (!requiredIds.All(id => detection.Ids.Contains(id)))
            {
                return;
            }

            // Find the center point of each marker.
            var markerCenters = new Dictionary<int, Point2f>();

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

            // Find the center of the entire measurement area.
            Point2f areaCenter = new Point2f(
                markerCenters.Values.Average(p => p.X),
                markerCenters.Values.Average(p => p.Y)
            );

            var innerCorners = new Dictionary<int, OpenCvSharp.Point>();

            // For each marker, choose the corner closest to the area center.
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

                innerCorners[id] = new OpenCvSharp.Point(
                    (int)innerCorner.X,
                    (int)innerCorner.Y
                );
            }

            // Our physical marker order:
            // 1 = top-left
            // 2 = top-right
            // 3 = bottom-right
            // 4 = bottom-left

            OpenCvSharp.Point[] measurementRectangle =
            {
        innerCorners[1],
        innerCorners[2],
        innerCorners[3],
        innerCorners[4]
    };

            Cv2.Polylines(
                frame,
                new[] { measurementRectangle },
                true,
                Scalar.LimeGreen,
                3
            );
        }

        private void CameraTimer_Tick(
            object? sender,
            System.EventArgs e)
        {
            using Mat? frame = _cameraService.GetFrame();

            if (frame == null)
            {
                return;
            }

            // Detect ArUco markers
            MarkerDetectionResult detection =
                _visionService.DetectMarkers(frame);

            // Create perspective-corrected measurement area
            using Mat? rectified =
                _visionService.CreateRectifiedMeasurementArea(
                    frame,
                    detection
                );

            // Show rectified preview if all required markers were found
            if (rectified != null)
            {
                OpenCvSharp.Rect? objectRectangle =
                    _visionService.DetectObject(rectified);

                if (objectRectangle.HasValue)
                {
                    OpenCvSharp.Rect rectangle =
                        objectRectangle.Value;

                    // TEMPORARY calibration values.
                    // Replace these with the actual physical dimensions
                    // between the four inner ArUco corners.
                    const double measurementAreaWidthMm = 1200.0;
                    const double measurementAreaHeightMm = 700.0;

                    // Calculate how many millimeters each pixel represents.
                    double mmPerPixelX =
                        measurementAreaWidthMm / rectified.Width;

                    double mmPerPixelY =
                        measurementAreaHeightMm / rectified.Height;

                    // Convert detected object size from pixels to millimeters.
                    double objectWidthMm =
                        rectangle.Width * mmPerPixelX;

                    double objectHeightMm =
                        rectangle.Height * mmPerPixelY;

                    // Update UI.
                    XMeasurementText.Text =
                        $"{objectWidthMm:F1} mm";

                    YMeasurementText.Text =
                        $"{objectHeightMm:F1} mm";

                    // Draw detected object.
                    Cv2.Rectangle(
                        rectified,
                        rectangle,
                        Scalar.LimeGreen,
                        3
                    );

                    // Optional: show dimensions inside processed view.
                    Cv2.PutText(
                        rectified,
                        $"X: {objectWidthMm:F1} mm",
                        new OpenCvSharp.Point(
                            rectangle.X,
                            Math.Max(rectangle.Y - 35, 25)
                        ),
                        HersheyFonts.HersheySimplex,
                        0.7,
                        Scalar.LimeGreen,
                        2
                    );

                    Cv2.PutText(
                        rectified,
                        $"Y: {objectHeightMm:F1} mm",
                        new OpenCvSharp.Point(
                            rectangle.X,
                            Math.Max(rectangle.Y - 10, 50)
                        ),
                        HersheyFonts.HersheySimplex,
                        0.7,
                        Scalar.LimeGreen,
                        2
                    );
                }
                else
                {
                    XMeasurementText.Text = "--- mm";
                    YMeasurementText.Text = "--- mm";
                }

                RectifiedPreview.Source =
                    rectified.ToBitmapSource();
            }
            else
            {
                RectifiedPreview.Source = null;

                XMeasurementText.Text = "--- mm";
                YMeasurementText.Text = "--- mm";
            }

            // Count only reference markers 1, 2, 3 and 4
            int markerCount = detection.Ids
                .Distinct()
                .Count(id => id is 1 or 2 or 3 or 4);

            MarkerStatusText.Text = $"{markerCount} / 4";

            // Update system status
            if (markerCount == 4)
            {
                MarkerStatusText.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(
                            34,
                            197,
                            94
                        )
                    );

                SystemStatusText.Text = "● System ready";

                SystemStatusText.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(
                            34,
                            197,
                            94
                        )
                    );
            }
            else
            {
                MarkerStatusText.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(
                            215,
                            18,
                            19
                        )
                    );

                SystemStatusText.Text = "● System not ready";

                SystemStatusText.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(
                            215,
                            18,
                            19
                        )
                    );
            }

            // Draw debug overlays on the ORIGINAL camera image
            DrawDetectedMarkers(
                frame,
                detection
            );

            DrawMeasurementArea(
                frame,
                detection
            );

            // Show original camera preview
            CameraPreview.Source =
                frame.ToBitmapSource();
        }

        private void DrawDetectedMarkers(
            Mat frame,
            MarkerDetectionResult detection)
        {
            for (int i = 0; i < detection.Ids.Length; i++)
            {
                int id = detection.Ids[i];

                Point2f[] markerCorners = detection.Corners[i];

                OpenCvSharp.Point[] points = markerCorners
                    .Select(p =>
                        new OpenCvSharp.Point(
                            (int)p.X,
                            (int)p.Y))
                    .ToArray();

                Cv2.Polylines(
                    frame,
                    new[] { points },
                    true,
                    Scalar.LimeGreen,
                    2
                );

                foreach (OpenCvSharp.Point point in points)
                {
                    Cv2.Circle(
                        frame,
                        point,
                        4,
                        Scalar.LimeGreen,
                        -1
                    );
                }

                var labelPoint =
                    new OpenCvSharp.Point(
                        (int)markerCorners[0].X,
                        (int)markerCorners[0].Y - 10
                    );

                Cv2.PutText(
                    frame,
                    $"ID {id}",
                    labelPoint,
                    HersheyFonts.HersheySimplex,
                    0.8,
                    Scalar.LimeGreen,
                    2
                );
            }
        }

        private void MainWindow_Closed(
            object? sender,
            System.EventArgs e)
        {
            _cameraTimer.Stop();

            _visionService.Dispose();
            _cameraService.Dispose();
        }
    }
}