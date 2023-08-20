namespace vcarve.GCode
{
    internal class ToolPathSegment
    {
        public Point p { get; set; } // Center point
        public double d { get; set; } // Depth
        public int idx { get; set; } // Path index

        public ToolPathSegment(Point p, double d, int idx)
        {
            this.p = p;
            this.d = d;
            this.idx = idx;
        }
    }
}
