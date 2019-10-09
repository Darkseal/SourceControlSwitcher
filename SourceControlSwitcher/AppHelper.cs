using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceControlSwitcher
{
    public static class AppHelper
    {
#if DEBUG
        public static readonly bool Debug = true;
#else
        public static readonly bool Debug = false;
#endif


        /// <summary>
        /// Writes to the general output window.
        /// https://stackoverflow.com/a/1852535/1233379
        /// </summary>
        /// <param name="msg"></param>
        public static void Output(string msg, bool appendTS = true, bool debugOnly = true)
        {
            if (debugOnly && !AppHelper.Debug) return;
            ThreadHelper.ThrowIfNotOnUIThread();

            if (AppHelper.Debug) msg = String.Format("[{0}] {1}", "DEBUG", msg);
            if (appendTS) msg = String.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), msg);

            // ADD TO Error List pane
            TaskManager.AddWarning(msg);

            // ADD TO custom Output pane
            IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            Guid customGuid = new Guid(GuidList.GuidPkgString);
            IVsOutputWindowPane customPane = null;
            outWindow.GetPane(ref customGuid, out customPane);
            if (customPane == null)
            {
                string customTitle = "Source Control Switcher [Debug]";
                outWindow.CreatePane(ref customGuid, customTitle, 1, 1);
                outWindow.GetPane(ref customGuid, out customPane);
            }
            customPane.Activate(); // Brings this pane into view
            customPane.OutputString(msg);
        }
    }
}
