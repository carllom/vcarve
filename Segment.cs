using vcarve.BezierSharp;

namespace vcarve
{
    public class Segment
    {
        public int pathIdx { get; set; }
        public Point Start {get; protected set;}
        public Point End {get; protected set;}

        /// <summary>
        /// Dot product of two vectors
        /// </summary>
        protected static double Dot(Point a, Point b)
        {
            return a.x * b.x + a.y * b.y;
        }
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

        public readonly double Length => Math.Sqrt(x * x + y * y);
    }

    class LineSegment : Segment
    {
        public LineSegment(Point start, Point end)
        {
            Start = start;
            End = end;
        }

        /// <summary>
        /// TODO: validate - copilot generated code
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public Point ShortestDistanceTo(Point p)
        {
            var v = new Point(End.x - Start.x, End.y - Start.y);
            var w = new Point(p.x - Start.x, p.y - Start.y);
            var c1 = Dot(w, v);
            if (c1 <= 0)
                return Start;
            var c2 = Dot(v, v);
            if (c2 <= c1)
                return End;
            var b = c1 / c2;
            var pb = new Point(Start.x + b * v.x, Start.y + b * v.y);
            return pb;
        }

        public double Length => Math.Sqrt((End.x - Start.x)* (End.x - Start.x) + (End.y - Start.y) * (End.y - Start.y));

        public Point ToVector => new Point(End.x - Start.x, End.y - Start.y);

        public override string ToString()
        {
            return $"Line S:[{Start.x:0.###}:{Start.y:0.###}] E:[{End.x:0.###}:{End.y:0.###}] ";
        }
    }

    class QuadraticBezierSegment : Segment
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

        public Bezier AsBezier() => new Bezier(Start, End, Control);
    }

    class CubicBezierSegment : Segment
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

        public Bezier AsBezier() => new Bezier(Start, End, Control1, Control2);
    }
}
