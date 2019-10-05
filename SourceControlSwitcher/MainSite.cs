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
    [Guid(GuidList.guidPkgString)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
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

#if DEBUG
        private readonly bool Debug = true;
#else
        private readonly bool Debug = false;
#endif

        public MainSite() { }

        protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // disabled - throws a System.MissingMethodException
            // the switch is already performed within the base.InitializeAsync() method.
            // await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await base.InitializeAsync(cancellationToken, progress);

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

            if (Debug)
            {
                TaskManager.Initialize(this);
                solutionEvents = ((Events2)_DTE2.Events).SolutionEvents;
                solutionEvents.Opened += new _dispSolutionEvents_OpenedEventHandler(this.SolutionEvents_Opened);
            }
        }

        void SolutionEvents_Opened()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var getProvider = GetService(typeof(IVsRegisterScciProvider)) as IVsGetScciProviderInterface;
            Assumes.Present(getProvider);
            Guid pGuid;
            getProvider.GetSourceControlProviderID(out pGuid);         
            TaskManager.AddWarning(String.Format("[DEBUG] Current Source Control Provider ID: {0}", pGuid.ToString()));
        }

        public static void RegisterPrimarySourceControlProvider(RcsType rcsType)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
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

            if (!enabled)
                return;

            SccProvider currentSccProvider = GetCurrentSccProvider();
            if (providerToLoad == currentSccProvider)
                return;

            int installed;
            hr = _VsShell.IsPackageInstalled(ref packageGuid, out installed);
            Marshal.ThrowExceptionForHR(hr);
            if (installed == 0)
                return;

            hr = _VsRegisterScciProvider.RegisterSourceControlProvider(sccProviderGuid);
            Marshal.ThrowExceptionForHR(hr);
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
                        packageGuid = new Guid(VSToolsForGitPackagedId);
                        sccProviderGuid = new Guid(VSToolsForGitSccProviderId);
                        provider = SccProvider.VisualStudioToolsForGit;
                        return true;
                    }
                case GitSccProvider.GitSourceControlProvider:
                    {
                        packageGuid = new Guid(GitSourceControlProviderPackagedId);
                        sccProviderGuid = new Guid(GitSourceControlProviderSccProviderId);
                        provider = SccProvider.GitSourceControlProvider;
                        return true;
                    }
                case GitSccProvider.EzGit:
                    {
                        packageGuid = new Guid(EzGitPackagedId);
                        sccProviderGuid = new Guid(EzGitProviderdId);
                        provider = SccProvider.EzGit;
                        return true;
                    }
                default:
                    throw new Exception();
            }
        }

        /// <returns>false if handling the scc provider is disabled for this Rcs type</returns>
        private static bool RegisterSubversionScc(out Guid packageGuid, out Guid sccProviderGuid, out SccProvider provider)
        {
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
                        packageGuid = new Guid(AnkhSvnPackageId);
                        sccProviderGuid = new Guid(AnkhSvnSccProviderId);
                        provider = SccProvider.AnkhSvn;
                        return true;
                    }
                case SubversionSccProvider.VisualSVN:
                    {
                        packageGuid = new Guid(VisualSvnPackageId);
                        sccProviderGuid = new Guid(VisualSvnSccProviderId);
                        provider = SccProvider.VisualSVN;
                        return true;
                    }
                case SubversionSccProvider.VisualSVN2019:
                    {
                        packageGuid = new Guid(VisualSvn2019PackageId);
                        sccProviderGuid = new Guid(VisualSvn2019SccProviderId);
                        provider = SccProvider.VisualSVN_2019;
                        return true;
                    }
                default:
                    throw new Exception();
            }
        }

        /// <returns>false if handling the scc provider is disabled for this Rcs type</returns>
        private static bool RegisterMercurialScc(out Guid packageGuid, out Guid sccProviderGuid, out SccProvider provider)
        {
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
                        packageGuid = new Guid(HgSccPackagePackageId);
                        sccProviderGuid = new Guid(HgSccPackageSccProviderId);
                        provider = SccProvider.HgSccPackage;
                        return true;
                    }
                case MercurialSccProvider.VisualHG:
                    {
                        packageGuid = new Guid(VisualHGPackageId);
                        sccProviderGuid = new Guid(VisualHGSccProviderId);
                        provider = SccProvider.VisualHG;
                        return true;
                    }
                case MercurialSccProvider.VSHG:
                    {
                        packageGuid = new Guid(VSHGPackageId);
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
            PerforceSccProvider perforceProvider = GetPerforceSccProvider();

            if (perforceProvider == PerforceSccProvider.Default)
            {
                perforceProvider = GetDefaultPerforceSccProvider();
            }

            switch (perforceProvider)
            {
                case PerforceSccProvider.Disabled:
                    packageGuid = new Guid();
                    sccProviderGuid = new Guid();
                    provider = SccProvider.Unknown;
                    return false;
                case PerforceSccProvider.P4VS:
                    packageGuid = new Guid(P4VSPackageId);
                    sccProviderGuid = new Guid(P4VSPackageSccProviderId);
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