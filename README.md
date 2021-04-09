# unity-dollar-recognizer
Unity Port of the One Dollar Gesture Recognizer

This is a basic port of the [$1 Unistroke Recognizer](https://depts.washington.edu/acelab/proj/dollar/index.html) (JS Version) to use Unity data structures. The [original code](https://depts.washington.edu/acelab/proj/dollar/dollar.js) is under the New BSD License.

The default 16 demo gestures are included unlike [SteBeeGizmo/DollarUnity](https://github.com/SteBeeGizmo/DollarUnity).

## Usage
1. Initialize `DollarRecognizer dollarRecognizer = new DollarRecognizer();`
2. Collect 2D strokes into a `List<Vector2> points`
3. Call `dollarRecognizer.Recognize(points, useProtractor);`
