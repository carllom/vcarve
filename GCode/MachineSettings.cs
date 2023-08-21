using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vcarve.GCode
{
    internal class MachineSettings
    {
        /// <summary>
        /// The maximum allowed cutting depth
        /// </summary>
        public double MaxDepth { get; set; }

        /// <summary>
        /// Z height for non-cutting moves
        /// </summary>
        public double SafeHeight { get; set; }

        /// <summary>
        /// Machine origin
        /// </summary>
        public Point Origin { get; set; }

        /// <summary>
        /// The maximum feed rate mm/min of the tool
        /// </summary>
        public int FeedRate { get; set; }

        /// <summary>
        /// Machine movement precision in mm
        /// </summary>
        public double Precision { get; set; }

        /// <summary>
        /// Current tool
        /// </summary>
        public ConicalTool Tool { get; set; }

        public MachineSettings()
        {
            MaxDepth = 25;
            SafeHeight = -0.5;
            Origin = new Point(0, 0);
            FeedRate = 1000;
            Precision = 0.01;
            Tool = new ConicalTool(45, 5);
        }
    }
}
