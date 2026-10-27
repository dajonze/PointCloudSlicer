using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcRt = Autodesk.AutoCAD.Runtime;  // alias to avoid System.Exception ambiguity

namespace PointCloudSlicer
{
    /// <summary>
    /// Helpers for working with PointCloudEx entities in the drawing database.
    /// AutoCAD 2020+ uses PointCloudEx (attached via .rcp / .rcs attachments).
    /// </summary>
    public static class PointCloudHelper
    {
        // -----------------------------------------------------------------------
        // Selection helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Prompt the user to select a single PointCloudEx entity.
        /// Returns the ObjectId, or ObjectId.Null on cancel / wrong selection.
        /// </summary>
        public static ObjectId SelectPointCloud(Editor editor)
        {
            var opts = new PromptEntityOptions("\nSelect point cloud: ");
            opts.SetRejectMessage("\nThat is not a point cloud. Please select a point cloud.");
            opts.AddAllowedClass(typeof(PointCloudEx), false);

            var result = editor.GetEntity(opts);
            if (result.Status != PromptStatus.OK)
                return ObjectId.Null;

            return result.ObjectId;
        }

        // -----------------------------------------------------------------------
        // Clipping plane application
        // -----------------------------------------------------------------------

        /// <summary>
        /// Apply all planes defined in <see cref="PlaneManager"/> to a PointCloudEx entity.
        ///
        /// Clipping planes in AutoCAD's PointCloudEx work as half-spaces:
        ///   • The normal points toward the VISIBLE side.
        ///   • Points on the "back" (opposite normal) side are hidden.
        ///
        /// This method replaces the entity's existing clipping boundary with the
        /// new set of planes.  Existing planar-clipping boundaries are removed
        /// first to avoid accumulation across repeated PCSLICE calls.
        /// </summary>
        public static void ApplyClippingPlanes(
            Transaction tr,
            ObjectId pointCloudId,
            IReadOnlyList<ClippingPlane> planes,
            Editor editor)
        {
            if (planes.Count == 0)
            {
                editor.WriteMessage("\nNo clipping planes defined — nothing to apply.");
                return;
            }

            var pc = tr.GetObject(pointCloudId, OpenMode.ForWrite) as PointCloudEx;
            if (pc == null)
            {
                editor.WriteMessage("\nCould not open point cloud for writing.");
                return;
            }

            // ------------------------------------------------------------------
            // Build a planar PointCloudClippingBoundary for each user plane.
            //
            // PointCloudEx.GetClippingBoundary() / SetClippingBoundary() works
            // with the PointCloudClippingBoundary class.  Each boundary has a
            // type; we use Planar to represent a single half-space.
            //
            // AutoCAD 2020+ API:
            //   pc.ClearClippingBoundaries()
            //   pc.AddClippingBoundary(boundary)
            // ------------------------------------------------------------------

            try
            {
                // Remove all existing clipping boundaries so we start clean.
                pc.ClearClippingBoundaries();

                foreach (var plane in planes)
                {
                    var boundary = new PointCloudClippingBoundary(
                        PointCloudClippingBoundaryType.Planar);

                    // The plane is expressed as a Plane object:
                    //   origin  = any point on the plane
                    //   normal  = the vector pointing toward the KEPT side
                    var acPlane = new Plane(plane.OriginPoint, plane.Normal);
                    boundary.SetPlanarClippingBoundary(acPlane);

                    pc.AddClippingBoundary(boundary);
                }

                editor.WriteMessage($"\n{planes.Count} clipping plane(s) applied to point cloud.");
            }
            catch (AcRt.Exception ex)
                when (ex.ErrorStatus == ErrorStatus.NotImplementedYet
                   || ex.ErrorStatus == ErrorStatus.InvalidInput)
            {
                // Planar clipping boundary API not available on this AutoCAD version.
                // This plugin requires AutoCAD 2020 or later for half-space clipping.
                editor.WriteMessage(
                    "\nERROR: Planar clipping boundary API is not available on this AutoCAD version.");
                editor.WriteMessage(
                    "\nPointCloudSlicer requires AutoCAD 2020 or later. No clipping was applied.");
            }
        }

        // -----------------------------------------------------------------------
        // Plane removal
        // -----------------------------------------------------------------------

        /// <summary>
        /// Remove all clipping boundaries from the selected point cloud.
        /// </summary>
        public static void RemoveAllClipping(Transaction tr, ObjectId pointCloudId, Editor editor)
        {
            var pc = tr.GetObject(pointCloudId, OpenMode.ForWrite) as PointCloudEx;
            if (pc == null)
            {
                editor.WriteMessage("\nCould not open point cloud for writing.");
                return;
            }

            pc.ClearClippingBoundaries();
            editor.WriteMessage("\nAll clipping boundaries removed from point cloud.");
        }

        // -----------------------------------------------------------------------
        // Bounding-box helper (for snapping Z to top/bottom, etc.)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the geometric extents of a point cloud as reported by the entity.
        /// Returns null if the entity has no extents.
        /// </summary>
        public static Extents3d? GetExtents(Transaction tr, ObjectId pointCloudId)
        {
            var pc = tr.GetObject(pointCloudId, OpenMode.ForRead) as PointCloudEx;
            if (pc == null) return null;

            try { return pc.GeometricExtents; }
            catch { return null; }
        }
    }
}
