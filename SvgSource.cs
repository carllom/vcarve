using System.Globalization;
using System.Text;
using System.Xml;
using vcarve.BezierSharp;
using vcarve.GCode;

namespace vcarve
{
    internal class SvgSource
    {
        private readonly string _path;
        private List<IEnumerable<Segment>> _svgpaths;

        // Determine clockwise or counter-clockwise: https://stackoverflow.com/a/1165943

        public SvgSource(string path)
        {
            _path = path;
            _svgpaths = new();

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            using var fstream = File.OpenText(path);
            using var xmlReader = XmlReader.Create(fstream);

            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element)
                {
                    if (xmlReader.Name == "path")
                    {
                        var d = xmlReader.GetAttribute("d");
                        var s = ParsePath(d);
                        if (s?.Count() > 0) _svgpaths.Add(s);

                        var res = RenderPath(s);
                        
                        File.Copy(path, TargetPath(path), true);
                        AppendVisualization(TargetPath(path), VisualizeContour(s));
                        AppendVisualization(TargetPath(path), VisualizeSegments(s));
                        AppendVisualization(TargetPath(path), VisualizeTrace(s));
                        AppendVisualization(TargetPath(path), VisualizePath(res));

                        RenderGCode(GCodePath(path), res);
                    }
                }
            }
        }

        private static double Number(string s) => double.Parse(s, CultureInfo.InvariantCulture);

        /// <summary>
        /// Split the svg path into segments
        /// </summary>
        /// <param name="pathspec"></param>
        /// <returns></returns>
        private IEnumerable<Segment> ParsePath(string? pathspec)
        {
            if (string.IsNullOrWhiteSpace(pathspec)) return Array.Empty<Segment>();
            var tokens = pathspec.Split(new[] {'\t',' ',','}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Point initial = Point.Uninitalized, current = Point.Uninitalized;

            var pathIdx = 0;
            var segments = new List<Segment>();
            string command = string.Empty;
            for (int i = 0; i < tokens.Length;)
            {
                // New command
                if (char.IsLetter(tokens[i][0]))
                {
                    command = tokens[i];
                    i++;
                }

                // Specification: https://developer.mozilla.org/en-US/docs/Web/SVG/Tutorial/Paths
                switch (command)
                {
                    case "M": // Move to - implicitly new path
                        { 
                            var x = Number(tokens[i++]);
                            var y = Number(tokens[i++]);
                            initial = current = new Point(x, y);
                            command = "L"; // Multiple coordinates are treated as line segments
                        }
                        break;
                    case "m": // Move to (relative) - implicitly new path
                        {
                            var c = current;
                            if (c == Point.Uninitalized) current = new Point(0, 0); // If "m" is first in path, first coordinate pair is treated as absolute, so we'll initialize it to origin.
                            var x = current.x + Number(tokens[i++]);
                            var y = current.y + Number(tokens[i++]);
                            initial = current = new Point(x, y);
                            command = "l"; // Multiple coordinates are treated as line segments
                        }
                        break;
                    case "L": // Line
                        {
                            var x1 = Number(tokens[i++]);
                            var y1 = Number(tokens[i++]);
                            var end = new Point(x1, y1);
                            segments.Add(new LineSegment(current, end) { pathIdx = pathIdx });
                            current = end;
                        }
                        break;
                    case "l": // Line (relative)
                        {
                            var x1r = current.x + Number(tokens[i++]);
                            var y1r = current.y + Number(tokens[i++]);
                            var end = new Point(x1r, y1r);
                            segments.Add(new LineSegment(current, end) { pathIdx = pathIdx });
                            current = end;
                        }
                        break;
                    case "H": // Horizontal line
                        {
                            var x1 = Number(tokens[i++]);
                            var end = new Point(x1, current.y);
                            segments.Add(new LineSegment(current, end) { pathIdx = pathIdx });
                            current = end;
                        }
                        break;
                    case "h": // Horizontal line (relative)
                        {
                            var x1r = current.x + Number(tokens[i++]);
                            var end = new Point(x1r, current.y);
                            segments.Add(new LineSegment(current, end) { pathIdx = pathIdx });
                            current = end;
                        }
                        break;
                    case "V": // Vertical line
                        {
                            var y1 = Number(tokens[i++]);
                            var end = new Point(current.x, y1);
                            segments.Add(new LineSegment(current, end) { pathIdx = pathIdx });
                            current = end;
                        }
                        break;
                    case "v": // Vertical line (relative)
                        {
                            var y1r = current.y + Number(tokens[i++]);
                            var end = new Point(current.x, y1r);
                            segments.Add(new LineSegment(current, end) { pathIdx = pathIdx });
                            current = end;
                        }
                        break;
                    case "C": // Cubic Bezier curve
                        {
                            var c1x = Number(tokens[i++]);
                            var c1y = Number(tokens[i++]);
                            var c2x = Number(tokens[i++]);
                            var c2y = Number(tokens[i++]);
                            var xend = Number(tokens[i++]);
                            var yend = Number(tokens[i++]);
                            var end = new Point(xend, yend);
                            segments.Add(new CubicBezierSegment(current, new Point(c1x, c1y), new Point(c2x, c2y), end) { pathIdx = pathIdx });
                            current = end;
                        }
                        break;
                    case "c": // Cubic Bezier curve (relative)
                        {
                            var c1x = current.x + Number(tokens[i++]);
                            var c1y = current.y + Number(tokens[i++]);
                            var c2x = current.x + Number(tokens[i++]);
                            var c2y = current.y + Number(tokens[i++]);
                            var xend = current.x + Number(tokens[i++]);
                            var yend = current.y + Number(tokens[i++]);
                            var end = new Point(xend, yend);
                            segments.Add(new CubicBezierSegment(current, new Point(c1x, c1y), new Point(c2x, c2y), end) { pathIdx = pathIdx });
                            current = end;
                        }
                        break;
                    case "Q": // Quadratic curve
                        {
                            var cx = Number(tokens[i++]);
                            var cy = Number(tokens[i++]);
                            var xend = Number(tokens[i++]);
                            var yend = Number(tokens[i++]);
                            var control = new Point(cx, cy);
                            var end = new Point(xend, yend);
                            segments.Add(new QuadraticBezierSegment(current, control, end) { pathIdx = pathIdx });
                            current = end;
                        }
                        break;
                    case "q": // Quadratic curve (relative)
                        {
                            var cx = current.x + Number(tokens[i++]);
                            var cy = current.y + Number(tokens[i++]);
                            var xend = current.x + Number(tokens[i++]);
                            var yend = current.y + Number(tokens[i++]);
                            var control = new Point(cx, cy);
                            var end = new Point(xend, yend);
                            segments.Add(new QuadraticBezierSegment(current, control, end) { pathIdx = pathIdx });
                            current = end;
                        }
                        break;
                    case "S": // Multi-segment cubic Bezier curve
                    case "s":
                    case "T": // Multi-segment quadratic Bezier curve
                    case "t":
                        throw new NotImplementedException("Multi-segment Bezier curves 'S,s,T,t' not implemented yet");
                        break;
                    case "A": // Arc
                    case "a":
                        throw new NotImplementedException("Arcs 'A,a' not implemented yet");
                        break;
                    case "Z": // Close path
                    case "z":
                        segments.Add(new LineSegment(current, initial) { pathIdx = pathIdx });
                        current = initial;
                        pathIdx++; // Next sub-path index
                        break;
                    default:
                        throw new NotImplementedException($"Command '{command}' not implemented yet");

                }
            }
            return segments;
        }

        private IEnumerable<ToolPathSegment> RenderPath(IEnumerable<Segment> segments)
        {
            double toolradius = 2;
            List<ToolPathSegment> result = new();
            int segidx = 1, count = segments.Count();
            foreach (var segment in segments)
            {
                switch (segment)
                {
                    case CubicBezierSegment cbseg:
                        // Skip silently for now
                        break;
                    case QuadraticBezierSegment qbseg:
                        var qbez = qbseg.AsBezier();
                        for (double t = 0.01; t < 1; t+=.05)
                        {
                            Point p = qbez.Compute(t); // Point on traced curve
                            Point n = qbez.Normal(t); // Normal at current t
                            Point tc = Point.Uninitalized; // Tool center point
                            double depth = 0;
                            for (double d = toolradius; d>0; d-=.1) // Begin with max tool radius and decrease
                            {
                                tc = p + n * d;
                                bool touched = false;
                                foreach(var oseg in segments)
                                {
                                    if (oseg == segment) continue; // Do not compare the segment with itself
                                    var cp = ClosestPoint(oseg, tc);
                                    if (cp.point == p && (t == 0 || t ==1)) continue; // Do not take endpoints of neighbouring segments into account;
                                    if (cp.dist <= d)
                                    {
                                        touched = true;
                                        break;
                                    }
                                }
                                if (!touched)
                                {
                                    depth = d;
                                    break; // We did not touch any other curves, this is a safe depth
                                }
                            }
                            result.Add(new(tc, depth, qbseg.pathIdx));
                        }
                        break;
                    case LineSegment line:
                        // Skip silently for now
                        break;
                    default:
                        throw new NotImplementedException($"Segment type {segment.GetType().Name} is not supported");
                        break;
                }
                Console.WriteLine($"{segidx++}/{count} segments done");
            }
            return result;
        }

        private (TPoint point, double dist) ClosestPoint(Segment oseg, Point tc)
        {
            switch (oseg)
            {
                case CubicBezierSegment cbseg:
                    return cbseg.AsBezier().Project(tc);
                case QuadraticBezierSegment qbseg:
                    return qbseg.AsBezier().Project(tc);
                case LineSegment line:
                    return (new TPoint(Point.Uninitalized, 0), double.MaxValue);
                //    break;
                default:
                    throw new NotImplementedException($"Segment type {oseg.GetType().Name} is not supported");
            }
        }

        private static string GCodePath(string path) => Path.Combine(Path.GetDirectoryName(path), $"{Path.GetFileNameWithoutExtension(path)}.gcode");
        private void RenderGCode(string path, IEnumerable<ToolPathSegment> toolpath)
        {
            using var file = new StreamWriter(path);
            file.WriteLine("%");
            file.WriteLine("(Generated by vcarve)");
            file.WriteLine("G01 F100 (feed rate)");
            var pathidx = -1; //first.idx;
            foreach (var c in toolpath)
            {
                if (c.idx != pathidx)
                {
                    file.WriteLine("(new sub-path)");
                    file.WriteLine("G00 Z0.5");
                    file.WriteLine($"G00 X{c.p.x} Y{-c.p.y}");
                    pathidx = c.idx;
                }
                file.WriteLine($"G01 X{c.p.x} Y{-c.p.y} Z{-c.d*2}");
            }
            file.WriteLine("(end)");
            file.WriteLine("G28 G91 X0 Y0 Z0.5");
            
        }

        #region Visualization

        private static string TargetPath(string path) => Path.Combine(Path.GetDirectoryName(path), $"{Path.GetFileNameWithoutExtension(path)}-annotated{Path.GetExtension(path)}");

        private static void AppendVisualization(string path, string content)
        {
            using (var wrt = new StreamWriter(path + ".tmp"))
            {
                using (var rdr = File.OpenText(path))
                {
                    while (!rdr.EndOfStream)
                    {
                        var l = rdr.ReadLine();
                        if (l.Trim() == "</svg>") break;
                        wrt.WriteLine(l);
                    }
                }
                wrt.WriteLine("<g>");
                wrt.Write(content);
                wrt.WriteLine("</g>");
                wrt.WriteLine("</svg>");
            }
            File.Move(path + ".tmp", path, true);
        }

        private static string VisualizeContour(IEnumerable<Segment> s)
        {
            var sb = new StringBuilder();
            foreach (var seg in s)
            {
                switch (seg)
                {
                    case CubicBezierSegment cbseg:
                        // Cubic curve approximation
                        var cbez = new Bezier(cbseg.Start, cbseg.End, cbseg.Control1, cbseg.Control2);
                        foreach (var cp in cbez.GetLUT())
                        {
                            sb.AppendSvgDisc(cp, 0.1, "red", 0.3);
                        }
                        break;
                    case QuadraticBezierSegment qbseg:
                        // Quadratic curve approximation
                        var qbez = new Bezier(qbseg.Start, qbseg.End, qbseg.Control);
                        foreach (var cp in qbez.GetLUT())
                        {
                            sb.AppendSvgDisc(cp, 0.1, "red", 0.3);
                        }
                        break;
                    case LineSegment line:
                        sb.AppendSvgLine(line.Start, line.End, 0.2, "red", 0.3);
                        // Use 100 dots
                        //var v = line.ToVector;
                        //for(int t=0;t<=100; t++)
                        //{
                        //    sb.AppendSvgDisc(line.Start + v * (t / 100d), 0.1, "red", 0.3);
                        //}
                        break;
                    default:
                        throw new NotImplementedException($"Currently there is no visual representation for {seg.GetType().Name}");
                        break;
                }
            }
            return sb.ToString();
        }

        private static string VisualizeTrace(IEnumerable<Segment> s)
        {
            var sb = new StringBuilder();
            foreach (var seg in s)
            {
                switch (seg)
                {
                    case CubicBezierSegment cbseg:
                        break;
                    case QuadraticBezierSegment qbseg:
                        var qbez = qbseg.AsBezier();
                        for (var t = 0d; t <= 1; t += .1)
                        {
                            Point p = qbez.Compute(t);
                            Point n = qbez.Normal(t);
                            n = n.Normalize();
                            sb.AppendSvgLine(p, p + n, 0.1, "lime");
                        }
                        break;
                    case LineSegment line:
                        break;
                    default:
                        throw new NotImplementedException($"Currently there is no visual representation for {seg.GetType().Name}");
                        break;
                }
            }
            return sb.ToString();
        }


        private static string VisualizeSegments(IEnumerable<Segment> s)
        {
            var sb = new StringBuilder();
            var startpel = new Point(0.5, 0.15); // Start point ellipse
            var endpel = new Point(0.15, 0.5); // Start point ellipse

            // Visualization
            foreach (var seg in s)
            {
                switch (seg)
                {
                    case CubicBezierSegment cbseg:
                        // Control points
                        sb.AppendSvgLine(cbseg.Start, cbseg.Control1, 0.1, "cyan");
                        sb.AppendSvgLine(cbseg.End, cbseg.Control2, 0.1, "cyan");
                        sb.AppendSvgCircle(cbseg.Control1, 0.3, "yellow");
                        sb.AppendSvgCircle(cbseg.Control2, 0.3, "yellow");
                        break;
                    case QuadraticBezierSegment qbseg:
                        // Control points
                        sb.AppendSvgLine(qbseg.Start, qbseg.Control, 0.1, "cyan");
                        sb.AppendSvgLine(qbseg.End, qbseg.Control, 0.1, "cyan");
                        sb.AppendSvgCircle(qbseg.Control, 0.3, "yellow");
                        break;
                    case LineSegment line:
                        break;
                    default:
                        throw new NotImplementedException($"Currently there is no visual representation for {seg.GetType().Name}");
                        break;
                }

                // Start/end points
                sb.AppendSvgDisc(seg.Start, startpel, "green");
                sb.AppendSvgDisc(seg.End, endpel, "purple");
            }
            return sb.ToString();
        }

        private static string VisualizePath(IEnumerable<ToolPathSegment> res)
        {
            var sb = new StringBuilder();
            foreach (var c in res)
            {
                sb.AppendSvgCircle(c.p, c.d, "teal");
            }

            return sb.ToString();
        }

        #endregion
    }


    public static class StringBuilderExtensions
    {
        public static StringBuilder AppendSvgCircle(this StringBuilder sb, Point c, double r, string color, double opacity = 1.0)
        {
            var o = opacity == 1.0 ? string.Empty : $"opacity=\"{opacity}\"";
            return sb.AppendLine($"<circle cx=\"{c.x}\" cy=\"{c.y}\" r=\"{r}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"0.1\" {o}/>");
        }
        public static StringBuilder AppendSvgDisc(this StringBuilder sb, Point c, double r, string color, double opacity = 1.0)
        {
            var o = opacity == 1.0 ? string.Empty : $"opacity=\"{opacity}\"";
            return sb.AppendLine($"<circle cx=\"{c.x}\" cy=\"{c.y}\" r=\"{r}\" fill=\"{color}\" stroke=\"none\" {o}/>");
        }

        public static StringBuilder AppendSvgDisc(this StringBuilder sb, Point c, Point r, string color, double opacity = 1.0)
        {
            var o = opacity == 1.0 ? string.Empty : $"opacity=\"{opacity}\"";
            return sb.AppendLine($"<ellipse cx=\"{c.x}\" cy=\"{c.y}\" rx=\"{r.x}\" ry=\"{r.y}\" fill=\"{color}\" stroke=\"none\" {o}/>");
        }

        public static StringBuilder AppendSvgLine(this StringBuilder sb, Point start, Point end, double width, string color, double opacity = 1.0)
        {
            var o = opacity == 1.0 ? string.Empty : $"stroke-opacity=\"{opacity}\"";
            return sb.AppendLine($"<line x1=\"{start.x}\" y1=\"{start.y}\" x2=\"{end.x}\" y2=\"{end.y}\" stroke=\"{color}\" stroke-width=\"{width}\" {o}/>");
        }
    }
}
