using vcarve.BezierSharp;

namespace vcarve
{
    public abstract class Segment
    {
        public int pathIdx { get; set; }
        public int winding { get; set; } = 1; // 1 = clockwise, -1 = counter-clockwise. Default to clockwise
        public Point Start {get; protected set;}
        public Point End {get; protected set;}

        /// <summary>
        /// Dot product of two vectors
        /// </summary>
        protected static double Dot(Point a, Point b)
        {
            return a.x * b.x + a.y * b.y;
        }

        public static double AngleBetween(Point a, Point b)
        {
            // Given that a and b are vectors, calculate the counter-clockwise angle between them
            // https://stackoverflow.com/questions/14066933/direct-way-of-computing-clockwise-angle-between-2-vectors
            var dot = Dot(a, b);
            var det = a.x * b.y - a.y * b.x; // Determinant
            var angle = Math.Atan2(det, dot); // atan2(y, x) or atan2(sin, cos)
            return angle;
        }

        public abstract Rect BoundingBox();

        public abstract Point NormalAt(double t);
        public abstract Point PointAt(double t);

        public abstract (Point point, double distance) ClosestPoint(Point p);
    }

    public record struct Point(double x, double y)
    {
        public static readonly Point Uninitalized = new(double.NaN, double.NaN);

        public static Point operator /(Point a, double b) => new Point(a.x / b, a.y / b);
        public static Point operator *(Point a, double b) => new Point(a.x * b, a.y * b);

        public static Point operator +(Point a, Point b) => new Point(a.x + b.x, a.y + b.y);
        public static Point operator -(Point a, Point b) => new Point(a.x - b.x, a.y - b.y);

        public Point Normalize()
        {
            return new Point(x / Length, y / Length);
        }

        public double ToPolarAngle()
        {
            // Convert to polar angle
            // https://stackoverflow.com/questions/2676719/calculating-the-angle-between-the-line-defined-by-two-points
            return Math.Atan2(y, x);
        }

        public readonly double Length => Math.Sqrt(x * x + y * y);
    }

    public record struct Rect(Point a, Point b)
    {
        public readonly double Width => Math.Abs(a.x - b.x);
        public readonly double Height => Math.Abs(a.y - b.y);
        public readonly double MidX => (a.x + b.x) / 2;
        public readonly double MidY => (a.y + b.y) / 2;
        
        public readonly double MinX => Math.Min(a.x, b.x);
        public readonly double MinY => Math.Min(a.y, b.y);
        public readonly double MaxX => Math.Max(a.x, b.x);
        public readonly double MaxY => Math.Max(a.y, b.y);

        public Rect(double x1,  double y1, double x2, double y2) : this(new Point(x1,y2), new Point(x2,y2)) { }

        /// <summary>
        /// Returns the largest vertical/horizontal distance between this and another rectangle
        /// </summary>
        /// <param name="other">Measure distance to this rectangle</param>
        /// <returns>Largest h/v distance. Negative values means overlap</returns>
        public double DistanceTo(Rect other)
        {
            return Math.Max(
                Math.Abs(MidX - other.MidX) - (Width + other.Width) / 2,
                Math.Abs(MidY - other.MidY) - (Height + other.Height) / 2
                );
        }
    }

    /// <summary>
    /// Special segment to deal with the case where two segments with different normals meet
    /// </summary>
    class JoinSegment : Segment
    {
        public readonly Point StartNormal;
        public readonly Point EndNormal;
        private readonly Point _mid;
        private readonly double s;
        private readonly double a;

        public JoinSegment(Point p, Point startNormal, Point endNormal)
        {
            Start = p;
            End = p;
            StartNormal = startNormal.Normalize();
            EndNormal = endNormal.Normalize();
            s = StartNormal.ToPolarAngle();
            a = AngleBetween(StartNormal, EndNormal);
            _mid = new Point(Math.Cos(s + a * 0.5), Math.Sin(s + a * 0.5));
            _bbox = new Rect(p, p);
        }

        private Rect _bbox;
        public override Rect BoundingBox() => _bbox;

        public override Point NormalAt(double t)
        {
            if (t <= 0) return StartNormal;
            if (t >= 1) return EndNormal;
            if (a > 0)
                return _mid; // TODO: Avoid ending up with this case - an inner angle does not need a join
            return new Point(Math.Cos(s + a * t), Math.Sin(s + a * t));
        }

