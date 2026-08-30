using Microsoft.Extensions.Configuration;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Quality_Vision.Api.Services;
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
        private readonly AppSettings _settings;
        private int _measurementMissFrames = 0;

        private readonly StationApiHost _stationApiHost;

        public MainWindow()
        {
            InitializeComponent();

            IConfiguration configuration =
                new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile(
                        "appsettings.json",
                        optional: false,
                        reloadOnChange: true)
                    .Build();

            _settings =
                configuration.Get<AppSettings>()
                ?? throw new InvalidOperationException(
                    "Could not load appsettings.json.");

            _cameraService = new CameraService();
            _visionService = new VisionService(
                _settings.ArUco,
                _settings.Detection,
                _settings.Calibration
            );
            _stationApiHost =
                new StationApiHost(
                    _settings,
                    _cameraService,
                    _visionService
                );

            int previewFps =
                Math.Max(1, _settings.Vision.PreviewFps);

            _cameraTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(
                    1000.0 / previewFps)
            };

            _cameraTimer.Tick += CameraTimer_Tick;

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private async void MainWindow_Loaded(
    object sender,
    System.Windows.RoutedEventArgs e)
        {
            StationNameText.Text =
                _settings.Station.StationName;

            bool started =
                _cameraService.Start(
                    _settings.Camera);

            if (!started)
            {
                CameraStatusText.Text = "● Disconnected";

                CameraStatusText.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(
                            215,
                            18,
                            19));

                SystemStatusText.Text =
                    "● System not ready";

                SystemStatusText.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(
                            215,
                            18,
                            19));

                return;
            }

            CameraStatusText.Text =
                "● Connected";

            CameraStatusText.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(
                        34,
                        197,
                        94));

            SystemStatusText.Text =
                "● System not ready";

            SystemStatusText.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(
                        215,
                        18,
                        19));

            await _stationApiHost.StartAsync();

            _cameraTimer.Start();
        }

        private void DrawMeasurementArea(
    Mat frame,
    MarkerDetectionResult detection)
        {
            int[] requiredIds =
            {
                _settings.ArUco.TopLeftMarkerId,
                _settings.ArUco.TopRightMarkerId,
                _settings.ArUco.BottomRightMarkerId,
                _settings.ArUco.BottomLeftMarkerId
            };

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
                innerCorners[_settings.ArUco.TopLeftMarkerId],
                innerCorners[_settings.ArUco.TopRightMarkerId],
                innerCorners[_settings.ArUco.BottomRightMarkerId],
                innerCorners[_settings.ArUco.BottomLeftMarkerId]
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
                RotatedRect? objectRectangle =
                    _visionService.DetectObject(rectified);

                if (objectRectangle.HasValue)
                {
                    // We successfully detected an object in this frame,
                    // so reset the counter that tracks failed detection frames.
                    _measurementMissFrames = 0;

                    // Get the detected rotated rectangle.
                    // Unlike a normal Rect, this rectangle follows the angle of the object.
                    RotatedRect rectangle =
                        objectRectangle.Value;


                    // Calculate how many millimeters each pixel represents
                    // in the rectified image.
                    double pixelsPerMm =
                        _settings.Calibration.RectifiedPixelsPerMm;

                    double mmPerPixel =
                        1.0 / pixelsPerMm;


                    // Get the two detected sides of the rotated rectangle in pixels.
                    double side1Pixels =
                        rectangle.Size.Width;

                    double side2Pixels =
                        rectangle.Size.Height;


                    // MinAreaRect can swap Width and Height depending on rotation.
                    // For our material:
                    // X = longest side
                    // Y = shortest side
                    double objectWidthPixels =
                        Math.Max(side1Pixels, side2Pixels);

                    double objectHeightPixels =
                        Math.Min(side1Pixels, side2Pixels);


                    // Convert the detected pixel dimensions into millimeters.
                    double objectWidthMm =
                        objectWidthPixels * mmPerPixel;

                    double objectHeightMm =
                        objectHeightPixels * mmPerPixel;


                    // Update the live measurement values in the WPF UI.
                    XMeasurementText.Text =
                        $"{objectWidthMm:F1} mm";

                    YMeasurementText.Text =
                        $"{objectHeightMm:F1} mm";


                    // Get the four corner points of the rotated rectangle.
                    Point2f[] boxPoints =
                        rectangle.Points();


                    // Convert the floating-point OpenCV coordinates
                    // into integer pixel coordinates for drawing.
                    OpenCvSharp.Point[] drawPoints =
                        boxPoints
                            .Select(p =>
                                new OpenCvSharp.Point(
                                    (int)p.X,
                                    (int)p.Y))
                            .ToArray();


                    // Draw the rotated green outline around the detected object.
                    Cv2.Polylines(
                        rectified,
                        new[] { drawPoints },
                        true,
                        Scalar.LimeGreen,
                        3
                    );


                    // Use the center of the detected object as the reference
                    // point for positioning the X/Y text.
                    int textX =
                        (int)rectangle.Center.X;

                    int textY =
                        (int)rectangle.Center.Y;


                    // Draw the X measurement inside the processed image.
                    Cv2.PutText(
                        rectified,
                        $"X: {objectWidthMm:F1} mm",
                        new OpenCvSharp.Point(
                            Math.Max(textX - 100, 10),
                            Math.Max(textY - 25, 25)
                        ),
                        HersheyFonts.HersheySimplex,
                        0.7,
                        Scalar.LimeGreen,
                        2
                    );


                    // Draw the Y measurement underneath the X measurement.
                    Cv2.PutText(
                        rectified,
                        $"Y: {objectHeightMm:F1} mm",
                        new OpenCvSharp.Point(
                            Math.Max(textX - 100, 10),
                            Math.Max(textY, 50)
                        ),
                        HersheyFonts.HersheySimplex,
                        0.7,
                        Scalar.LimeGreen,
                        2
                    );
                }
                else
                {
                    _measurementMissFrames++;

                    if (_measurementMissFrames >=
                        _settings.Detection.MeasurementMissToleranceFrames)
                    {
                        XMeasurementText.Text = "--- mm";
                        YMeasurementText.Text = "--- mm";
                    }
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
            int[] requiredMarkerIds =
            {
                _settings.ArUco.TopLeftMarkerId,
                _settings.ArUco.TopRightMarkerId,
                _settings.ArUco.BottomRightMarkerId,
                _settings.ArUco.BottomLeftMarkerId
            };

            int markerCount = detection.Ids
                .Distinct()
                .Count(id => requiredMarkerIds.Contains(id));

            MarkerStatusText.Text =
                $"{markerCount} / {requiredMarkerIds.Length}";

            // Update system status
            if (markerCount == requiredMarkerIds.Length)
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

        private async void MainWindow_Closed(
            object? sender,
            System.EventArgs e)
        {
            _cameraTimer.Stop();

            await _stationApiHost.StopAsync();

            _visionService.Dispose();
            _cameraService.Dispose();
        }
    }
}