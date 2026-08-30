using Microsoft.AspNetCore.Mvc;
using Quality_Vision.Api.Models;
using Quality_Vision.Api.Services;
using Quality_Vision.Models;
using Quality_Vision.Services;

namespace Quality_Vision.Api.Controllers
{
    [ApiController]
    [Route("api/vision")]
    public class VisionController : ControllerBase
    {
        private readonly MeasurementService _measurementService;
        private readonly InsightApiService _insightApiService;
        private readonly AppSettings _settings;

        public VisionController(
            MeasurementService measurementService,
            InsightApiService insightApiService,
            AppSettings settings)
        {
            _measurementService = measurementService;
            _insightApiService = insightApiService;
            _settings = settings;
        }

        [HttpPost("measure")]
        public IActionResult Measure()
        {
            Guid measurementId = Guid.NewGuid();

            _ = Task.Run(async () =>
            {
                MeasurementResult result =
                    await _measurementService.MeasureAsync(
                        measurementId
                    );

                await _insightApiService
                    .SendMeasurementAsync(
                        result
                    );
            });

            return Accepted(
                new MeasurementAcceptedResponse
                {
                    MeasurementId = measurementId,
                    StationId = _settings.Station.StationId,
                    Status = "Accepted"
                }
            );
        }
    }
}