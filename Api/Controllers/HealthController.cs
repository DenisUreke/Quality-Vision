using Microsoft.AspNetCore.Mvc;
using Quality_Vision.Models;
using Quality_Vision.Services;

namespace Quality_Vision.Api.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly CameraService _cameraService;
        private readonly AppSettings _settings;

        public HealthController(
            CameraService cameraService,
            AppSettings settings)
        {
            _cameraService = cameraService;
            _settings = settings;
        }

        [HttpGet]
        public IActionResult GetHealth()
        {
            return Ok(new
            {
                status = "ok",
                stationId = _settings.Station.StationId,
                stationName = _settings.Station.StationName,
                cameraConnected = _cameraService.IsConnected,
                timestamp = DateTime.Now
            });
        }
    }
}