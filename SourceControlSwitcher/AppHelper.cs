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
        /// <summary>
        /// Writes to the general output window.
        /// https://stackoverflow.com/a/1852535/1233379
        /// </summary>
        /// <param name="msg"></param>
        public static void Output(string msg)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;

            Guid generalPaneGuid = VSConstants.GUID_OutWindowGeneralPane;
            _ = outWindow.GetPane(ref generalPaneGuid, out IVsOutputWindowPane generalPane);

            _ = generalPane.OutputString(msg);
            _ = generalPane.Activate(); // Brings this pane into view
        }
    }
}