        public override (Point point, double distance) ClosestPoint(Point p) => (Start, (Start - p).Length);

        public override Point PointAt(double t) => Start;
    }

    class LineSegment : Segment
    {
        public LineSegment(Point start, Point end)
        {
            Start = start;
            End = end;
        }

        public double Length => Math.Sqrt((End.x - Start.x)* (End.x - Start.x) + (End.y - Start.y) * (End.y - Start.y));

        public Point ToVector => new Point(End.x - Start.x, End.y - Start.y);

        public override Point PointAt(double t)
        {
            // return the point at t along the line segment
            return new Point(Start.x + t * (End.x - Start.x), Start.y + t * (End.y - Start.y));
        }

        public override string ToString()
        {
            return $"Line S:[{Start.x:0.###}:{Start.y:0.###}] E:[{End.x:0.###}:{End.y:0.###}] ";
        }

        private Rect? _bbox;
        public override Rect BoundingBox() => _bbox ??= new Rect(new Point(Math.Min(Start.x, End.x), Math.Min(Start.y, End.y)), new Point(Math.Max(Start.x, End.x), Math.Max(Start.y, End.y)));

        private Point? _normal;
        public override Point NormalAt(double t)
        {
            if (!_normal.HasValue)
            {
                // Given that the line segment is a vector from start to end, calculate the normalized normal
                // by rotating 90 degrees counter clockwise
                var v = ToVector;
                _normal = new Point(-v.y / Length, v.x / Length);
            }
            return _normal.Value * winding;
        }

        /// <summary>
        /// Copilot generated code
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override (Point point, double distance) ClosestPoint(Point p)
        {
            // Calculate the closest point on the line segment to the given point and the distance between them
            var v = ToVector;
            var w = new Point(p.x - Start.x, p.y - Start.y);
            var c1 = Dot(w, v);
            if (c1 <= 0)
                return (Start, Math.Sqrt(w.x * w.x + w.y * w.y));
            var c2 = Dot(v, v);
            if (c2 <= c1)
                return (End, Math.Sqrt((p.x - End.x) * (p.x - End.x) + (p.y - End.y) * (p.y - End.y)));
            var b = c1 / c2;
            var pb = new Point(Start.x + b * v.x, Start.y + b * v.y);
            return (pb, Math.Sqrt((p.x - pb.x) * (p.x - pb.x) + (p.y - pb.y) * (p.y - pb.y)));
        }
    }

    abstract class BezierSegment : Segment
    {
        private Bezier _bez;

        protected abstract Bezier CreateBezier();
        private Bezier Bez { get { return _bez ??= CreateBezier(); } }

        private Rect? _bbox;
        public override Rect BoundingBox() => _bbox ??= Bez.BoundingBox();

        public override Point PointAt(double t) => Bez.Compute(t);
        public override Point NormalAt(double t) => (Point)Bez.Normal(t) * winding;

        public override (Point point, double distance) ClosestPoint(Point p) => Bez.Project(p);
    }

    class QuadraticBezierSegment : BezierSegment
    {
        public QuadraticBezierSegment(Point start, Point control, Point end)
        {
            Start = start;
            Control = control;
            End = end;
        }

        public Point Control { get; }

        public override string ToString()
        {
            return $"Bezier S:[{Start.x:0.###}:{Start.y:0.###}] E:[{End.x:0.###}:{End.y:0.###}] C:[{Control.x:0.###}:{Control.y:0.###}]";
        }

        protected override Bezier CreateBezier() => new Bezier(Start, End, Control);
    }

    class CubicBezierSegment : BezierSegment
    {
        public CubicBezierSegment(Point start, Point control1, Point control2, Point end)
        {
            Start = start;
            Control1 = control1;
            Control2 = control2;
            End = end;
        }

        public Point Control1 { get; }
        public Point Control2 { get; }

        public override string ToString()
        {
            return $"Bezier S:[{Start.x:0.###}:{Start.y:0.###}] E:[{End.x:0.###}:{End.y:0.###}] C1:[{Control1.x:0.###}:{Control1.y:0.###}] C2:[{Control2.x:0.###}:{Control2.y:0.###}]";
        }

        protected override Bezier CreateBezier() => new Bezier(Start, End, Control1, Control2);
    }
}
