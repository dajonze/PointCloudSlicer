using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;

[assembly: ExtensionApplication(typeof(PointCloudSlicer.PluginMain))]
[assembly: CommandClass(typeof(PointCloudSlicer.Commands))]

namespace PointCloudSlicer
{
    /// <summary>
    /// Plugin entry point — AutoCAD calls Initialize on load and Terminate on unload.
    /// </summary>
    public class PluginMain : IExtensionApplication
    {
        public void Initialize()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage(
                "\nPointCloudSlicer loaded. Commands: PCPICKX, PCPICKY, PCPICKZ, PCSLICE, PCSLICERESET, PCSTATUS\n");
        }

        public void Terminate() { }
    }
}
