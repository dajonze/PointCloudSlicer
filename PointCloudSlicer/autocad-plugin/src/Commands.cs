using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace PointCloudSlicer
{
    /// <summary>
    /// AutoCAD commands exposed by the plugin.
    ///
    ///   PCPICKX  — define a clipping plane perpendicular to X
    ///   PCPICKY  — define a clipping plane perpendicular to Y
    ///   PCPICKZ  — define a clipping plane perpendicular to Z
    ///   PCSLICE  — apply all defined planes to a point cloud
    ///   PCSLICEBAND — define a min/max band on one axis and apply it
    ///   PCREMOVECLIP — remove all clipping from a selected point cloud
    ///   PCSLICERESET — clear all stored planes (does not touch drawings)
    ///   PCSTATUS — show currently stored planes
    /// </summary>
    public class Commands
    {
        // -----------------------------------------------------------------------
        // PCPICKX — pick a point and define an X-axis clipping plane
        // -----------------------------------------------------------------------
        [CommandMethod("PCPICKX", CommandFlags.Modal)]
        public void PickX()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed  = doc.Editor;

            ed.WriteMessage(
                "\nDefine X clipping plane. Pick a point — the X coordinate will be used.");
            ed.WriteMessage(
                "\n  Tip: snap to a point on the point cloud to align with existing geometry.");

            var pt = GetPoint(ed, "\nPick point for X plane: ");
            if (pt == null) return;

            bool? keepPos = AskSide(ed, "X", pt.Value.X);
            if (keepPos == null) { ed.WriteMessage("\nCancelled."); return; }

            PlaneManager.AddOrReplace(ClipAxis.X, pt.Value.X, keepPos.Value);

            string sideLabel = keepPos.Value ? "positive (+X)" : "negative (−X)";
            ed.WriteMessage(
                $"\nX plane set at X = {pt.Value.X:F4}. Keeping {sideLabel} side.");
            ed.WriteMessage(PlaneManager.StatusText());
        }

        // -----------------------------------------------------------------------
        // PCPICKY — pick a point and define a Y-axis clipping plane
        // -----------------------------------------------------------------------
        [CommandMethod("PCPICKY", CommandFlags.Modal)]
        public void PickY()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed  = doc.Editor;

            ed.WriteMessage("\nDefine Y clipping plane.");

            var pt = GetPoint(ed, "\nPick point for Y plane: ");
            if (pt == null) return;

            bool? keepPos = AskSide(ed, "Y", pt.Value.Y);
            if (keepPos == null) { ed.WriteMessage("\nCancelled."); return; }

            PlaneManager.AddOrReplace(ClipAxis.Y, pt.Value.Y, keepPos.Value);

            string sideLabel = keepPos.Value ? "positive (+Y)" : "negative (−Y)";
            ed.WriteMessage(
                $"\nY plane set at Y = {pt.Value.Y:F4}. Keeping {sideLabel} side.");
            ed.WriteMessage(PlaneManager.StatusText());
        }

        // -----------------------------------------------------------------------
        // PCPICKZ — pick a point and define a Z-axis clipping plane
        // -----------------------------------------------------------------------
        [CommandMethod("PCPICKZ", CommandFlags.Modal)]
        public void PickZ()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed  = doc.Editor;

            ed.WriteMessage("\nDefine Z clipping plane (horizontal slice).");

            var pt = GetPoint(ed, "\nPick point for Z plane: ");
            if (pt == null) return;

            bool? keepPos = AskSide(ed, "Z", pt.Value.Z);
            if (keepPos == null) { ed.WriteMessage("\nCancelled."); return; }

            PlaneManager.AddOrReplace(ClipAxis.Z, pt.Value.Z, keepPos.Value);

            string sideLabel = keepPos.Value ? "above" : "below";
            ed.WriteMessage(
                $"\nZ plane set at Z = {pt.Value.Z:F4}. Keeping {sideLabel} (Z {(keepPos.Value ? ">" : "<")} {pt.Value.Z:F4}).");
            ed.WriteMessage(PlaneManager.StatusText());
        }

        // -----------------------------------------------------------------------
        // PCSLICE — apply all stored planes to a selected point cloud
        // -----------------------------------------------------------------------
        [CommandMethod("PCSLICE", CommandFlags.Modal)]
        public void Slice()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db  = doc.Database;
            var ed  = doc.Editor;

            if (PlaneManager.Planes.Count == 0)
            {
                ed.WriteMessage(
                    "\nNo clipping planes defined. Use PCPICKX, PCPICKY, or PCPICKZ first.");
                return;
            }

            ed.WriteMessage($"\n{PlaneManager.Planes.Count} plane(s) will be applied:");
            ed.WriteMessage(PlaneManager.StatusText());

            var id = PointCloudHelper.SelectPointCloud(ed);
            if (id == ObjectId.Null)
            {
                ed.WriteMessage("\nSelection cancelled.");
                return;
            }

            using var tr = db.TransactionManager.StartTransaction();
            PointCloudHelper.ApplyClippingPlanes(tr, id, PlaneManager.Planes, ed);
            tr.Commit();
        }

        // -----------------------------------------------------------------------
        // PCSLICEBAND — define a min/max slab on a single axis and apply it
        // -----------------------------------------------------------------------
        [CommandMethod("PCSLICEBAND", CommandFlags.Modal)]
        public void SliceBand()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db  = doc.Database;
            var ed  = doc.Editor;

            // Choose axis
            var axisOpt = new PromptKeywordOptions(
                "\nChoose axis for band slice [X/Y/Z]", "X Y Z");
            axisOpt.Keywords.Default = "Z";
            var axisRes = ed.GetKeywords(axisOpt);
            if (axisRes.Status != PromptStatus.OK) return;

            var axis = axisRes.StringResult switch
            {
                "X" => ClipAxis.X,
                "Y" => ClipAxis.Y,
                _   => ClipAxis.Z,
            };

            string axisName = axis.ToString();

            ed.WriteMessage($"\nPick the MINIMUM {axisName} boundary point:");
            var minPt = GetPoint(ed, $"\nPick min {axisName} point: ");
            if (minPt == null) return;

            ed.WriteMessage($"\nPick the MAXIMUM {axisName} boundary point:");
            var maxPt = GetPoint(ed, $"\nPick max {axisName} point: ");
            if (maxPt == null) return;

            double minVal = axis switch
            {
                ClipAxis.X => Math.Min(minPt.Value.X, maxPt.Value.X),
                ClipAxis.Y => Math.Min(minPt.Value.Y, maxPt.Value.Y),
                _           => Math.Min(minPt.Value.Z, maxPt.Value.Z),
            };
            double maxVal = axis switch
            {
                ClipAxis.X => Math.Max(minPt.Value.X, maxPt.Value.X),
                ClipAxis.Y => Math.Max(minPt.Value.Y, maxPt.Value.Y),
                _           => Math.Max(minPt.Value.Z, maxPt.Value.Z),
            };

            if (Math.Abs(maxVal - minVal) < 1e-6)
            {
                ed.WriteMessage("\nMin and max are too close — band would be empty.");
                return;
            }

            ed.WriteMessage(
                $"\nBand on {axisName}: {minVal:F4} to {maxVal:F4}  (thickness = {maxVal - minVal:F4})");

            PlaneManager.SetBand(axis, minVal, maxVal);
            ed.WriteMessage(PlaneManager.StatusText());

            ed.WriteMessage("\nSelect point cloud to apply band:");
            var id = PointCloudHelper.SelectPointCloud(ed);
            if (id == ObjectId.Null)
            {
                ed.WriteMessage("\nSelection cancelled.");
                return;
            }

            using var tr = db.TransactionManager.StartTransaction();
            PointCloudHelper.ApplyClippingPlanes(tr, id, PlaneManager.Planes, ed);
            tr.Commit();
        }

        // -----------------------------------------------------------------------
        // PCREMOVECLIP — strip all clipping from a selected point cloud
        // -----------------------------------------------------------------------
        [CommandMethod("PCREMOVECLIP", CommandFlags.Modal)]
        public void RemoveClip()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db  = doc.Database;
            var ed  = doc.Editor;

            ed.WriteMessage("\nSelect point cloud to remove all clipping from:");
            var id = PointCloudHelper.SelectPointCloud(ed);
            if (id == ObjectId.Null) return;

            using var tr = db.TransactionManager.StartTransaction();
            PointCloudHelper.RemoveAllClipping(tr, id, ed);
            tr.Commit();
        }

        // -----------------------------------------------------------------------
        // PCSLICERESET — clear all stored planes (does not change the drawing)
        // -----------------------------------------------------------------------
        [CommandMethod("PCSLICERESET", CommandFlags.Modal)]
        public void SliceReset()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed  = doc.Editor;

            PlaneManager.Clear();
            ed.WriteMessage("\nAll stored clipping planes cleared.");
        }

        // -----------------------------------------------------------------------
        // PCSTATUS — display currently stored planes
        // -----------------------------------------------------------------------
        [CommandMethod("PCSTATUS", CommandFlags.Modal)]
        public void Status()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed  = doc.Editor;

            ed.WriteMessage("\n--- PointCloudSlicer status ---");
            ed.WriteMessage($"\n{PlaneManager.StatusText()}");
        }

        // -----------------------------------------------------------------------
        // PCEXTENTS — report the geometric extents of a selected point cloud
        // -----------------------------------------------------------------------
        [CommandMethod("PCEXTENTS", CommandFlags.Modal)]
        public void Extents()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db  = doc.Database;
            var ed  = doc.Editor;

            ed.WriteMessage("\nSelect point cloud to report extents:");
            var id = PointCloudHelper.SelectPointCloud(ed);
            if (id == ObjectId.Null) return;

            using var tr = db.TransactionManager.StartTransaction();
            var ext = PointCloudHelper.GetExtents(tr, id);
            if (ext == null)
            {
                ed.WriteMessage("\nCould not determine point cloud extents.");
                return;
            }

            var e = ext.Value;
            ed.WriteMessage("\n--- Point cloud extents ---");
            ed.WriteMessage($"\n  Min: ({e.MinPoint.X:F4}, {e.MinPoint.Y:F4}, {e.MinPoint.Z:F4})");
            ed.WriteMessage($"\n  Max: ({e.MaxPoint.X:F4}, {e.MaxPoint.Y:F4}, {e.MaxPoint.Z:F4})");
            ed.WriteMessage($"\n  Size X: {e.MaxPoint.X - e.MinPoint.X:F4}");
            ed.WriteMessage($"\n  Size Y: {e.MaxPoint.Y - e.MinPoint.Y:F4}");
            ed.WriteMessage($"\n  Size Z: {e.MaxPoint.Z - e.MinPoint.Z:F4}");
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private static Point3d? GetPoint(Editor ed, string prompt)
        {
            var opts   = new PromptPointOptions(prompt);
            opts.AllowNone = false;
            var result = ed.GetPoint(opts);
            if (result.Status != PromptStatus.OK) return null;
            return result.Value;
        }

        /// <summary>
        /// Ask the user which side of the plane to keep.
        /// For Z this shows "Above / Below", for X/Y shows "Positive / Negative".
        /// Returns true if the user wants to keep the positive side,
        /// false for negative, or null if the user cancelled (ESC).
        /// </summary>
        private static bool? AskSide(Editor ed, string axisName, double value)
        {
            string pos, neg;
            if (axisName == "Z")
            {
                pos = "Above";
                neg = "Below";
            }
            else
            {
                pos = $"Positive";
                neg = $"Negative";
            }

            var opts = new PromptKeywordOptions(
                $"\nKeep which side of {axisName} = {value:F4}? [{pos}/{neg}]",
                $"{pos} {neg}");
            opts.Keywords.Default = pos;
            opts.AllowNone = false;

            var res = ed.GetKeywords(opts);
            if (res.Status != PromptStatus.OK)
                return null; // user cancelled

            return res.StringResult.Equals(pos, StringComparison.OrdinalIgnoreCase);
        }
    }
}
