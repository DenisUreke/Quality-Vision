using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quality_Vision.Api.Models
{
    public class MeasurementAcceptedResponse
    {
        public Guid MeasurementId { get; set; }

        public int StationId { get; set; }

        public string Status { get; set; } = "Accepted";
    }
}
