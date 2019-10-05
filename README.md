# Source Control Switcher

Automatically Automatically sets the Source Control Provider according to the one 
used by the opened Visual Studio project.

Supported Source Control Providers are:

 * **AnkhSVN** *(Subversion, default)*;
 * **VisualSVN** *(Subversion)*;
 * **Visual Studio Tools for Git** *(Git, default)*;
 * **EZ-GIT (Easy Git Integration Tool)** *(Git)*;
 * **Git Source Control Provider** *(Git)*;
 * **HgSccPackage** *(Mercurial, default)*;
 * **VisualHG** *(Mercurial)*;
 * **VSHG** *(Mercurial)*;
 * **P4VS** *(Helix, default)*.
 
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
 * [SourceControlSwitch official page](https://www.ryadel.com/)
 * [SourceControlSwitch on GitHub](https://github.com/Darkseal/SourceControlSwitcher/)
