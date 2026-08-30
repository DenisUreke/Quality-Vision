using Quality_Vision.Api.Models;
using Quality_Vision.Models;
using System.Net.Http;
using System.Net.Http.Json;

namespace Quality_Vision.Api.Services
{
    public class InsightApiService
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _settings;

        public InsightApiService(
            AppSettings settings)
        {
            _settings = settings;

            _httpClient = new HttpClient
            {
                BaseAddress =
                    new Uri(
                        _settings.InsightApi.BaseUrl),

                Timeout =
                    TimeSpan.FromSeconds(
                        _settings.InsightApi.TimeoutSeconds)
            };
        }

        public async Task<bool> SendMeasurementAsync(
            MeasurementResult result)
        {
            var request =
                new InsightMeasurementRequest
                {
                    MeasurementId =
                        result.MeasurementId,

                    StationId =
                        result.StationId,

                    Success =
                        result.Success,

                    X =
                        result.X,

                    Y =
                        result.Y,

                    Message =
                        result.Message,

                    Timestamp =
                        result.Timestamp
                };

            try
            {
                HttpResponseMessage response =
                    await _httpClient.PostAsJsonAsync(
                        _settings.InsightApi.MeasurementEndpoint,
                        request
                    );

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}