# Notes

## Bugs

### (DONE) Tool shortcuts on convex corners between two lines

Added spot segment with intermediate normal to try to prevent this. Still generates shortcut.
I think tool must take a circular path between the end of the first(a) line and the start of the second(b).
Trace the tool by stepping the normal from a.normal to b.normal using the spot point.
Spot segment needs two normals, one from a, one from b.

> Join segments added

### (DONE) Bauhaus 93 sample 'B' is inside out

Is it rendered clockwise? Can we see some other indication that this letter is unique. Other letters in sample are OK.

> Direction is calculated and normals adjusted accordingly

### Gothic letters contain difficult normals

Some curves loop back on themselves: The control point is near but beyond the endpoint resulting in a kind of 'hook' shape. The normals near the end point (tip of the hook) point outwards, either crossing the curve itself or the adjacent segment.

## Improvements

### GCode simplification/crunching

When trace path has been generated, see if a range of movements can be simplified.
Approximate trace with gcode lines and curves, and if the fit is within machine precision they are replaced.

#### Lines

Line crunching would probably be easiest to start with. It can be done iteratively:

* Start at a point and consider 2 points after that (for a total of 3 points)
* If they can be approximated by a line going through all 3 points, the 2 last points are replaced by the approximation movement
* Use the same starting point and repeat until no more approximations can be done
* Move to the next point and repeat the process

_Do not forget to take all 3 dimensions(`x`,`y`,`z`) into account when approximating!_

### Optimize CNC movement

Do not do the paths in sequence; traced images are scanned top-down and it can get a bit jumpy.

Start from 0,0 and find the closest path. After the path is rendered, find the path closest to the current point. Get distance by bounding box. Pick that path, render and repeat until
all paths are rendered.

## Optimizations

### Dynamic step length

Take tool precision and segment length into account. Step a reasonable distance.

Take the length of the segment and divide by tool precision (or if that is too much - tool precision multiplied by a reduction factor). _Maybe call this tool trace resolution?_

If the length of the segment is 10 and the tool precision is 0.1 we get 10/0.1 = 100 steps.

> This generates an awful lot of coordinates