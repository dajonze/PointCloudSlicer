using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.AutoCAD.Geometry;

namespace PointCloudSlicer
{
    /// <summary>
    /// Axis for a clipping plane.
    /// </summary>
    public enum ClipAxis { X, Y, Z }

    /// <summary>
    /// A single user-defined clipping plane.
    /// </summary>
    public class ClippingPlane
    {
        /// <summary>Axis this plane is perpendicular to.</summary>
        public ClipAxis Axis { get; }

        /// <summary>The coordinate value along the axis.</summary>
        public double Value { get; }

        /// <summary>
        /// Normal direction. "Positive" means we keep the side the normal points toward.
        /// When true, normal faces the positive axis direction; when false, it faces negative.
        /// This controls which side of the plane is kept visible.
        /// </summary>
        public bool KeepPositiveSide { get; }

        public ClippingPlane(ClipAxis axis, double value, bool keepPositiveSide = true)
        {
            Axis = axis;
            Value = value;
            KeepPositiveSide = keepPositiveSide;
        }

        /// <summary>
        /// Point on the plane (used as the plane origin).
        /// </summary>
        public Point3d OriginPoint => Axis switch
        {
            ClipAxis.X => new Point3d(Value, 0, 0),
            ClipAxis.Y => new Point3d(0, Value, 0),
            ClipAxis.Z => new Point3d(0, 0, Value),
            _           => Point3d.Origin,
        };

        /// <summary>
        /// Outward normal of the plane — points toward the side that IS visible.
        /// </summary>
        public Vector3d Normal => Axis switch
        {
            ClipAxis.X => KeepPositiveSide ? new Vector3d( 1, 0, 0) : new Vector3d(-1, 0, 0),
            ClipAxis.Y => KeepPositiveSide ? new Vector3d( 0, 1, 0) : new Vector3d( 0,-1, 0),
            ClipAxis.Z => KeepPositiveSide ? new Vector3d( 0, 0, 1) : new Vector3d( 0, 0,-1),
            _           => Vector3d.ZAxis,
        };

        public override string ToString()
        {
            string side = KeepPositiveSide ? "+" : "-";
            return $"{Axis} = {Value:F4}  (keep {side} side)";
        }
    }

    /// <summary>
    /// Singleton that stores the planes the user has defined this session.
    /// Planes persist until PCSLICERESET or AutoCAD is closed.
    /// </summary>
    public static class PlaneManager
    {
        private static readonly List<ClippingPlane> _planes = new();

        public static IReadOnlyList<ClippingPlane> Planes => _planes.AsReadOnly();

        /// <summary>
        /// Add a plane. If a plane on the same axis with the same keepPositiveSide sense
        /// already exists, it is replaced (so you always have at most one +X, one -X, etc.).
        /// </summary>
        public static void AddOrReplace(ClipAxis axis, double value, bool keepPositiveSide)
        {
            // Remove existing plane with same axis + direction
            _planes.RemoveAll(p => p.Axis == axis && p.KeepPositiveSide == keepPositiveSide);
            _planes.Add(new ClippingPlane(axis, value, keepPositiveSide));
        }

        /// <summary>
        /// Convenience: define a slice band on one axis (two planes, min and max).
        /// Removes any existing planes on that axis.
        /// </summary>
        public static void SetBand(ClipAxis axis, double minValue, double maxValue)
        {
            _planes.RemoveAll(p => p.Axis == axis);
            // Keep the positive side at minValue  →  min boundary
            _planes.Add(new ClippingPlane(axis, minValue, keepPositiveSide: true));
            // Keep the negative side at maxValue  →  max boundary
            _planes.Add(new ClippingPlane(axis, maxValue, keepPositiveSide: false));
        }

        public static void Clear() => _planes.Clear();

        public static string StatusText()
        {
            if (_planes.Count == 0)
                return "No clipping planes defined.";

            var sb = new StringBuilder();
            sb.AppendLine($"  {_planes.Count} plane(s) defined:");
            foreach (var p in _planes)
                sb.AppendLine($"    {p}");
            return sb.ToString();
        }
    }
}
