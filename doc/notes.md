# Notes

## Bugs

### (DONE) Tool shortcuts on convex corners between two lines

Added spot segment with intermediate normal to try to prevent this. Still generates shortcut.
I think tool must take a circular path between the end of the first(a) line and the start of the second(b).
Trace the tool by stepping the normal from a.normal to b.normal using the spot point.
Spot segment needs two normals, one from a, one from b.

### Bauhaus 93 sample 'B' is inside out

Is it rendered clockwise? Can we see some other indication that this letter is unique. Other letters in sample are OK.

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

## Optimizations

### Dynamic step length

Take tool precision and segment length into account. Step a reasonable distance.

Take the length of the segment and divide by tool precision (or if that is too much - tool precision multiplied by a reduction factor). _Maybe call this tool trace resolution?_

If the length of the segment is 10 and the tool precision is 0.1 we get 10/0.1 = 100 steps.
