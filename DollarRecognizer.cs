using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Point = UnityEngine.Vector2;

public class DollarRecognizer
{
    // Port of https://depts.washington.edu/acelab/proj/dollar/dollar.js

    const int numPoints = 64;
    const float squareSize = 250f;
    static readonly Vector2 origin = Vector2.zero;
    static readonly float diagonal = Mathf.Sqrt(squareSize * squareSize + squareSize * squareSize);
    static readonly float halfDiagonal = 0.5f * diagonal;
    const float angleRange = Mathf.Deg2Rad * 45f;
    const float anglePrecision = Mathf.Deg2Rad * 2f;
    static readonly float phi = 0.5f * (-1f + Mathf.Sqrt(5f));
    List<Unistroke> unistrokes = new List<Unistroke>();

    public DollarRecognizer()
    {
        unistrokes = DEFAULT_UNISTROKES.ToList();
    }

    public class Unistroke
    {
        public string name;
        public Vector2[] points;
        public float[] vector;

        public Unistroke(string name, Vector2[] points)
        {
            this.name = name;
            this.points = Resample(points.ToList(), numPoints).ToArray();
            float radians = IndicativeAngle(this.points);
            this.points = RotateBy(this.points, -radians);
            this.points = ScaleTo(this.points, squareSize);
            this.points = TranslateTo(this.points, origin);
            this.vector = Vectorize(this.points);
        }
    }

    public struct Result
    {
        public string name;
        public float score;
        public TimeSpan time;

        public Result(string name, float score, TimeSpan time)
        {
            this.name = name;
            this.score = score;
            this.time = time;
        }
    }

    public Result Recognize(List<Vector2> points, bool useProtractor)
    {
        DateTime t0 = DateTime.Now;
        Unistroke candidate = new Unistroke("", points.ToArray());

        int u = -1;
        float b = float.MaxValue;
        for (int i = 0; i < unistrokes.Count; i++)
        {
            float d;
            if (useProtractor)
            {
                d = OptimalCosineDistance(unistrokes[i].vector, candidate.vector);
            }
            else
            {
                d = DistanceAtBestAngle(candidate.points, unistrokes[i], -angleRange, angleRange, anglePrecision);
            }
            if (d < b)
            {
                b = d;
                u = i;
            }
        }
        DateTime t1 = DateTime.Now;
        return (u == -1) ? new Result("No match.", 0f, t1 - t0) : new Result(this.unistrokes[u].name, useProtractor ? (1f - b) : (1f - b / halfDiagonal), t1 - t0);
    }

    public int AddGesture(string name, Vector2[] points)
    {
        unistrokes.Add(new Unistroke(name, points));
        int num = 0;
        for (int i = 0; i < unistrokes.Count; i++)
        {
            if (unistrokes[i].name == name)
            {
                num++;
            }
        }
        return num;
    }

    public int DeleteUserGestures()
    {
        int numUnistrokes = DEFAULT_UNISTROKES.Length;
        unistrokes.RemoveRange(numUnistrokes, unistrokes.Count - numUnistrokes);
        return unistrokes.Count;
    }

    static public List<Vector2> Resample(List<Point> points, int n)
    {
        float intervalLength = PathLength(points.ToArray()) / (n - 1);
        float capitalD = 0f;
        List<Vector2> newPoints = new List<Vector2>();
        newPoints.Add(points[0]);
        for (int i = 1; i < points.Count; i++)
        {
            float d = Vector2.Distance(points[i - 1], points[i]);
            if ((capitalD + d) >= intervalLength)
            {
                float qx = points[i - 1].x + ((intervalLength - capitalD) / d) * (points[i].x - points[i - 1].x);
                float qy = points[i - 1].y + ((intervalLength - capitalD) / d) * (points[i].y - points[i - 1].y);
                Vector2 q = new Vector2(qx, qy);
                newPoints.Add(q);
                points.Insert(i, q);
                capitalD = 0f;
            }
            else
            {
                capitalD += d;
            }
        }
        if (newPoints.Count == n - 1)
        {
            newPoints.Add(points[points.Count - 1]);
        }
        return newPoints;
    }

