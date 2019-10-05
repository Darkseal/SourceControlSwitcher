# Source Control Switcher

![Source Control Switcher Logo](https://www.ryadel.com/wp-content/uploads/2019/10/Source-Control-Switcher-logo-368x200.png)

Automatically sets the Source Control Provider according to the one 
used by the opened Visual Studio project.

All you need to do is to set your favourite defaults using the 
extension's dedicated ***Source Control Switcher*** option tab, 
which will be added to your Visual Studio 
**Tools** -> **Options** panel upon install, as in the screenshot below:

![Source Control Switcher - Options Screenshot](https://www.ryadel.com/wp-content/uploads/2019/10/ss-01-1.png)

Supported Source Control Providers are:

 * **AnkhSVN** *(Subversion, default)*
 * **VisualSVN** *(Subversion)*
 * **VisualSVN 2019** *(Subversion)*
 * **Visual Studio Tools for Git** *(Git, default)*
 * **EZ-GIT (Easy Git Integration Tool)** *(Git)*
 * **Git Source Control Provider** *(Git)*
 * **HgSccPackage** *(Mercurial, default)*
 * **VisualHG** *(Mercurial)*
 * **VSHG** *(Mercurial)*
 * **P4VS** *(Helix, default)*
 
More providers can be added, provided they are regular source control providers 
and there exists an easy way to detect
proper RCS type by checking file or directories presence
starting from solution root directory.

It supports all Visual Studio versions from 2015 to 2019.
License is MIT.

This extension is strongly based to [SccAutoSwitcher](https://github.com/ceztko/SccAutoSwitcher) by *Francesco Pretto*, 
which sadly seems to be no longer updated 
and lack VS2019 support and async loading support 
(which led me to create this project).

## Useful References
 * [SourceControlSwitch official page](https://www.ryadel.com/en/portfolio/source-control-switcher/)
 * [SourceControlSwitch on GitHub](https://github.com/Darkseal/SourceControlSwitcher/)
 * [SourceControlSwitch on Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=Ryadel.SourceControlSwitcher)
