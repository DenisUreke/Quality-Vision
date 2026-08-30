using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Quality_Vision.Models;
using Quality_Vision.Services;

namespace Quality_Vision.Api.Services
{
    public class StationApiHost : IAsyncDisposable
    {
        private WebApplication? _app;

        private readonly AppSettings _settings;
        private readonly CameraService _cameraService;
        private readonly VisionService _visionService;

        public StationApiHost(
            AppSettings settings,
            CameraService cameraService,
            VisionService visionService)
        {
            _settings = settings;
            _cameraService = cameraService;
            _visionService = visionService;
        }

        public async Task StartAsync()
        {
            if (_app != null)
            {
                return;
            }

            WebApplicationBuilder builder =
                WebApplication.CreateBuilder();

            builder.WebHost.UseUrls(
                _settings.StationApi.Url
            );

            // Enables controller discovery.
            builder.Services.AddControllers();

            // Reuse the same settings instance
            // already loaded by the WPF application.
            builder.Services.AddSingleton(
                _settings
            );

            // Reuse the same physical camera instance.
            builder.Services.AddSingleton(
                _cameraService
            );

            // Reuse the same vision service.
            builder.Services.AddSingleton(
                _visionService
            );

            // Performs one complete measurement.
            builder.Services.AddSingleton<
                MeasurementService>();

            // Sends completed measurements to Insight.
            builder.Services.AddSingleton<
                InsightApiService>();

            _app = builder.Build();

            _app.MapControllers();

            await _app.StartAsync();
        }

        public async Task StopAsync()
        {
            if (_app == null)
            {
                return;
            }

            await _app.StopAsync();
            await _app.DisposeAsync();

            _app = null;
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }
}