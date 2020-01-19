// Copyright (c) 2013-2014 Francesco Pretto
// This file is subject to the MIT license

using System;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using EnvDTE80;
using Microsoft.Build.Evaluation;
using System.Reflection;
using System.IO;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;
using System.Threading;
using Microsoft;

namespace SourceControlSwitcher
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [Guid(GuidList.GuidPkgString)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionHasMultipleProjects, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionHasSingleProject, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExistsAndNotBuildingAndNotDebugging, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.NotBuildingAndNotDebugging, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.ToolboxInitialized, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideOptionPage(typeof(SwitcherOptions), "Source Control Switcher", "Source Control Providers", 101, 106, true)] // Options dialog page
    public sealed partial class MainSite : AsyncPackage, IVsSolutionEvents3, IVsSolutionLoadEvents
    {
        private static DTE2 _DTE2;

        private SolutionEvents solutionEvents;

        private static IVsRegisterScciProvider _VsRegisterScciProvider;
        private static IVsGetScciProviderInterface _VsGetScciProviderInterface;
        private static IVsShell _VsShell;
        private static WritableSettingsStore _SettingsStore;
        private static RcsType _CurrentSolutionRcsType;

        public MainSite() { }

        protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // NOTE: this switch is already performed within the base.InitializeAsync() method.
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _CurrentSolutionRcsType = RcsType.Unknown;

            //EnvDTE.IVsExtensibility extensibility = await GetServiceAsync(typeof(EnvDTE.IVsExtensibility)) as EnvDTE.IVsExtensibility;
            //_DTE2 = (DTE2)extensibility.GetGlobalsObject(null).DTE as EnvDTE80.DTE2;
            _DTE2 = Package.GetGlobalService(typeof(DTE)) as DTE2;

            IVsSolution solution = await GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            Assumes.Present(solution);
            int hr;
            uint pdwCookie;
            hr = solution.AdviseSolutionEvents(this, out pdwCookie);
            Marshal.ThrowExceptionForHR(hr);

            _VsShell = await GetServiceAsync(typeof(SVsShell)) as IVsShell;
            _VsRegisterScciProvider = await GetServiceAsync(typeof(IVsRegisterScciProvider)) as IVsRegisterScciProvider;
            _VsGetScciProviderInterface = await GetServiceAsync(typeof(IVsRegisterScciProvider)) as IVsGetScciProviderInterface;
            _SettingsStore = GetWritableSettingsStore();

            TaskManager.Initialize(this);
            solutionEvents = ((Events2)_DTE2.Events).SolutionEvents;
            solutionEvents.Opened += new _dispSolutionEvents_OpenedEventHandler(this.SolutionEvents_Opened);
            SolutionEvents_Opened();
        }

        void SolutionEvents_Opened()
        {
            AppHelper.Output("SolutionEvents_Opened");

            ThreadHelper.ThrowIfNotOnUIThread();
            //var getProvider = GetService(typeof(IVsRegisterScciProvider)) as IVsGetScciProviderInterface;
            //Assumes.Present(getProvider);

            if (AppHelper.Debug)
            {
                Guid pGuid;
                _VsGetScciProviderInterface.GetSourceControlProviderID(out pGuid);
                var msg = String.Format("Current Source Control Provider ID: {0}", pGuid.ToString());
                AppHelper.Output(msg);
            }

            //_CurrentSolutionRcsType = RcsType.Unknown;
            if (!string.IsNullOrWhiteSpace(_DTE2?.Solution?.FullName))
            {
                SetSCC(_DTE2.Solution.FullName);
            }
        }

        public static void RegisterPrimarySourceControlProvider(RcsType rcsType)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            AppHelper.Output(String.Format("Registering Primary Source Control Provider: {0}", rcsType.ToString()));

            int hr;
            Guid packageGuid = new Guid();
            Guid sccProviderGuid = new Guid();
            SccProvider providerToLoad = SccProvider.Unknown;
            bool enabled = false;

            switch (rcsType)
            {
                case RcsType.Subversion:
                    {
                        enabled = RegisterSubversionScc(out packageGuid, out sccProviderGuid, out providerToLoad);
                        break;
                    }
                case RcsType.Git:
                    {
                        enabled = RegisterGitScc(out packageGuid, out sccProviderGuid, out providerToLoad);
                        break;
                    }
                case RcsType.Mercurial:
                    {
                        enabled = RegisterMercurialScc(out packageGuid, out sccProviderGuid, out providerToLoad);
                        break;
                    }
                case RcsType.Perforce:
                    {
                        enabled = RegisterPerforceScc(out packageGuid, out sccProviderGuid, out providerToLoad);
                        break;
                    }
            }

            AppHelper.Output(String.Format("Provider to Load: {0}, Enabled: {1}", providerToLoad.ToString(), enabled));

            if (!enabled)
                return;

            SccProvider currentSccProvider = GetCurrentSccProvider();
            AppHelper.Output(String.Format("Current Provider: {0}", currentSccProvider.ToString()));

            if (providerToLoad == currentSccProvider)
                return;

            var installed = IsSccPackageInstalled(packageGuid);

            AppHelper.Output(String.Format("Provider {0} installed: {1}", providerToLoad.ToString(), installed));

            if (!installed)
                return;

            hr = _VsRegisterScciProvider.RegisterSourceControlProvider(sccProviderGuid);
            Marshal.ThrowExceptionForHR(hr);

            AppHelper.Output(String.Format("Provider {0} registered (providerGuid: {1})", providerToLoad.ToString(), sccProviderGuid.ToString()));
        }

        /// <returns>false if handling the scc provider is disabled for this Rcs type</returns>
        private static bool RegisterGitScc(out Guid packageGuid, out Guid sccProviderGuid, out SccProvider provider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            GitSccProvider gitProvider = GetGitSccProvider();

            if (gitProvider == GitSccProvider.Default)
                gitProvider = GetDefaultGitSccProvider();

            if (gitProvider == GitSccProvider.Disabled)
            {
                packageGuid = new Guid();
                sccProviderGuid = new Guid();
                provider = SccProvider.Unknown;
                return false;
            }

            switch (gitProvider)
            {
                case GitSccProvider.VisualStudioToolsForGit:
                    {
                        packageGuid = GetSccInstalledPackageId(VSToolsForGitPackageIds);
                        sccProviderGuid = new Guid(VSToolsForGitSccProviderId);
                        provider = SccProvider.VisualStudioToolsForGit;
                        return true;
                    }
                case GitSccProvider.EasyGitIntegrationTools:
                    {
                        packageGuid = GetSccInstalledPackageId(EasyGitIntegrationToolsPackageIds);
                        sccProviderGuid = new Guid(EasyGitIntegrationToolsSccProviderId);
                        provider = SccProvider.EasyGitIntegrationTools;
                        return true;
                    }
                default:
                    throw new Exception();
            }
        }

        /// <returns>false if handling the scc provider is disabled for this Rcs type</returns>
        private static bool RegisterSubversionScc(out Guid packageGuid, out Guid sccProviderGuid, out SccProvider provider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SubversionSccProvider svnProvider = GetSubversionSccProvider();

            if (svnProvider == SubversionSccProvider.Default)
                svnProvider = GetDefaultSubversionSccProvider();

            if (svnProvider == SubversionSccProvider.Disabled)
            {
                packageGuid = new Guid();
                sccProviderGuid = new Guid();
                provider = SccProvider.Unknown;
                return false;
            }

            switch (svnProvider)
            {
                case SubversionSccProvider.AnkhSVN:
                    {
                        packageGuid = GetSccInstalledPackageId(AnkhSvnPackageIds);
                        sccProviderGuid = new Guid(AnkhSvnSccProviderId);
                        provider = SccProvider.AnkhSvn;
                        return true;
                    }
                case SubversionSccProvider.VisualSVN:
                    {
                        packageGuid = GetSccInstalledPackageId(VisualSvnPackageIds);
                        sccProviderGuid = new Guid(VisualSvnSccProviderId);
                        provider = SccProvider.VisualSVN;
                        return true;
                    }
                default:
                    throw new Exception();
            }
        }

        /// <returns>false if handling the scc provider is disabled for this Rcs type</returns>
        private static bool RegisterMercurialScc(out Guid packageGuid, out Guid sccProviderGuid, out SccProvider provider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            MercurialSccProvider mercurialProvider = GetMercurialSccProvider();

            if (mercurialProvider == MercurialSccProvider.Default)
                mercurialProvider = GetDefaultMercurialSccProvider();

            if (mercurialProvider == MercurialSccProvider.Disabled)
            {
                packageGuid = new Guid();
                sccProviderGuid = new Guid();
                provider = SccProvider.Unknown;
                return false;
            }

            switch (mercurialProvider)
            {
                case MercurialSccProvider.HgSccPackage:
                    {
                        packageGuid = GetSccInstalledPackageId(HgSccPackagePackageIds);
                        sccProviderGuid = new Guid(HgSccPackageSccProviderId);
                        provider = SccProvider.HgSccPackage;
                        return true;
                    }
                case MercurialSccProvider.VisualHG:
                    {
                        packageGuid = GetSccInstalledPackageId(VisualHGPackageIds);
                        sccProviderGuid = new Guid(VisualHGSccProviderId);
                        provider = SccProvider.VisualHG;
                        return true;
                    }
                case MercurialSccProvider.VSHG:
                    {
                        packageGuid = GetSccInstalledPackageId(VSHGPackageIds);
                        sccProviderGuid = new Guid(VSHGProviderId);
                        provider = SccProvider.VSHG;
                        return true;
                    }
                default:
                    throw new Exception();
            }
        }

        private static bool RegisterPerforceScc(out Guid packageGuid, out Guid sccProviderGuid, out SccProvider provider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            PerforceSccProvider perforceProvider = GetPerforceSccProvider();

            if (perforceProvider == PerforceSccProvider.Default)
                perforceProvider = GetDefaultPerforceSccProvider();

            switch (perforceProvider)
            {
                case PerforceSccProvider.Disabled:
                    packageGuid = new Guid();
                    sccProviderGuid = new Guid();
                    provider = SccProvider.Unknown;
                    return false;
                case PerforceSccProvider.P4VS:
                    packageGuid = GetSccInstalledPackageId(P4VSPackageIds);
                    sccProviderGuid = new Guid(P4VSProviderId);
                    provider = SccProvider.P4VS;
                    return true;
                case PerforceSccProvider.Default:
                default:
                    throw new Exception();
            }
        }

        //private static string GetRegUserSettingsPath()
        //{
        //    string version = _DTE2.Version;
        //    string suffix = GetSuffix(_DTE2.CommandLineArguments);
        //    return @"Software\Microsoft\VisualStudio\" + version + suffix;
        //}

        //private static string GetSuffix(string args)
        //{
        //    string[] tokens = args.Split(' ', '\t');
        //    int foundIndex = -1;
        //    int it = 0;
        //    foreach (string str in tokens)
        //    {
        //        if (str.Equals("/RootSuffix", StringComparison.InvariantCultureIgnoreCase))
        //        {
        //            foundIndex = it + 1;
        //            break;
        //        }

        //        it++;
        //    }

        //    if (foundIndex == -1 || foundIndex >= tokens.Length)
        //        return String.Empty;

        //    return tokens[foundIndex];
        //}

        public WritableSettingsStore GetWritableSettingsStore()
        {
            var shellSettingsManager = new ShellSettingsManager(this);
            return shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
        }

        //private void GetService<T>(out T service)
        //{
        //    service = (T)GetService(typeof(T));
        //}

        //private T GetService<T>()
        //{
        //    return (T)GetService(typeof(T));
        //}

        public static DTE2 DTE2
        {
            get { return _DTE2; }
        }
    }
}
