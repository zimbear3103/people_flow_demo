using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Generates the ordered points of a closed runway loop for a given <see cref="TrackShape"/>.
    /// Points are LOCAL (XZ plane, Y = 0), centred on the origin, and form a closed loop: the path
    /// runs from the last point back to the first (the start point is NOT duplicated at the end).
    ///
    /// Index 0 always sits at the bottom-centre so it lines up with <see cref="RunwayTrack.EntryT"/>
    /// (= 0), where the lanes feed runners in. Straight edges need only their two endpoints — the
    /// track's arc-length engine interpolates between consecutive points — so a rectangle is just a
    /// handful of corner points and naturally produces sharp "kinky" corners. Curves (the oval and
    /// rounded corners) are sampled densely.
    /// </summary>
    public static class TrackPath
    {
        const int CornerArcSegments = 6; // samples per rounded corner (quarter arc)

        /// <summary>Build the loop points for the level's shape. <paramref name="ovalSegments"/> is the
        /// sample count used for the smooth oval.</summary>
        public static List<Vector3> Build(LevelData level, int ovalSegments)
        {
            switch (level.trackShape)
            {
                case TrackShape.Rectangle:
                    return RoundedRect(level.loopWidth, level.loopHeight, level.cornerRadius);

                case TrackShape.Square:
                {
                    float s = Mathf.Min(level.loopWidth, level.loopHeight);
                    return RoundedRect(s, s, level.cornerRadius);
                }

                case TrackShape.Custom:
                    return Custom(level.customWaypoints, level.loopWidth, level.loopHeight, ovalSegments);

                default:
                    return Oval(level.loopWidth, level.loopHeight, ovalSegments);
            }
        }

        // ---- shapes ---------------------------------------------------------

        /// <summary>A smooth ellipse, <paramref name="n"/> points starting at the bottom-centre.</summary>
        static List<Vector3> Oval(float w, float h, int n)
        {
            float a = w * 0.5f, b = h * 0.5f;
            var pts = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                // Start at the bottom-centre (-90°) and wind so x goes positive first (matches the
                // rectangle's winding), so EntryT = 0 sits at the bottom-centre for every shape.
                float ang = -Mathf.PI * 0.5f + Mathf.PI * 2f * (i / (float)n);
                pts.Add(new Vector3(a * Mathf.Cos(ang), 0f, b * Mathf.Sin(ang)));
            }
            return pts;
        }

        /// <summary>A <paramref name="w"/>×<paramref name="h"/> rectangle. <paramref name="radius"/> ≤ 0
        /// gives sharp corners (just the four corners + a bottom-centre start); a positive radius
        /// (clamped to half the shorter side) rounds each corner with a quarter-arc.</summary>
        static List<Vector3> RoundedRect(float w, float h, float radius)
        {
            float a = w * 0.5f, b = h * 0.5f;
            float r = Mathf.Clamp(radius, 0f, Mathf.Min(a, b));

            var pts = new List<Vector3>();
            pts.Add(new Vector3(0f, 0f, -b)); // bottom-centre start (EntryT = 0)

            if (r <= 1e-4f)
            {
                // Sharp "kinky" corners: bottom-right → top-right → top-left → bottom-left, closing
                // back along the bottom-left half to the start.
                pts.Add(new Vector3(a, 0f, -b));
                pts.Add(new Vector3(a, 0f, b));
                pts.Add(new Vector3(-a, 0f, b));
                pts.Add(new Vector3(-a, 0f, -b));
                return pts;
            }

            // Rounded: a quarter-arc at each corner, joined by the straight edges (implicit between
            // consecutive points). Arc centres are inset by r from each corner; the sweep goes the
            // same way the loop winds (bottom-centre → right → top → left).
            AddArc(pts, a - r, -b + r, r, -90f, 0f);   // bottom-right
            AddArc(pts, a - r, b - r, r, 0f, 90f);     // top-right
            AddArc(pts, -a + r, b - r, r, 90f, 180f);  // top-left
            AddArc(pts, -a + r, -b + r, r, 180f, 270f);// bottom-left
            return pts;
        }

        /// <summary>The designer's manual path, copied with Y flattened. Falls back to an oval if too
        /// few points were supplied to form a loop.</summary>
        static List<Vector3> Custom(List<Vector3> waypoints, float w, float h, int ovalSegments)
        {
            if (waypoints == null || waypoints.Count < 3)
            {
                PFLog.Warn("TrackPath: Custom shape needs at least 3 waypoints — falling back to an oval.");
                return Oval(w, h, ovalSegments);
            }

            var pts = new List<Vector3>(waypoints.Count);
            foreach (var p in waypoints) pts.Add(new Vector3(p.x, 0f, p.z));
            return pts;
        }

        // ---- helpers --------------------------------------------------------

        /// <summary>Append a quarter-arc (inclusive of both endpoints) of <paramref name="r"/> around
        /// the inset centre (<paramref name="cx"/>, <paramref name="cz"/>), sweeping from
        /// <paramref name="startDeg"/> to <paramref name="endDeg"/>.</summary>
        static void AddArc(List<Vector3> into, float cx, float cz, float r, float startDeg, float endDeg)
        {
            for (int k = 0; k <= CornerArcSegments; k++)
            {
                float ang = Mathf.Deg2Rad * Mathf.Lerp(startDeg, endDeg, k / (float)CornerArcSegments);
                into.Add(new Vector3(cx + r * Mathf.Cos(ang), 0f, cz + r * Mathf.Sin(ang)));
            }
        }
    }
}
