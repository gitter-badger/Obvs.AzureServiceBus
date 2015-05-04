﻿using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyTitle("Obvs.AzureServiceBus")]
[assembly: AssemblyDescription("Azure ServiceBus support for the Obvs framework.")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
[assembly: AssemblyCompany("Drew Marsh")]
[assembly: AssemblyProduct("Obvs.AzureServiceBus")]
[assembly: AssemblyCopyright("Copyright © Drew Marsh 2015")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("0.2.1.*")]
[assembly: AssemblyInformationalVersion("0.2.1-alpha")]

[assembly: InternalsVisibleTo("Obvs.AzureServiceBus.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]