    static float IndicativeAngle(Vector2[] points)
    {
        Vector2 c = Centroid(points);
        return Mathf.Atan2(c.y - points[0].y, c.x - points[0].x);
    }

    static Vector2[] RotateBy(Vector2[] points, float radians)
    {
        Vector2 c = Centroid(points);
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        Vector2[] newPoints = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            float qx = (points[i].x - c.x) * cos - (points[i].y - c.y) * sin + c.x;
            float qy = (points[i].x - c.x) * sin + (points[i].y - c.y) * cos + c.y;
            newPoints[i].Set(qx, qy);
        }
        return newPoints;
    }

    static Vector2[] ScaleTo(Vector2[] points, float size)
    {
        Rect b = BoundingBox(points);
        Vector2[] newPoints = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            float qx = points[i].x * size / b.width;
            float qy = points[i].y * size / b.height;
            newPoints[i].Set(qx, qy);
        }
        return newPoints;
    }

    /// <summary>
    /// Moves points such that the centroid is pt.
    /// </summary>
    /// <param name="points">Array of points</param>
    /// <param name="pt">New centroid</param>
    /// <returns>Shifted array of points</returns>
    static Vector2[] TranslateTo(Vector2[] points, Vector2 pt)
    {
        Vector2 c = Centroid(points);
        Vector2[] newPoints = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            newPoints[i] = points[i] + pt - c;
        }
        return newPoints;
    }

    static float[] Vectorize(Vector2[] points)
    {
        float sum = 0f;
        float[] vector = new float[2 * points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            vector[2 * i] = points[i].x;
            vector[2 * i + 1] = points[i].y;
            sum += points[i].sqrMagnitude;
        }
        float magnitude = Mathf.Sqrt(sum);
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] /= magnitude;
        }
        return vector;
    }

    static public float OptimalCosineDistance(float[] v1, float[] v2)
    {
        float a = 0f;
        float b = 0f;
        for (int i = 0; i < v1.Length; i += 2)
        {
            a += v1[i] * v2[i] + v1[i + 1] * v2[i + 1];
            b += v1[i] * v2[i + 1] - v1[i + 1] * v2[i];
        }
        float angle = Mathf.Atan(b / a);
        return Mathf.Acos(a * Mathf.Cos(angle) + b * Mathf.Sin(angle));
    }

    static public float DistanceAtBestAngle(Vector2[] points, Unistroke target, float a, float b, float threshold)
    {
        float x1 = Mathf.Lerp(b, a, phi);
        float f1 = DistanceAtAngle(points, target, x1);
        float x2 = Mathf.Lerp(a, b, phi);
        float f2 = DistanceAtAngle(points, target, x2);
        while (Mathf.Abs(b - a) > threshold)
        {
            if (f1 < f2)
            {
                b = x2;
                x2 = x1;
                f2 = f1;
                x1 = Mathf.Lerp(b, a, phi);
                f1 = DistanceAtAngle(points, target, x1);
            }
            else
            {
                a = x1;
                x1 = x2;
                f1 = f2;
                x2 = Mathf.Lerp(a, b, phi);
                f2 = DistanceAtAngle(points, target, x2);
            }
        }
        return Mathf.Min(f1, f2);
    }

    static public float DistanceAtAngle(Vector2[] points, Unistroke target, float radians)
    {
        Vector2[] newPoints = RotateBy(points, radians);
        return PathDistance(newPoints, target.points);
    }

    static public Vector2 Centroid(Vector2[] points)
    {
        Vector2 mean = Vector2.zero;
        foreach (Vector2 point in points)
        {
            mean += point;
        }
        mean = mean / points.Length;
        return mean;
    }

    static public Rect BoundingBox(Vector2[] points)
    {
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int i = 0; i < points.Length; i++)
        {
            minX = Math.Min(minX, points[i].x);
            maxX = Math.Max(maxX, points[i].x);
            minY = Math.Min(minY, points[i].y);
            maxY = Math.Max(maxY, points[i].y);
        }
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    static float PathDistance(Vector2[] pts1, Vector2[] pts2)
    {
        float d = 0f;
        for (int i = 0; i < pts1.Length; i++)
        {
            d += Vector2.Distance(pts1[i], pts2[i]);
        }
        return d;
    }

    static float PathLength(Vector2[] points)
    {
        float d = 0f;
        for (int i = 1; i < points.Length; i++)
        {
            d += Vector2.Distance(points[i - 1], points[i]);
        }
        return d;
    }

    static readonly Unistroke[] DEFAULT_UNISTROKES = new Unistroke[]
    {
        new Unistroke("triangle", new Point[]{new Point(137,139),new Point(135,141),new Point(133,144),new Point(132,146),new Point(130,149),new Point(128,151),new Point(126,155),new Point(123,160),new Point(120,166),new Point(116,171),new Point(112,177),new Point(107,183),new Point(102,188),new Point(100,191),new Point(95,195),new Point(90,199),new Point(86,203),new Point(82,206),new Point(80,209),new Point(75,213),new Point(73,213),new Point(70,216),new Point(67,219),new Point(64,221),new Point(61,223),new Point(60,225),new Point(62,226),new Point(65,225),new Point(67,226),new Point(74,226),new Point(77,227),new Point(85,229),new Point(91,230),new Point(99,231),new Point(108,232),new Point(116,233),new Point(125,233),new Point(134,234),new Point(145,233),new Point(153,232),new Point(160,233),new Point(170,234),new Point(177,235),new Point(179,236),new Point(186,237),new Point(193,238),new Point(198,239),new Point(200,237),new Point(202,239),new Point(204,238),new Point(206,234),new Point(205,230),new Point(202,222),new Point(197,216),new Point(192,207),new Point(186,198),new Point(179,189),new Point(174,183),new Point(170,178),new Point(164,171),new Point(161,168),new Point(154,160),new Point(148,155),new Point(143,150),new Point(138,148),new Point(136,148)}),
        new Unistroke("x", new Point[]{new Point(87,142),new Point(89,145),new Point(91,148),new Point(93,151),new Point(96,155),new Point(98,157),new Point(100,160),new Point(102,162),new Point(106,167),new Point(108,169),new Point(110,171),new Point(115,177),new Point(119,183),new Point(123,189),new Point(127,193),new Point(129,196),new Point(133,200),new Point(137,206),new Point(140,209),new Point(143,212),new Point(146,215),new Point(151,220),new Point(153,222),new Point(155,223),new Point(157,225),new Point(158,223),new Point(157,218),new Point(155,211),new Point(154,208),new Point(152,200),new Point(150,189),new Point(148,179),new Point(147,170),new Point(147,158),new Point(147,148),new Point(147,141),new Point(147,136),new Point(144,135),new Point(142,137),new Point(140,139),new Point(135,145),new Point(131,152),new Point(124,163),new Point(116,177),new Point(108,191),new Point(100,206),new Point(94,217),new Point(91,222),new Point(89,225),new Point(87,226),new Point(87,224) }),
        new Unistroke("rectangle", new Point[]{new Point(78,149),new Point(78,153),new Point(78,157),new Point(78,160),new Point(79,162),new Point(79,164),new Point(79,167),new Point(79,169),new Point(79,173),new Point(79,178),new Point(79,183),new Point(80,189),new Point(80,193),new Point(80,198),new Point(80,202),new Point(81,208),new Point(81,210),new Point(81,216),new Point(82,222),new Point(82,224),new Point(82,227),new Point(83,229),new Point(83,231),new Point(85,230),new Point(88,232),new Point(90,233),new Point(92,232),new Point(94,233),new Point(99,232),new Point(102,233),new Point(106,233),new Point(109,234),new Point(117,235),new Point(123,236),new Point(126,236),new Point(135,237),new Point(142,238),new Point(145,238),new Point(152,238),new Point(154,239),new Point(165,238),new Point(174,237),new Point(179,236),new Point(186,235),new Point(191,235),new Point(195,233),new Point(197,233),new Point(200,233),new Point(201,235),new Point(201,233),new Point(199,231),new Point(198,226),new Point(198,220),new Point(196,207),new Point(195,195),new Point(195,181),new Point(195,173),new Point(195,163),new Point(194,155),new Point(192,145),new Point(192,143),new Point(192,138),new Point(191,135),new Point(191,133),new Point(191,130),new Point(190,128),new Point(188,129),new Point(186,129),new Point(181,132),new Point(173,131),new Point(162,131),new Point(151,132),new Point(149,132),new Point(138,132),new Point(136,132),new Point(122,131),new Point(120,131),new Point(109,130),new Point(107,130),new Point(90,132),new Point(81,133),new Point(76,133)}),
        new Unistroke("circle", new Point[]{new Point(127,141),new Point(124,140),new Point(120,139),new Point(118,139),new Point(116,139),new Point(111,140),new Point(109,141),new Point(104,144),new Point(100,147),new Point(96,152),new Point(93,157),new Point(90,163),new Point(87,169),new Point(85,175),new Point(83,181),new Point(82,190),new Point(82,195),new Point(83,200),new Point(84,205),new Point(88,213),new Point(91,216),new Point(96,219),new Point(103,222),new Point(108,224),new Point(111,224),new Point(120,224),new Point(133,223),new Point(142,222),new Point(152,218),new Point(160,214),new Point(167,210),new Point(173,204),new Point(178,198),new Point(179,196),new Point(182,188),new Point(182,177),new Point(178,167),new Point(170,150),new Point(163,138),new Point(152,130),new Point(143,129),new Point(140,131),new Point(129,136),new Point(126,139)}),
        new Unistroke("check", new Point[]{new Point(91,185),new Point(93,185),new Point(95,185),new Point(97,185),new Point(100,188),new Point(102,189),new Point(104,190),new Point(106,193),new Point(108,195),new Point(110,198),new Point(112,201),new Point(114,204),new Point(115,207),new Point(117,210),new Point(118,212),new Point(120,214),new Point(121,217),new Point(122,219),new Point(123,222),new Point(124,224),new Point(126,226),new Point(127,229),new Point(129,231),new Point(130,233),new Point(129,231),new Point(129,228),new Point(129,226),new Point(129,224),new Point(129,221),new Point(129,218),new Point(129,212),new Point(129,208),new Point(130,198),new Point(132,189),new Point(134,182),new Point(137,173),new Point(143,164),new Point(147,157),new Point(151,151),new Point(155,144),new Point(161,137),new Point(165,131),new Point(171,122),new Point(174,118),new Point(176,114),new Point(177,112),new Point(177,114),new Point(175,116),new Point(173,118)}),
        new Unistroke("caret", new Point[]{new Point(79,245),new Point(79,242),new Point(79,239),new Point(80,237),new Point(80,234),new Point(81,232),new Point(82,230),new Point(84,224),new Point(86,220),new Point(86,218),new Point(87,216),new Point(88,213),new Point(90,207),new Point(91,202),new Point(92,200),new Point(93,194),new Point(94,192),new Point(96,189),new Point(97,186),new Point(100,179),new Point(102,173),new Point(105,165),new Point(107,160),new Point(109,158),new Point(112,151),new Point(115,144),new Point(117,139),new Point(119,136),new Point(119,134),new Point(120,132),new Point(121,129),new Point(122,127),new Point(124,125),new Point(126,124),new Point(129,125),new Point(131,127),new Point(132,130),new Point(136,139),new Point(141,154),new Point(145,166),new Point(151,182),new Point(156,193),new Point(157,196),new Point(161,209),new Point(162,211),new Point(167,223),new Point(169,229),new Point(170,231),new Point(173,237),new Point(176,242),new Point(177,244),new Point(179,250),new Point(181,255),new Point(182,257)}),
        new Unistroke("zig-zag", new Point[]{new Point(307,216),new Point(333,186),new Point(356,215),new Point(375,186),new Point(399,216),new Point(418,186)}),
        new Unistroke("arrow", new Point[]{new Point(68,222),new Point(70,220),new Point(73,218),new Point(75,217),new Point(77,215),new Point(80,213),new Point(82,212),new Point(84,210),new Point(87,209),new Point(89,208),new Point(92,206),new Point(95,204),new Point(101,201),new Point(106,198),new Point(112,194),new Point(118,191),new Point(124,187),new Point(127,186),new Point(132,183),new Point(138,181),new Point(141,180),new Point(146,178),new Point(154,173),new Point(159,171),new Point(161,170),new Point(166,167),new Point(168,167),new Point(171,166),new Point(174,164),new Point(177,162),new Point(180,160),new Point(182,158),new Point(183,156),new Point(181,154),new Point(178,153),new Point(171,153),new Point(164,153),new Point(160,153),new Point(150,154),new Point(147,155),new Point(141,157),new Point(137,158),new Point(135,158),new Point(137,158),new Point(140,157),new Point(143,156),new Point(151,154),new Point(160,152),new Point(170,149),new Point(179,147),new Point(185,145),new Point(192,144),new Point(196,144),new Point(198,144),new Point(200,144),new Point(201,147),new Point(199,149),new Point(194,157),new Point(191,160),new Point(186,167),new Point(180,176),new Point(177,179),new Point(171,187),new Point(169,189),new Point(165,194),new Point(164,196)}),
        new Unistroke("left square bracket", new Point[]{new Point(140,124),new Point(138,123),new Point(135,122),new Point(133,123),new Point(130,123),new Point(128,124),new Point(125,125),new Point(122,124),new Point(120,124),new Point(118,124),new Point(116,125),new Point(113,125),new Point(111,125),new Point(108,124),new Point(106,125),new Point(104,125),new Point(102,124),new Point(100,123),new Point(98,123),new Point(95,124),new Point(93,123),new Point(90,124),new Point(88,124),new Point(85,125),new Point(83,126),new Point(81,127),new Point(81,129),new Point(82,131),new Point(82,134),new Point(83,138),new Point(84,141),new Point(84,144),new Point(85,148),new Point(85,151),new Point(86,156),new Point(86,160),new Point(86,164),new Point(86,168),new Point(87,171),new Point(87,175),new Point(87,179),new Point(87,182),new Point(87,186),new Point(88,188),new Point(88,195),new Point(88,198),new Point(88,201),new Point(88,207),new Point(89,211),new Point(89,213),new Point(89,217),new Point(89,222),new Point(88,225),new Point(88,229),new Point(88,231),new Point(88,233),new Point(88,235),new Point(89,237),new Point(89,240),new Point(89,242),new Point(91,241),new Point(94,241),new Point(96,240),new Point(98,239),new Point(105,240),new Point(109,240),new Point(113,239),new Point(116,240),new Point(121,239),new Point(130,240),new Point(136,237),new Point(139,237),new Point(144,238),new Point(151,237),new Point(157,236),new Point(159,237)}),
        new Unistroke("right square bracket", new Point[]{new Point(112,138),new Point(112,136),new Point(115,136),new Point(118,137),new Point(120,136),new Point(123,136),new Point(125,136),new Point(128,136),new Point(131,136),new Point(134,135),new Point(137,135),new Point(140,134),new Point(143,133),new Point(145,132),new Point(147,132),new Point(149,132),new Point(152,132),new Point(153,134),new Point(154,137),new Point(155,141),new Point(156,144),new Point(157,152),new Point(158,161),new Point(160,170),new Point(162,182),new Point(164,192),new Point(166,200),new Point(167,209),new Point(168,214),new Point(168,216),new Point(169,221),new Point(169,223),new Point(169,228),new Point(169,231),new Point(166,233),new Point(164,234),new Point(161,235),new Point(155,236),new Point(147,235),new Point(140,233),new Point(131,233),new Point(124,233),new Point(117,235),new Point(114,238),new Point(112,238)}),
        new Unistroke("v", new Point[]{new Point(89,164),new Point(90,162),new Point(92,162),new Point(94,164),new Point(95,166),new Point(96,169),new Point(97,171),new Point(99,175),new Point(101,178),new Point(103,182),new Point(106,189),new Point(108,194),new Point(111,199),new Point(114,204),new Point(117,209),new Point(119,214),new Point(122,218),new Point(124,222),new Point(126,225),new Point(128,228),new Point(130,229),new Point(133,233),new Point(134,236),new Point(136,239),new Point(138,240),new Point(139,242),new Point(140,244),new Point(142,242),new Point(142,240),new Point(142,237),new Point(143,235),new Point(143,233),new Point(145,229),new Point(146,226),new Point(148,217),new Point(149,208),new Point(149,205),new Point(151,196),new Point(151,193),new Point(153,182),new Point(155,172),new Point(157,165),new Point(159,160),new Point(162,155),new Point(164,150),new Point(165,148),new Point(166,146)}),
        new Unistroke("delete", new Point[]{new Point(123,129),new Point(123,131),new Point(124,133),new Point(125,136),new Point(127,140),new Point(129,142),new Point(133,148),new Point(137,154),new Point(143,158),new Point(145,161),new Point(148,164),new Point(153,170),new Point(158,176),new Point(160,178),new Point(164,183),new Point(168,188),new Point(171,191),new Point(175,196),new Point(178,200),new Point(180,202),new Point(181,205),new Point(184,208),new Point(186,210),new Point(187,213),new Point(188,215),new Point(186,212),new Point(183,211),new Point(177,208),new Point(169,206),new Point(162,205),new Point(154,207),new Point(145,209),new Point(137,210),new Point(129,214),new Point(122,217),new Point(118,218),new Point(111,221),new Point(109,222),new Point(110,219),new Point(112,217),new Point(118,209),new Point(120,207),new Point(128,196),new Point(135,187),new Point(138,183),new Point(148,167),new Point(157,153),new Point(163,145),new Point(165,142),new Point(172,133),new Point(177,127),new Point(179,127),new Point(180,125)}),
        new Unistroke("left curly brace", new Point[]{new Point(150,116),new Point(147,117),new Point(145,116),new Point(142,116),new Point(139,117),new Point(136,117),new Point(133,118),new Point(129,121),new Point(126,122),new Point(123,123),new Point(120,125),new Point(118,127),new Point(115,128),new Point(113,129),new Point(112,131),new Point(113,134),new Point(115,134),new Point(117,135),new Point(120,135),new Point(123,137),new Point(126,138),new Point(129,140),new Point(135,143),new Point(137,144),new Point(139,147),new Point(141,149),new Point(140,152),new Point(139,155),new Point(134,159),new Point(131,161),new Point(124,166),new Point(121,166),new Point(117,166),new Point(114,167),new Point(112,166),new Point(114,164),new Point(116,163),new Point(118,163),new Point(120,162),new Point(122,163),new Point(125,164),new Point(127,165),new Point(129,166),new Point(130,168),new Point(129,171),new Point(127,175),new Point(125,179),new Point(123,184),new Point(121,190),new Point(120,194),new Point(119,199),new Point(120,202),new Point(123,207),new Point(127,211),new Point(133,215),new Point(142,219),new Point(148,220),new Point(151,221)}),
        new Unistroke("right curly brace", new Point[]{new Point(117,132),new Point(115,132),new Point(115,129),new Point(117,129),new Point(119,128),new Point(122,127),new Point(125,127),new Point(127,127),new Point(130,127),new Point(133,129),new Point(136,129),new Point(138,130),new Point(140,131),new Point(143,134),new Point(144,136),new Point(145,139),new Point(145,142),new Point(145,145),new Point(145,147),new Point(145,149),new Point(144,152),new Point(142,157),new Point(141,160),new Point(139,163),new Point(137,166),new Point(135,167),new Point(133,169),new Point(131,172),new Point(128,173),new Point(126,176),new Point(125,178),new Point(125,180),new Point(125,182),new Point(126,184),new Point(128,187),new Point(130,187),new Point(132,188),new Point(135,189),new Point(140,189),new Point(145,189),new Point(150,187),new Point(155,186),new Point(157,185),new Point(159,184),new Point(156,185),new Point(154,185),new Point(149,185),new Point(145,187),new Point(141,188),new Point(136,191),new Point(134,191),new Point(131,192),new Point(129,193),new Point(129,195),new Point(129,197),new Point(131,200),new Point(133,202),new Point(136,206),new Point(139,211),new Point(142,215),new Point(145,220),new Point(147,225),new Point(148,231),new Point(147,239),new Point(144,244),new Point(139,248),new Point(134,250),new Point(126,253),new Point(119,253),new Point(115,253)}),
        new Unistroke("star", new Point[]{new Point(75,250),new Point(75,247),new Point(77,244),new Point(78,242),new Point(79,239),new Point(80,237),new Point(82,234),new Point(82,232),new Point(84,229),new Point(85,225),new Point(87,222),new Point(88,219),new Point(89,216),new Point(91,212),new Point(92,208),new Point(94,204),new Point(95,201),new Point(96,196),new Point(97,194),new Point(98,191),new Point(100,185),new Point(102,178),new Point(104,173),new Point(104,171),new Point(105,164),new Point(106,158),new Point(107,156),new Point(107,152),new Point(108,145),new Point(109,141),new Point(110,139),new Point(112,133),new Point(113,131),new Point(116,127),new Point(117,125),new Point(119,122),new Point(121,121),new Point(123,120),new Point(125,122),new Point(125,125),new Point(127,130),new Point(128,133),new Point(131,143),new Point(136,153),new Point(140,163),new Point(144,172),new Point(145,175),new Point(151,189),new Point(156,201),new Point(161,213),new Point(166,225),new Point(169,233),new Point(171,236),new Point(174,243),new Point(177,247),new Point(178,249),new Point(179,251),new Point(180,253),new Point(180,255),new Point(179,257),new Point(177,257),new Point(174,255),new Point(169,250),new Point(164,247),new Point(160,245),new Point(149,238),new Point(138,230),new Point(127,221),new Point(124,220),new Point(112,212),new Point(110,210),new Point(96,201),new Point(84,195),new Point(74,190),new Point(64,182),new Point(55,175),new Point(51,172),new Point(49,170),new Point(51,169),new Point(56,169),new Point(66,169),new Point(78,168),new Point(92,166),new Point(107,164),new Point(123,161),new Point(140,162),new Point(156,162),new Point(171,160),new Point(173,160),new Point(186,160),new Point(195,160),new Point(198,161),new Point(203,163),new Point(208,163),new Point(206,164),new Point(200,167),new Point(187,172),new Point(174,179),new Point(172,181),new Point(153,192),new Point(137,201),new Point(123,211),new Point(112,220),new Point(99,229),new Point(90,237),new Point(80,244),new Point(73,250),new Point(69,254),new Point(69,252)}),
        new Unistroke("pigtail", new Point[]{new Point(81,219),new Point(84,218),new Point(86,220),new Point(88,220),new Point(90,220),new Point(92,219),new Point(95,220),new Point(97,219),new Point(99,220),new Point(102,218),new Point(105,217),new Point(107,216),new Point(110,216),new Point(113,214),new Point(116,212),new Point(118,210),new Point(121,208),new Point(124,205),new Point(126,202),new Point(129,199),new Point(132,196),new Point(136,191),new Point(139,187),new Point(142,182),new Point(144,179),new Point(146,174),new Point(148,170),new Point(149,168),new Point(151,162),new Point(152,160),new Point(152,157),new Point(152,155),new Point(152,151),new Point(152,149),new Point(152,146),new Point(149,142),new Point(148,139),new Point(145,137),new Point(141,135),new Point(139,135),new Point(134,136),new Point(130,140),new Point(128,142),new Point(126,145),new Point(122,150),new Point(119,158),new Point(117,163),new Point(115,170),new Point(114,175),new Point(117,184),new Point(120,190),new Point(125,199),new Point(129,203),new Point(133,208),new Point(138,213),new Point(145,215),new Point(155,218),new Point(164,219),new Point(166,219),new Point(177,219),new Point(182,218),new Point(192,216),new Point(196,213),new Point(199,212),new Point(201,211)})
    };
}
