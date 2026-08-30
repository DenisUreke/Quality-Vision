using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quality_Vision.Api.Models
{
    public class InsightMeasurementRequest
    {
        public Guid MeasurementId { get; set; }

        public int StationId { get; set; }

        public bool Success { get; set; }

        public double? X { get; set; }

        public double? Y { get; set; }

        public string? Message { get; set; }

        public DateTime Timestamp { get; set; }
    }
}