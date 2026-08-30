using OpenCvSharp;
using Quality_Vision.Models;

namespace Quality_Vision.Services
{
    /// <summary>
    /// Coordinates a complete measurement by capturing a camera frame,
    /// detecting the reference markers and object, and calculating
    /// the object's physical X and Y dimensions.
    /// </summary>
    public class MeasurementService
    {
        private readonly CameraService _cameraService;
        private readonly VisionService _visionService;
        private readonly AppSettings _settings;

        // Prevent two actual measurements from running
        // at the same time.
        private readonly SemaphoreSlim _measurementLock = new(1, 1);

        /// <summary>
        /// Initializes the measurement service with the camera,
        /// vision processing service and application settings.
        /// </summary>
        public MeasurementService(
            CameraService cameraService,
            VisionService visionService,
            AppSettings settings)
        {
            _cameraService = cameraService;
            _visionService = visionService;
            _settings = settings;
        }

        /// <summary>
        /// Performs one complete measurement and returns the result.
        /// Only one measurement can run at a time.
        /// </summary>
        public async Task<MeasurementResult> MeasureAsync(Guid measurementId)
        {
            await _measurementLock.WaitAsync();

            try
            {
                // -----------------------------------
                // CAPTURE ONE CAMERA FRAME
                // -----------------------------------

                using Mat? frame = _cameraService.GetFrame();

                if (frame == null)
                {
                    return CreateFailureResult( measurementId,"Could not capture camera frame.");
                }


                // -----------------------------------
                // DETECT REFERENCE MARKERS
                // -----------------------------------

                MarkerDetectionResult detection = _visionService.DetectMarkers(frame);

                int[] requiredMarkerIds =
                {
                    _settings.ArUco.TopLeftMarkerId,
                    _settings.ArUco.TopRightMarkerId,
                    _settings.ArUco.BottomRightMarkerId,
                    _settings.ArUco.BottomLeftMarkerId
                };

                bool allMarkersFound =
                    requiredMarkerIds.All(
                        id =>
                            detection.Ids.Contains(id)
                    );

                if (!allMarkersFound)
                {
                    return CreateFailureResult(measurementId,"All reference markers were not detected.");
                }


                // -----------------------------------
                // CREATE RECTIFIED MEASUREMENT AREA
                // -----------------------------------

                using Mat? rectified =
                    _visionService
                        .CreateRectifiedMeasurementArea(
                            frame,
                            detection
                        );

                if (rectified == null)
                {
                    return CreateFailureResult(measurementId, "Could not create rectified measurement area.");
                }


                // -----------------------------------
                // DETECT OBJECT
                // -----------------------------------

                RotatedRect? objectRectangle = _visionService.DetectObject(rectified);

                if (!objectRectangle.HasValue)
                {
                    return CreateFailureResult(measurementId,"No measurable object was detected.");
                }


                // -----------------------------------
                // CALCULATE OBJECT DIMENSIONS
                // -----------------------------------

                RotatedRect rectangle = objectRectangle.Value;

                double side1Pixels = rectangle.Size.Width;

                double side2Pixels = rectangle.Size.Height;

                // X = longest material side
                // Y = shortest material side
                double xPixels = Math.Max(side1Pixels, side2Pixels);

                double yPixels = Math.Min(side1Pixels, side2Pixels);

                // -----------------------------------
                // CONVERT PIXELS TO MILLIMETERS
                // -----------------------------------

                double pixelsPerMm = _settings.Calibration.RectifiedPixelsPerMm;

                if (pixelsPerMm <= 0)
                {
                    return CreateFailureResult(measurementId, "Invalid RectifiedPixelsPerMm configuration.");
                }

                double mmPerPixel = 1.0 / pixelsPerMm;

                double xMm = xPixels * mmPerPixel;

                double yMm = yPixels * mmPerPixel;


                // -----------------------------------
                // SUCCESSFUL RESULT
                // -----------------------------------

                return new MeasurementResult
                {
                    MeasurementId = measurementId,

                    StationId = _settings.Station.StationId,

                    Success = true,

                    X = xMm,
                    Y = yMm,

                    Message = "Measurement successful.",

                    Timestamp = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                return CreateFailureResult( measurementId, $"Measurement failed: {ex.Message}");
            }
            finally
            {
                _measurementLock.Release();
            }
        }

        /// <summary>
        /// Creates a failed measurement result for the current station
        /// using the supplied error message.
        /// </summary>
        private MeasurementResult CreateFailureResult(Guid measurementId, string message)
        {
            return new MeasurementResult
            {
                MeasurementId = measurementId,

                StationId = _settings.Station.StationId,

                Success = false,

                X = null,
                Y = null,

                Message = message,

                Timestamp = DateTime.Now
            };
        }
    }
}