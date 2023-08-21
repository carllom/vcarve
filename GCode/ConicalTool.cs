namespace vcarve.GCode
{
    internal class ConicalTool
    {
        private double _angle;
        private double _radius;

        public ConicalTool(double angle, double radius)
        {
            Angle = angle;
            Radius = radius;
        }

        /// <summary>
        /// Max cutting depth for the tool
        /// </summary>
        public double MaxDepth { get; private set; }

        /// <summary>
        /// Tool cone angle in degrees
        /// </summary>
        public double Angle 
        {
            get => _angle;
            set { _angle = value; Update(); }
        }

        /// <summary>
        /// Tool radius (max cutting)
        /// </summary>
        public double Radius 
        {
            get => _radius;
            set { _radius = value; Update(); }
        }

        /// <summary>
        /// Tool diameter (max cutting)
        /// </summary>
        public double Diameter => Radius * 2;

        private void Update()
        {
            _tan = Math.Tan(Angle * Math.PI / 180);
            MaxDepth = DepthAtRadius(Radius);
        }
        
        private double _tan; // Slope of the cone

        public double RadiusAtDepth(double depth)
        {
            if (depth > MaxDepth) return -1;
            return _tan * depth;
        }

        public double DepthAtRadius(double radius)
        {
            if (radius < 0 || radius > Radius) return -1;
            return radius / _tan;
        }
    }
}
