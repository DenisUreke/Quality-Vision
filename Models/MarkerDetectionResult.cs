using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenCvSharp;

namespace Quality_Vision.Models
{
    public class MarkerDetectionResult
    {
        public int[] Ids { get; set; } = Array.Empty<int>();

        public Point2f[][] Corners { get; set; } = Array.Empty<Point2f[]>();
    }
}