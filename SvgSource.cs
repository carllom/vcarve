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

            tpNoD = NumberOfDecimals(ToolPrecision);
            srNoD = NumberOfDecimals(StepResolution);
            tsNoD = NumberOfDecimals(TraceStep);

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

                        var res = RenderToolPath(s);

                        File.Copy(path, TargetPath(path), true);
                        AppendVisualization(TargetPath(path), VisualizeBBox(s));
                        AppendVisualization(TargetPath(path), VisualizeContour(s));
                        AppendVisualization(TargetPath(path), VisualizeSegments(s));
                        AppendVisualization(TargetPath(path), VisualizeNormals(s));
                        AppendVisualization(TargetPath(path), VisualizeToolPath(res));

                        RenderGCode(GCodePath(path), res);
                    }
                }
            }
        }

        // Precision configuration
        private int NumberOfDecimals(double value)
        {
            var s = value.ToString(CultureInfo.InvariantCulture);
            var dp = s.IndexOf(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator) + 1;
            if (dp == -1) return 0;
            return s.Length - dp;
        }
        private const double ToolPrecision = 0.01; // Tool movement precision
        private readonly int tpNoD; // Number of decimals for tool precision

        private const double StepResolution = 0.01; // Tool depth step resolution
        private readonly int srNoD; // Number of decimals for depth step resolution

        private const double TraceStep = 0.1; // Tracing step resolution (in terms of t)
        private readonly int tsNoD; // Number of decimals for tracing step resolution


        private static double Number(string s) => double.Parse(s, CultureInfo.InvariantCulture);

        /// <summary>
        /// Split the svg path into segments
        /// </summary>
        /// <param name="pathSpec"></param>
        /// <returns></returns>
        private IEnumerable<Segment> ParsePath(string? pathSpec)
        {
            if (string.IsNullOrWhiteSpace(pathSpec)) return Array.Empty<Segment>();
            var tokens = pathSpec.Split(new[] {'\t',' ',','}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Point initial = Point.Uninitalized, current = Point.Uninitalized;
            Point initialNormal = Point.Uninitalized, currentNormal = Point.Uninitalized;

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
                            initialNormal = currentNormal = Point.Uninitalized;
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
                            initialNormal = currentNormal =  Point.Uninitalized;

                            command = "l"; // Multiple coordinates are treated as line segments
                        }
                        break;
                    case "L": // Line
                        {
                            var x1 = Number(tokens[i++]);
                            var y1 = Number(tokens[i++]);
                            var end = new Point(x1, y1);
                            var ls = new LineSegment(current, end) { pathIdx = pathIdx };
                            if (initialNormal == Point.Uninitalized) initialNormal = ls.Normal();
                            segments.Add(ls);
                            current = end;
                            currentNormal = ls.Normal();
                        }
                        break;
                    case "l": // Line (relative)
                        {
                            var x1r = current.x + Number(tokens[i++]);
                            var y1r = current.y + Number(tokens[i++]);
                            var end = new Point(x1r, y1r);
                            var ls = new LineSegment(current, end) { pathIdx = pathIdx };
                            if (initialNormal == Point.Uninitalized) initialNormal = ls.Normal();
                            segments.Add(ls);
                            current = end;
                        }
                        break;
                    case "H": // Horizontal line
                        {
                            var x1 = Number(tokens[i++]);
                            var end = new Point(x1, current.y);
                            var ls = new LineSegment(current, end) { pathIdx = pathIdx };
                            if (initialNormal == Point.Uninitalized) initialNormal = ls.Normal();
                            segments.Add(ls);
                            current = end;
                            currentNormal = ls.Normal();
                        }
                        break;
                    case "h": // Horizontal line (relative)
                        {
                            var x1r = current.x + Number(tokens[i++]);
                            var end = new Point(x1r, current.y);
                            var ls = new LineSegment(current, end) { pathIdx = pathIdx };
                            if (initialNormal == Point.Uninitalized) initialNormal = ls.Normal();
                            segments.Add(ls);
                            current = end;
                            currentNormal = ls.Normal();
                        }
                        break;
                    case "V": // Vertical line
                        {
                            var y1 = Number(tokens[i++]);
                            var end = new Point(current.x, y1);
                            var ls = new LineSegment(current, end) { pathIdx = pathIdx };
                            if (initialNormal == Point.Uninitalized) initialNormal = ls.Normal();
                            segments.Add(ls);
                            current = end;
                            currentNormal = ls.Normal();
                        }
                        break;
                    case "v": // Vertical line (relative)
                        {
                            var y1r = current.y + Number(tokens[i++]);
                            var end = new Point(current.x, y1r);
                            var ls = new LineSegment(current, end) { pathIdx = pathIdx };
                            if (initialNormal == Point.Uninitalized) initialNormal = ls.Normal();
                            segments.Add(ls);
                            current = end;
                            currentNormal = ls.Normal();
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
                            var cbs = new CubicBezierSegment(current, new Point(c1x, c1y), new Point(c2x, c2y), end) { pathIdx = pathIdx };
                            if (initialNormal == Point.Uninitalized) initialNormal = cbs.Bez.Normal(0);
                            segments.Add(cbs);
                            current = end;
                            currentNormal = cbs.Bez.Normal(1);
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
                            var cbs = new CubicBezierSegment(current, new Point(c1x, c1y), new Point(c2x, c2y), end) { pathIdx = pathIdx };
                            if (initialNormal == Point.Uninitalized) initialNormal = cbs.Bez.Normal(0);
                            segments.Add(cbs);
                            current = end;
                            currentNormal = cbs.Bez.Normal(1);
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
                            var qbs = new QuadraticBezierSegment(current, control, end) { pathIdx = pathIdx };
                            if (initialNormal == Point.Uninitalized) initialNormal = qbs.Bez.Normal(0);
                            segments.Add(qbs);
                            current = end;
                            currentNormal = qbs.Bez.Normal(1);
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
                            var qbs = new QuadraticBezierSegment(current, control, end) { pathIdx = pathIdx };
                            if (initialNormal == Point.Uninitalized) initialNormal = qbs.Bez.Normal(0);
                            segments.Add(qbs);
                            current = end;
                            currentNormal = qbs.Bez.Normal(1);
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
                        if (RTP((current-initial).Length) == 0) // No distance gap between the current and initial point - close with a join
                        {
                            if (currentNormal != initialNormal) // Only necessary to add the join if there is a normal gap
                                segments.Add(new JoinSegment(current, currentNormal, initialNormal) { pathIdx = pathIdx });
                        }
                        else // There is a distance gap - close with a line
                        {
                            var ls = new LineSegment(current, initial) { pathIdx = pathIdx };
                            segments.Add(ls);
                            if (ls.Normal() != initialNormal) // Also add a join if there is a normal gap between the closing line and the initial normal
                                segments.Add(new JoinSegment(initial, ls.Normal(), initialNormal) { pathIdx = pathIdx });
                        }
                           
                        current = initial;
                        pathIdx++; // Next sub-path index
                        break;
                    default:
                        throw new NotImplementedException($"Command '{command}' not implemented yet");

                }
            }

            // If two consecutive segments are line segments, insert a join segment between them having an intermediate normal
            int sIdx = 0;
            while (sIdx < segments.Count - 1)
            {
                if (segments[sIdx] is LineSegment ls1 && segments[sIdx + 1] is LineSegment ls2 && ls1.pathIdx == ls2.pathIdx && ls1.End == ls2.Start)
                {
                    var normal = ls1.Normal();
                    var normal2 = ls2.Normal();
                    var a = Segment.AngleBetween(normal, normal2);
                    if (a < 0) // Only for outer corners
                        segments.Insert(sIdx + 1, new JoinSegment(ls1.End, normal, normal2) { pathIdx = ls1.pathIdx });
                }
                sIdx++;
            }

            return segments;
        }

        private IEnumerable<ToolPathSegment> RenderToolPath(IEnumerable<Segment> segments)
        {
            double toolRadius = 2;
            List<ToolPathSegment> result = new();
            int segidx = 1, count = segments.Count();
            int cnt = 0;
            foreach (var segment in segments)
            {
                cnt++;
                var neighbourSegs = segments.Where(s => s != segment && segment.BoundingBox().DistanceTo(s.BoundingBox()) <= toolRadius * 2).ToList(); // Get all segments that are within the tool radius of the current segment

                switch (segment)
                {
                    case CubicBezierSegment cbSeg:
                        // Skip silently for now
                        break;
                    case QuadraticBezierSegment qbSeg:
                        var qbez = qbSeg.Bez;

                        for (double t = 0; t <= 1; t = Math.Round(t + TraceStep, tsNoD))
                        {
                            Point p = qbez.Compute(t); // Point on traced curve
                            Point n = qbez.Normal(t); // Normal at current t
                            Point tc = Point.Uninitalized; // Tool center point
                            double depth = 0;
                            for (double d = toolRadius; d>0; d = Math.Round(d - StepResolution, srNoD)) // Begin with max tool radius and decrease
                            {
                                d = Math.Round(d, srNoD); // TODO: Round properly!!
                                tc = p + n * d;
                                bool touched = false;
                                foreach(var oSeg in neighbourSegs)
                                {
                                    //if (oSeg == segment) continue; // Do not compare the segment with itself
                                    var cp = ClosestPoint(oSeg, tc);
                                    if (cp.point == p && (t == 0 || t ==1)) continue; // Do not take endpoints of neighbouring segments into account;
                                    if (Math.Round(cp.dist,tpNoD) < d) // If the closest point on the other segment is within the tool radius (taking tool precision into account)
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
                            result.Add(new(tc, depth, qbSeg.pathIdx));
                        }
                        break;
                    case LineSegment line:
                        for (double t = 0; t <= 1; t = Math.Round(t + TraceStep, tsNoD))
                        {
                            Point p = line.PointAt(t); // Point on line
                            Point n = line.Normal(); // Line normal
                            Point tc = Point.Uninitalized; // Tool center point
                            double depth = 0;
                            for (double d = toolRadius; d > 0; d = Math.Round(d - StepResolution, srNoD)) // Begin with max tool radius and decrease
                            {
                                d = Math.Round(d, srNoD); // TODO: Round properly!!
                                tc = p + n * d;
                                bool touched = false;
                                foreach (var oSeg in neighbourSegs)
                                {
                                    var cp = ClosestPoint(oSeg, tc);
                                    if (cp.point == p && (t == 0 || t == 1)) continue; // Do not take endpoints of neighbouring segments into account;
                                    if (Math.Round(cp.dist, tpNoD) < d) // If the closest point on the other segment is within the tool radius (taking tool precision into account)
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
                            result.Add(new(tc, depth, line.pathIdx));
                        }
                        break;
                    case JoinSegment join:
                        for (double t = 0; t <= 1; t = Math.Round(t + TraceStep, tsNoD))
                        {
                            Point p = join.Start; // Join point
                            Point n = join.Normal(t);
                            Point tc = Point.Uninitalized; // Tool center point
                            double depth = 0;
                            for (double d = toolRadius; d > 0; d = Math.Round(d - StepResolution, srNoD)) // Begin with max tool radius and decrease
                            {
                                d = Math.Round(d, srNoD); // TODO: Round properly!!
                                tc = p + n * d;
                                bool touched = false;
                                foreach (var oSeg in neighbourSegs)
                                {
                                    var cp = ClosestPoint(oSeg, tc);
                                    if (cp.point == p) continue; // Do not take endpoints of neighbouring segments into account;
                                    if (Math.Round(cp.dist, tpNoD) < d) // If the closest point on the other segment is within the tool radius (taking tool precision into account)
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
                            result.Add(new(tc, depth, join.pathIdx));
                        }
                        break;
                    default:
                        throw new NotImplementedException($"Segment type {segment.GetType().Name} is not supported");
                        break;
                }
                Console.WriteLine($"{segidx++}/{count} segments done");
            }
            return result;
        }

        private (Point point, double dist) ClosestPoint(Segment oseg, Point tc)
        {
            switch (oseg)
            {
                case CubicBezierSegment cbseg:
                    return cbseg.Bez.Project(tc);
                case QuadraticBezierSegment qbseg:
                    return qbseg.Bez.Project(tc);
                case LineSegment line:
                    return line.ClosestPoint(tc);
                case JoinSegment join:
                    return join.ClosestPoint(tc);
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
            var pathidx = -1;
            foreach (var c in toolpath)
            {
                if (c.idx != pathidx)
                {
                    file.WriteLine("(new sub-path)");
                    file.WriteLine("G00 Z0.5");
                    file.WriteLine($"G00 X{RTP(c.p.x)} Y{RTP(-c.p.y)}");
                    pathidx = c.idx;
                }
                file.WriteLine($"G01 X{RTP(c.p.x)} Y{RTP(-c.p.y)} Z{RTP(-c.d*2)}");
            }
            file.WriteLine("G00 Z0.5");
            file.WriteLine("(end)");
            file.WriteLine("G28 G91 X0 Y0 Z0.5");
        }
        private double RTP(double value) => Math.Round(value, tpNoD); // Round to tool precision (0.001)


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
                    case JoinSegment join:
                        sb.AppendSvgDisc(join.Start, 0.1, "red", 0.3);
                        break;
                    default:
                        throw new NotImplementedException($"Currently there is no visual representation for {seg.GetType().Name}");
                        break;
                }
            }
            return sb.ToString();
        }

        private string VisualizeNormals(IEnumerable<Segment> s)
        {
            var sb = new StringBuilder();
            foreach (var seg in s)
            {
                switch (seg)
                {
                    case CubicBezierSegment cbseg:
                        break;
                    case QuadraticBezierSegment qbseg:
                        var qbez = qbseg.Bez;
                        for (double t = 0; t <= 1; t = Math.Round(t + TraceStep, tsNoD))
                        {
                            Point p = qbez.Compute(t);
                            Point n = qbez.Normal(t);
                            n = n.Normalize();
                            sb.AppendSvgLine(p, p + n, 0.1, "lime");
                        }
                        break;
                    case LineSegment line:
                        {
                            Point n = line.Normal();
                            for (var t = 0d; t <= 1; t += .1)
                            {
                                Point p = line.PointAt(t);
                                sb.AppendSvgLine(p, p + n, 0.1, "lime");
                            }
                        }
                        break;
                    case JoinSegment join:
                        for (double t = 0; t <= 1; t = Math.Round(t + TraceStep, tsNoD))
                        {
                            Point n = join.Normal(t);
                            sb.AppendSvgLine(join.Start, join.Start + n, 0.1, "lime");
                        }
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
                        break; // Lines have no extra visual representation
                    case JoinSegment join:
                        break; // Joins have no extra visual representation
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

        private static string VisualizeToolPath(IEnumerable<ToolPathSegment> res)
        {
            var sb = new StringBuilder();
            foreach (var c in res)
            {
                sb.AppendSvgCircle(c.p, c.d, "teal");
            }

            return sb.ToString();
        }

        private string VisualizeBBox(IEnumerable<Segment> s)
        {
            var sb = new StringBuilder();
            int c = 0;
            foreach (var seg in s)
            {
                switch (seg)
                {
                    case CubicBezierSegment cbseg:
                        break;
                    case QuadraticBezierSegment qbseg:
                        {
                            var bbox = qbseg.Bez.BoundingBox();
                            sb.AppendLine($"<rect x=\"{bbox.MinX}\" y=\"{bbox.MinY}\" width=\"{bbox.Width}\" height=\"{bbox.Height}\" fill=\"black\" stroke=\"red\" stroke-width=\"0.1\" opacity=\"0.2\" />");
                        }
                        break;
                    case LineSegment line:
                        {
                            if (line.Length < 0.001) 
                                break;
                        
                            var bbox = line.BoundingBox();
                            sb.AppendLine($"<rect x=\"{bbox.MinX}\" y=\"{bbox.MinY}\" width=\"{bbox.Width}\" height=\"{bbox.Height}\" fill=\"black\" stroke=\"red\" stroke-width=\"0.1\" opacity=\"0.2\" />");
                        }
                        break;
                    case JoinSegment join:
                        break; // No bbox for join
                    default:
                        throw new NotImplementedException($"Currently there is no visual representation for {seg.GetType().Name}");
                        break;
                }

                //if (c++ > 0) break;
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
