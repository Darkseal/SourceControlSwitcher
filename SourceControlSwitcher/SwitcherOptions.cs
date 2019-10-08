using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace SourceControlSwitcher
{
    public class SwitcherOptions : DialogPage
    {
        [DisplayName("Git Provider")]
        [Description("Select the Git Source Control Provider to use.")]
        [Category("Source Control Providers")]
        public GitSccProvider GitProvider
        {
            get { return MainSite.GetGitSccProvider(); }
            set { MainSite.SetGitSccProvider(value); }
        }

        [DisplayName("Subversion Provider")]
        [Description("Select the SVN Source Control Provider to use.")]
        [Category("Source Control Providers")]
        public SubversionSccProvider SubversionProvider
        {
            get { return MainSite.GetSubversionSccProvider(); }
            set { MainSite.SetSubversionSccProvider(value); }
        }

        [DisplayName("Mercurial Provider")]
        [Description("Select the HG/Mercurial Source Control Provider to use.")]
        [Category("Source Control Providers")]
        public MercurialSccProvider MercurialProvider
        {
            get { return MainSite.GetMercurialSccProvider(); }
            set { MainSite.SetMercurialSccProvider(value); }
        }

        [DisplayName("Perforce Provider")]
        [Description("Select the Helix/Perforce Source Control Provider to use.")]
        [Category("Source Control Providers")]
        public PerforceSccProvider PerforceProvider
        {
            get { return MainSite.GetPerforceSccProvider(); }
            set { MainSite.SetPerforceSccProvider(value); }
        }
    }

    [Serializable]
    public enum GitSccProvider
    {
        Default = 0,

        [Description("Git Source Control Provider")]
        [Display(Name = "Git Source Control Provider")]
        [LocDisplayName("Git Source Control Provider")]
        GitSourceControlProvider,

        [Description("EZ-GIT")]
        [Display(Name = "EZ-GIT")]
        [LocDisplayName("EZ-GIT")]
        EzGit_2019,

        [Description("Visual Studio Tools for Git")]
        [Display(Name = "Visual Studio Tools for Git")]
        [LocDisplayName("Visual Studio Tools for Git")]
        VisualStudioToolsForGit,

        Disabled
    }

    public enum SubversionSccProvider
    {
        Default = 0,

        [Description("AnkhSVN")]
        [Display(Name = "AnkhSVN")]
        [LocDisplayName("AnkhSVN")]
        AnkhSVN,

        [Description("VisualSVN")]
        [Display(Name = "VisualSVN")]
        [LocDisplayName("VisualSVN")]
        VisualSVN,

        [Description("VisualSVN 2019")]
        [Display(Name = "VisualSVN 2019")]
        [LocDisplayName("VisualSVN 2019")]
        VisualSVN2019,

        Disabled
    }

    public enum MercurialSccProvider
    {
        Default = 0,

        [Description("HgSccPackage")]
        [Display(Name = "HgSccPackage")]
        [LocDisplayName("HgSccPackage")]
        HgSccPackage,

        [Description("VisualHG")]
        [Display(Name = "VisualHG")]
        [LocDisplayName("VisualHG")]
        VisualHG,

        [Description("VSHG")]
        [Display(Name = "VSHG")]
        [LocDisplayName("VSHG")]
        VSHG,

        Disabled
    }

    public enum PerforceSccProvider
    {
        Default = 0,

        [Description("P4VS")]
        [Display(Name = "P4VS")]
        [LocDisplayName("P4VS")]
        P4VS,

        Disabled
    }
}
