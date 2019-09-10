﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Microsoft.AspNetCore.Identity;

using Covenant.Models;
using Covenant.Models.Launchers;
using Covenant.Models.Listeners;
using Covenant.Models.Grunts;

namespace Covenant.Core
{
    public static class DbInitializer
    {
        public async static Task Initialize(CovenantContext context, RoleManager<IdentityRole> roleManager, ConcurrentDictionary<int, CancellationTokenSource> ListenerCancellationTokens)
        {
            context.Database.EnsureCreated();

            await InitializeImplantTemplates(context);
            await InitializeListeners(context, ListenerCancellationTokens);
            await InitializeLaunchers(context);
            await InitializeTasks(context);
            await InitializeRoles(roleManager);
        }

        public async static Task InitializeImplantTemplates(CovenantContext context)
        {
            if (!context.ImplantTemplates.Any())
            {
                var templates = new List<ImplantTemplate>
                {
                    new ImplantTemplate
                    {
                        Name = "GruntHTTP",
                        Description = "A Windows implant written in C# that communicates over HTTP.",
                        Language = ImplantLanguage.CSharp,
                        CommType = CommunicationType.HTTP
                    },
                    new ImplantTemplate
                    {
                        Name = "GruntSMB",
                        Description = "A Windows implant written in C# that communicates over SMB.",
                        Language = ImplantLanguage.CSharp,
                        CommType = CommunicationType.SMB
                    }
                };
				templates.ForEach(T => T.ReadFromDisk());
                await context.ImplantTemplates.AddRangeAsync(templates);
            }
        }

        public async static Task InitializeListeners(CovenantContext context, ConcurrentDictionary<int, CancellationTokenSource> ListenerCancellationTokens)
        {
            if (!context.ListenerTypes.Any())
            {
                var listenerTypes = new List<ListenerType>
                {
                    new ListenerType { Name = "HTTP", Description = "Listens on HTTP protocol." }
                };
                await context.ListenerTypes.AddRangeAsync(listenerTypes);
                await context.SaveChangesAsync();
            }
            if (!context.Profiles.Any())
            {
                List<HttpProfile> httpProfiles = Directory.GetFiles(Common.CovenantProfileDirectory, "*.yaml", SearchOption.AllDirectories)
                                                 .Select(F => HttpProfile.Create(F))
                                                 .ToList();
                await context.Profiles.AddRangeAsync(httpProfiles);
                await context.SaveChangesAsync();
            }

            foreach (Listener l in context.Listeners.Where(L => L.Status == ListenerStatus.Active))
            {
                l.Profile = await context.GetProfile(l.ProfileId);
                await context.StartListener(l.Id, ListenerCancellationTokens);
            }
        }

        public async static Task InitializeLaunchers(CovenantContext context)
        {
            if (!context.Launchers.Any())
            {
                var launchers = new List<Launcher>
                {
                    new BinaryLauncher(),
                    new PowerShellLauncher(),
                    new MSBuildLauncher(),
                    new InstallUtilLauncher(),
                    new WmicLauncher(),
                    new Regsvr32Launcher(),
                    new MshtaLauncher(),
                    new CscriptLauncher(),
                    new WscriptLauncher()
                };
                await context.Launchers.AddRangeAsync(launchers);
                await context.SaveChangesAsync();
            }
        }

        public async static Task InitializeTasks(CovenantContext context)
        {
            if (!context.ReferenceAssemblies.Any())
            {
                List<ReferenceAssembly> ReferenceAssemblies = Directory.GetFiles(Common.CovenantAssemblyReferenceNet35Directory).Select(R =>
                {
                    FileInfo info = new FileInfo(R);
                    return new ReferenceAssembly
                    {
                        Name = info.Name,
                        Location = info.FullName,
                        DotNetVersion = Common.DotNetVersion.Net35
                    };
                }).ToList();
                Directory.GetFiles(Common.CovenantAssemblyReferenceNet40Directory).ToList().ForEach(R =>
                {
                    FileInfo info = new FileInfo(R);
                    ReferenceAssemblies.Add(new ReferenceAssembly
                    {
                        Name = info.Name,
                        Location = info.FullName,
                        DotNetVersion = Common.DotNetVersion.Net40
                    });
                });
                await context.ReferenceAssemblies.AddRangeAsync(ReferenceAssemblies);
                await context.SaveChangesAsync();
            }
            if (!context.EmbeddedResources.Any())
            {
                IEnumerable<EmbeddedResource> EmbeddedResources = Directory.GetFiles(Common.CovenantEmbeddedResourcesDirectory).Select(R =>
                {
                    FileInfo info = new FileInfo(R);
                    return new EmbeddedResource
                    {
                        Name = info.Name,
                        Location = info.FullName
                    };
                });
                await context.EmbeddedResources.AddRangeAsync(EmbeddedResources);
                await context.SaveChangesAsync();
            }

            if (!context.ReferenceSourceLibraries.Any())
            {
                var ReferenceSourceLibraries = new List<ReferenceSourceLibrary>
                {
                    new ReferenceSourceLibrary
                    {
                        Name = "SharpSploit", Description = "SharpSploit is a library for C# post-exploitation modules.",
                        Location = Common.CovenantReferenceSourceLibraries + "SharpSploit" + Path.DirectorySeparatorChar,
                        SupportedDotNetVersions = new List<Common.DotNetVersion> { Common.DotNetVersion.Net35, Common.DotNetVersion.Net40 }
                    },
                    new ReferenceSourceLibrary
                    {
                        Name = "Rubeus", Description = "Rubeus is a C# toolset for raw Kerberos interaction and abuses.",
                        Location = Common.CovenantReferenceSourceLibraries + "Rubeus" + Path.DirectorySeparatorChar,
                        SupportedDotNetVersions = new List<Common.DotNetVersion> { Common.DotNetVersion.Net35, Common.DotNetVersion.Net40 }
                    },
                    new ReferenceSourceLibrary
                    {
                        Name = "Seatbelt", Description = "Seatbelt is a C# project that performs a number of security oriented host-survey \"safety checks\" relevant from both offensive and defensive security perspectives.",
                        Location = Common.CovenantReferenceSourceLibraries + "Seatbelt" + Path.DirectorySeparatorChar,
                        SupportedDotNetVersions = new List<Common.DotNetVersion> { Common.DotNetVersion.Net35, Common.DotNetVersion.Net40 }
                    },
                    new ReferenceSourceLibrary
                    {
                        Name = "SharpDPAPI", Description = "SharpDPAPI is a C# port of some Mimikatz DPAPI functionality.",
                        Location = Common.CovenantReferenceSourceLibraries + "SharpDPAPI" + Path.DirectorySeparatorChar + "SharpDPAPI" + Path.DirectorySeparatorChar,
                        SupportedDotNetVersions = new List<Common.DotNetVersion> { Common.DotNetVersion.Net35, Common.DotNetVersion.Net40 }
                    },
                    // new ReferenceSourceLibrary
                    // {
                    //     Name = "SharpChrome", Description = "SharpChrome is a C# port of some Mimikatz DPAPI functionality targeting Google Chrome.",
                    //     Location = Common.CovenantReferenceSourceLibraries + "SharpDPAPI" + Path.DirectorySeparatorChar + "SharpChrome" + Path.DirectorySeparatorChar,
                    //     SupportedDotNetVersions = new List<Common.DotNetVersion> { Common.DotNetVersion.Net35, Common.DotNetVersion.Net40 }
                    // },
                    new ReferenceSourceLibrary
                    {
                        Name = "SharpDump", Description = "SharpDump is a C# port of PowerSploit's Out-Minidump.ps1 functionality.",
                        Location = Common.CovenantReferenceSourceLibraries + "SharpDump" + Path.DirectorySeparatorChar,
                        SupportedDotNetVersions = new List<Common.DotNetVersion> { Common.DotNetVersion.Net35, Common.DotNetVersion.Net40 }
                    },
                    new ReferenceSourceLibrary
                    {
                        Name = "SharpUp", Description = "SharpUp is a C# port of various PowerUp functionality.",
                        Location = Common.CovenantReferenceSourceLibraries + "SharpUp" + Path.DirectorySeparatorChar,
                        SupportedDotNetVersions = new List<Common.DotNetVersion> { Common.DotNetVersion.Net35, Common.DotNetVersion.Net40 }
                    },
                    new ReferenceSourceLibrary
                    {
                        Name = "SharpWMI", Description = "SharpWMI is a C# implementation of various WMI functionality.",
                        Location = Common.CovenantReferenceSourceLibraries + "SharpWMI" + Path.DirectorySeparatorChar,
                        SupportedDotNetVersions = new List<Common.DotNetVersion> { Common.DotNetVersion.Net35, Common.DotNetVersion.Net40 }
                    }
                };
                await context.ReferenceSourceLibraries.AddRangeAsync(ReferenceSourceLibraries);
                await context.SaveChangesAsync();

                var ss = await context.GetReferenceSourceLibraryByName("SharpSploit");
                var ru = await context.GetReferenceSourceLibraryByName("Rubeus");
                var se = await context.GetReferenceSourceLibraryByName("Seatbelt");
                var sd = await context.GetReferenceSourceLibraryByName("SharpDPAPI");
                // var sc = await context.GetReferenceSourceLibraryByName("SharpChrome");
                var sdu = await context.GetReferenceSourceLibraryByName("SharpDump");
                var su = await context.GetReferenceSourceLibraryByName("SharpUp");
                var sw = await context.GetReferenceSourceLibraryByName("SharpWMI");
                await context.AddRangeAsync(
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.DirectoryServices.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.DirectoryServices.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.IdentityModel.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.IdentityModel.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Management.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Management.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Management.Automation.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ss, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Management.Automation.dll", Common.DotNetVersion.Net40) },

    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ru, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ru, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ru, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ru, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ru, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ru, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ru, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.DirectoryServices.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ru, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.DirectoryServices.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ru, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.DirectoryServices.AccountManagement.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ru, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.DirectoryServices.AccountManagement.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ru, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.IdentityModel.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = ru, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.IdentityModel.dll", Common.DotNetVersion.Net40) },

    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.DirectoryServices.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.DirectoryServices.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Management.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Management.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.ServiceProcess.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.ServiceProcess.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.XML.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.XML.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Web.Extensions.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = se, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Web.Extensions.dll", Common.DotNetVersion.Net40) },

    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sd, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sd, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sd, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sd, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sd, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sd, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sd, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.XML.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sd, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.XML.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sd, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Security.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sd, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Security.dll", Common.DotNetVersion.Net40) },

    // new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sc, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net35) },
    // new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sc, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net40) },
    // new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sc, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net35) },
    // new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sc, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net40) },
    // new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sc, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net35) },
    // new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sc, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net40) },
    // new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sc, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.XML.dll", Common.DotNetVersion.Net35) },
    // new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sc, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.XML.dll", Common.DotNetVersion.Net40) },
    // new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sc, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Security.dll", Common.DotNetVersion.Net35) },
    // new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sc, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Security.dll", Common.DotNetVersion.Net40) },


    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sdu, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sdu, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sdu, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sdu, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sdu, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sdu, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net40) },

    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = su, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = su, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = su, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = su, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = su, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = su, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = su, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Management.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = su, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Management.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = su, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.ServiceProcess.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = su, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.ServiceProcess.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = su, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.XML.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = su, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.XML.dll", Common.DotNetVersion.Net40) },

    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sw, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sw, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sw, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sw, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sw, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sw, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net40) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sw, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Management.dll", Common.DotNetVersion.Net35) },
    new ReferenceSourceLibraryReferenceAssembly { ReferenceSourceLibrary = sw, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Management.dll", Common.DotNetVersion.Net40) }
                );
            }

            if (!context.GruntTasks.Any())
            {
                var GruntTasks = new List<GruntTask>
                {
                    new GruntTask
                    {
                        Name = "Shell",
                        AlternateNames = new List<string>(),
                        Description = "Execute a Shell command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "Shell" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 1,
                                Name = "ShellCommand",
                                Description = "The ShellCommand to execute.",
                                Value = "whoami",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "ShellCmd",
                        AlternateNames = new List<string>(),
                        Description = "Execute a Shell command using \"cmd.exe /c\"",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "ShellCmd" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 2,
                                Name = "ShellCommand",
                                Description = "The ShellCommand to execute.",
                                Value = "whoami",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "PowerShell",
                        AlternateNames = new List<string>(),
                        Description = "Execute a PowerShell command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "PowerShell" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 3,
                                Name = "PowerShellCommand",
                                Description = "The PowerShellCommand to execute.",
                                Value = "Get-ChildItem Env:",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "Assembly",
                        AlternateNames = new List<string>(),
                        Description = "Execute a dotnet Assembly EntryPoint.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "Assembly" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 4,
                                Name = "AssemblyName",
                                Description = "Name of the assembly.",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 5,
                                Name = "EncodedAssembly",
                                Description = "The Base64 encoded Assembly bytes.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = false
                            },
                            new GruntTaskOption
                            {
                                Id = 6,
                                Name = "Parameters",
                                Description = "The command-line parameters to pass to the assembly's EntryPoint.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "AssemblyReflect",
                        AlternateNames = new List<string>(),
                        Description = "Execute a dotnet Assembly method using reflection.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "AssemblyReflect" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 7,
                                Name = "AssemblyName",
                                Description = "Name of the assembly.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 8,
                                Name = "EncodedAssembly",
                                Description = "The Base64 encoded Assembly bytes.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = false
                            },
                            new GruntTaskOption
                            {
                                Id = 9,
                                Name = "TypeName",
                                Description = "The name of the Type that contains the method to execute.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 10,
                                Name = "MethodName",
                                Description = "The name of the method to execute.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 11,
                                Name = "Parameters",
                                Description = "The parameters to pass to the method.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "ListDirectory",
                        AlternateNames = new List<string> { "ls" },
                        Description = "Get a listing of the current directory.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "ListDirectory" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 12,
                                Name = "Path",
                                Description = "Directory to list.",
                                Value = ".",
                                SuggestedValues = new List<string>(),
                                Optional = true,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "ChangeDirectory",
                        AlternateNames = new List<string> { "cd" },
                        Description = "Change the current directory.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "ChangeDirectory" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 13,
                                Name = "Directory",
                                Description = "Directory to change to.",
                                Value = ".",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "ProcessList",
                        AlternateNames = new List<string> { "ps" },
                        Description = "Get a list of currently running processes.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "ProcessList" + ".task")),
                        Options = new List<GruntTaskOption> { }
                    },
                    new GruntTask
                    {
                        Name = "Upload",
                        AlternateNames = new List<string>(),
                        Description = "Upload a file.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "Upload" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 14,
                                Name = "FilePath",
                                Description = "Remote file path to write to.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 15,
                                Name = "FileContents",
                                Description = "Base64 contents of the file to be written.",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = false
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "Download",
                        AlternateNames = new List<string>(),
                        Description = "Download a file.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "Download" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 16,
                                Name = "FileName",
                                Description = "Remote file name to download.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "Mimikatz",
                        AlternateNames = new List<string>(),
                        Description = "Execute a mimikatz command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "Mimikatz" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 17,
                                Name = "Command",
                                Description = "Mimikatz command to execute.",
                                Value = "sekurlsa::logonPasswords",
                                SuggestedValues = new List<string> { "sekurlsa::logonpasswords", "privilege::debug sekurlsa::logonpasswords", "lsadump::sam", "token::elevate lsadump::sam", "lsadump::secrets", "token::elevate lsadump::secrets" },
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "LogonPasswords",
                        AlternateNames = new List<string>(),
                        Description = "Execute the 'privilege::debug sekurlsa::logonPasswords' Mimikatz command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "LogonPasswords" + ".task")),
                        Options = new List<GruntTaskOption>()
                    },
                    new GruntTask
                    {
                        Name = "LsaSecrets",
                        AlternateNames = new List<string>(),
                        Description = "Execute the 'privilege::debug lsadump::secrets' Mimikatz command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "LsaSecrets" + ".task")),
                        Options = new List<GruntTaskOption>()
                    },
                    new GruntTask
                    {
                        Name = "LsaCache",
                        AlternateNames = new List<string>(),
                        Description = "Execute the 'privilege::debug lsadump::cache' Mimikatz command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "LsaCache" + ".task")),
                        Options = new List<GruntTaskOption>()
                    },
                    new GruntTask
                    {
                        Name = "SamDump",
                        AlternateNames = new List<string>(),
                        Description = "Execute the 'privilege::debug lsadump::sam' Mimikatz command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "SamDump" + ".task")),
                        Options = new List<GruntTaskOption>()
                    },
                    new GruntTask
                    {
                        Name = "Wdigest",
                        AlternateNames = new List<string>(),
                        Description = "Execute the 'sekurlsa::wdigest' Mimikatz command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "Wdigest" + ".task")),
                        Options = new List<GruntTaskOption>()
                    },
                    new GruntTask
                    {
                        Name = "PortScan",
                        AlternateNames = new List<string>(),
                        Description = "Perform a TCP port scan.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "PortScan" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 18,
                                Name = "ComputerNames",
                                Description = "ComputerName(s) to port scan. Can be a DNS name, IP address, or CIDR range.",
                                Value = "127.0.0.1",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 19,
                                Name = "Ports",
                                Description = "Ports to scan. Comma-delimited port list.",
                                Value = "80,443,445",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 20,
                                Name = "Ping",
                                Description = "Boolean, whether to ping hosts prior to port scanning.",
                                Value = "False",
                                SuggestedValues = new List<string>(),
                                Optional = true,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "Rubeus",
                        AlternateNames = new List<string>(),
                        Description = "Use a rubeus command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "Rubeus" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 21,
                                Name = "Command",
                                Description = "Rubeus command to execute.",
                                Value = "triage",
                                SuggestedValues = new List<string> { "triage", "dump", "asktgt", "asktgs", "renew", "s4u", "ptt", "purge", "describe", "klist", "tgtdeleg", "monitor", "harvest", "kerberoast", "asreproast", "createnetonly", "changepw", "hash", "tgssub" },
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "Kerberoast",
                        AlternateNames = new List<string>(),
                        Description = "Perform a \"Kerberoast\" attack that retrieves crackable service tickets for Domain User's w/ an SPN set.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "Kerberoast" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 22,
                                Name = "Usernames",
                                Description = "Username(s) to port scan. Comma-delimited username list.",
                                Value = "DOMAIN\\username1,DOMAIN\\username2",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 23,
                                Name = "HashFormat",
                                Description = "Format to output the hashes (\"Hashcat\" or \"John\").",
                                Value = "Hashcat",
                                SuggestedValues = new List<string> { "Hashcat", "John" },
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "SafetyKatz",
                        AlternateNames = new List<string>(),
                        Description = "Use SafetyKatz.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "SafetyKatz" + ".task")),
                        Options = new List<GruntTaskOption>()
                    },
                    new GruntTask
                    {
                        Name = "SharpDPAPI",
                        AlternateNames = new List<string>(),
                        Description = "Use a SharpDPAPI command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "SharpDPAPI" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 24,
                                Name = "Command",
                                Description = "SharpDPAPI command to execute.",
                                Value = "triage",
                                SuggestedValues = new List<string> { "triage", "machinetriage", "backupkey", "masterkeys", "machinemasterkeys", "credentials", "machinecredentials", "vaults", "machinevaults", "rdg" },
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    // new GruntTask
                    // {
                    //     Name = "SharpChrome",
                    //     AlternateNames = new List<string>(),
                    //     Description = "Use a SharpChrome command.",
                    //     Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "SharpChrome" + ".task")),
                    //     Options = new List<GruntTaskOption>
                    //     {
                    //         new GruntTaskOption
                    //         {
                    //             Id = 25,
                    //             Name = "Command",
                    //             Description = "SharpChrome command to execute.",
                    //             Value = "logins",
                    //             SuggestedValues = new List<string> { "logins", "cookies", "backupkey" },
                    //             Optional = false,
                    //             DisplayInCommand = true
                    //         }
                    //     }
                    // },
                    new GruntTask
                    {
                        Name = "SharpUp",
                        AlternateNames = new List<string>(),
                        Description = "Use a SharpUp command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "SharpUp" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 26,
                                Name = "Command",
                                Description = "SharpUp command to execute.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "SharpDump",
                        AlternateNames = new List<string>(),
                        Description = "Use a SharpDump command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "SharpDump" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 27,
                                Name = "ProcessID",
                                Description = "ProcessID of the process to createa dump file of.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "Seatbelt",
                        AlternateNames = new List<string>(),
                        Description = "Use a Seatbelt command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "Seatbelt" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 28,
                                Name = "Command",
                                Description = "Seatbelt command to execute.",
                                Value = "",
                                SuggestedValues = new List<string> { "all", "system", "BasicOSInfo", "RebootSchedule", "TokenGroupPrivs", "UACSystemPolicies", "PowerShellSettings",
                                    "AuditSettings", "WEFSettings", "LSASettings", "UserEnvVariables", "SystemEnvVariables", "UserFolders", "NonstandardServices", "LapsSettings",
                                    "LocalGroupMembers", "MappedDrives", "RDPSessions", "WMIMappedDrives", "NetworkShares", "FirewallRules", "AntiVirusWMI", "InterestingProcesses",
                                    "RegistryAutoRuns", "RegistryAutoLogon", "DNSCache", "ARPTable", "AllTcpConnections", "AllUdpConnections", "NonstandardProcesses", "SysmonConfig",
                                    "user", "SavedRDPConnections", "TriageIE", "DumpVault", "RecentRunCommands", "PuttySessions", "PuttySSHHostKeys", "CloudCreds", "RecentFiles",
                                    "MasterKeys", "CredFiles", "RCDManFiles", "CurrentDomainGroups", "Patches", "LogonSessions", "KerberosTGTData", "InterestingFiles", "IETabs",
                                    "TriageChrome", "TriageFirefox", "RecycleBin", "4624Events", "4648Events", "KerberosTickets" },
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "SharpWMI",
                        AlternateNames = new List<string>(),
                        Description = "Use a SharpWMI command.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "SharpWMI" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 29,
                                Name = "Command",
                                Description = "SharpWMI command to execute.",
                                Value = "",
                                SuggestedValues = new List<string> { "action=query", "action=create", "action=executevbs" },
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "WhoAmI",
                        AlternateNames = new List<string>(),
                        Description = "Gets the username of the currently used/impersonated token.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "WhoAmI" + ".task")),
                        Options = new List<GruntTaskOption>()
                    },
                    new GruntTask
                    {
                        Name = "ImpersonateUser",
                        AlternateNames = new List<string>(),
                        Description = "Find a process owned by the specified user and impersonate the token. Used to execute subsequent commands as the specified user.",
                        TokenTask = true,
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "ImpersonateUser" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 30,
                                Name = "Username",
                                Description = "User to impersonate. \"DOMAIN\\Username\" format expected.",
                                Value = "DOMAIN\\Username",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "ImpersonateProcess",
                        AlternateNames = new List<string>(),
                        Description = "Impersonate the token of the specified process. Used to execute subsequent commands as the user associated with the token of the specified process.",
                        TokenTask = true,
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "ImpersonateUser" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 31,
                                Name = "ProcessID",
                                Description = "Process ID of the process to impersonate.",
                                Value = "1234",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "GetSystem",
                        AlternateNames = new List<string>(),
                        Description = "Impersonate the SYSTEM user. Equates to ImpersonateUser(\"NT AUTHORITY\\SYSTEM\").",
                        TokenTask = true,
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "GetSystem" + ".task")),
                        Options = new List<GruntTaskOption>()
                    },
                    new GruntTask
                    {
                        Name = "MakeToken",
                        AlternateNames = new List<string>(),
                        Description = "Makes a new token with a specified username and password, and impersonates it to conduct future actions as the specified user.",
                        TokenTask = true,
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "MakeToken" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 32,
                                Name = "Username",
                                Description = "Username to authenticate as.",
                                Value = "username1",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 33,
                                Name = "Domain",
                                Description = "Domain to authenticate the user to.",
                                Value = "DOMAIN",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 34,
                                Name = "Password",
                                Description = "Password to authenticate the user.",
                                Value = "Password123",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 35,
                                Name = "LogonType",
                                Description = "LogonType to use. Defaults to LOGON32_LOGON_NEW_CREDENTIALS, which is suitable to perform actions that require remote authentication. LOGON32_LOGON_INTERACTIVE is suitable for local actions.",
                                Value = "LOGON32_LOGON_NEW_CREDENTIALS",
                                SuggestedValues = new List<string>(),
                                Optional = true,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "RevertToSelf",
                        AlternateNames = new List<string> { "RevToSelf" },
                        Description = "Ends the impersonation of any token, reverting back to the initial token associated with the current process. Useful in conjuction with functions impersonate a token and do not automatically RevertToSelf, such as ImpersonateUser(), ImpersonateProcess(), GetSystem(), and MakeToken().",
                        TokenTask = true,
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "RevertToSelf" + ".task")),
                        Options = new List<GruntTaskOption>()
                    },
                    new GruntTask
                    {
                        Name = "WMICommand",
                        AlternateNames = new List<string>(),
                        Description = "Execute a process on a remote system using Win32_Process Create, optionally with alternate credentials.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "WMI" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 36,
                                Name = "ComputerName",
                                Description = "ComputerName to create the process on.",
                                Value = "localhost",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 37,
                                Name = "Command",
                                Description = "Command line to execute on the remote system.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 38,
                                Name = "Username",
                                Description = "Username to authenticate as. Format: DOMAIN\\Username (optional)",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 39,
                                Name = "Password",
                                Description = "Password to authenticate the user. (optional)",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "WMIGrunt",
                        AlternateNames = new List<string>(),
                        Description = "Execute a Grunt Launcher on a remote system using Win32_Process Create, optionally with alternate credentials.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "WMI" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 40,
                                Name = "ComputerName",
                                Description = "ComputerName to launch the Grunt on.",
                                Value = "localhost",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 41,
                                Name = "Launcher",
                                Description = "Grunt Launcher to execute on the remote system.",
                                Value = "PowerShell",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 42,
                                Name = "Username",
                                Description = "Username to authenticate as. Format: DOMAIN\\Username (optional)",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 43,
                                Name = "Password",
                                Description = "Password to authenticate the user. (optional)",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "DCOMCommand",
                        AlternateNames = new List<string>(),
                        Description = "Execute a process on a remote system using various DCOM methods.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "DCOM" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 44,
                                Name = "ComputerName",
                                Description = "ComputerName to execute the process on.",
                                Value = "localhost",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 45,
                                Name = "Command",
                                Description = "Command line to execute on the remote system.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 46,
                                Name = "Method",
                                Description = "DCOM method to use for execution.",
                                Value = "MMC20.Application",
                                SuggestedValues = new List<string>(),
                                Optional = true,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "DCOMGrunt",
                        AlternateNames = new List<string>(),
                        Description = "Execute a Grunt Launcher on a remote system using various DCOM methods.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "DCOM" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 47,
                                Name = "ComputerName",
                                Description = "ComputerName to execute the process on.",
                                Value = "localhost",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 48,
                                Name = "Launcher",
                                Description = "Grunt Launcher to execute on the remote system.",
                                Value = "PowerShell",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 49,
                                Name = "Method",
                                Description = "DCOM method to use for execution.",
                                Value = "MMC20.Application",
                                SuggestedValues = new List<string>(),
                                Optional = true,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "BypassUACCommand",
                        AlternateNames = new List<string>(),
                        Description = "Bypasses UAC through token duplication and executes a command with high integrity.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "BypassUAC" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 50,
                                Name = "Command",
                                Description = "Command to execute with high integrity.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "BypassUACGrunt",
                        AlternateNames = new List<string>(),
                        Description = "Bypasses UAC through token duplication and executes a Grunt Launcher with high integrity.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "BypassUAC" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 51,
                                Name = "Launcher",
                                Description = "Launcher to execute with high integrity.",
                                Value = "PowerShell",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "GetDomainUser",
                        AlternateNames = new List<string>(),
                        Description = "Gets a list of specified (or all) user `DomainObject`s in the current Domain.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "GetDomainUser" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 52,
                                Name = "Identities",
                                Description = "List of comma-delimited usernames to retrieve.",
                                Value = "username",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "GetDomainGroup",
                        AlternateNames = new List<string>(),
                        Description = "Gets a list of specified (or all) group `DomainObject`s in the current Domain.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "GetDomainGroup" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 53,
                                Name = "Identities",
                                Description = "List of comma-delimited groups to retrieve.",
                                Value = "Domain Admins",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "GetDomainComputer",
                        AlternateNames = new List<string>(),
                        Description = "Gets a list of specified (or all) computer `DomainObject`s in the current Domain.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "GetDomainComputer" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 54,
                                Name = "Identities",
                                Description = "List of comma-delimited computers to retrieve.",
                                Value = "DC01",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "GetNetLocalGroup",
                        AlternateNames = new List<string>(),
                        Description = "Gets a list of `LocalGroup`s from specified remote computer(s).",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "GetNetLocalGroup" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 55,
                                Name = "ComputerNames",
                                Description = "List of comma-delimited ComputerNames to query.",
                                Value = "DC01",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "GetNetLocalGroupMember",
                        AlternateNames = new List<string>(),
                        Description = "Gets a list of `LocalGroupMember`s from specified remote computer(s).",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "GetNetLocalGroupMember" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 56,
                                Name = "ComputerNames",
                                Description = "List of comma-delimited ComputerNames to query.",
                                Value = "DC01",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 57,
                                Name = "LocalGroup",
                                Description = "LocalGroup name to query for members.",
                                Value = "Administrators",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "GetNetLoggedOnUser",
                        AlternateNames = new List<string>(),
                        Description = "Gets a list of `LoggedOnUser`s from specified remote computer(s).",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "GetNetLoggedOnUser" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 58,
                                Name = "ComputerNames",
                                Description = "List of comma-delimited ComputerNames to query.",
                                Value = "DC01",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "GetNetSession",
                        AlternateNames = new List<string>(),
                        Description = "Gets a list of `SessionInfo`s from specified remote computer(s).",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "GetNetSession" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 59,
                                Name = "ComputerNames",
                                Description = "List of comma-delimited ComputerNames to query.",
                                Value = "DC01",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "GetRegistryKey",
                        AlternateNames = new List<string>(),
                        Description = "Gets a value stored in registry.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "GetRegistryKey" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 60,
                                Name = "RegPath",
                                Description = "The full path to the registry value to be read.",
                                Value = "HKEY_CURRENT_USER\\Environment\\Path",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "SetRegistryKey",
                        AlternateNames = new List<string>(),
                        Description = "Sets a value into the registry.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "SetRegistryKey" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 61,
                                Name = "RegPath",
                                Description = "The full path to the registry value to be read.",
                                Value = "HKEY_CURRENT_USER\\Environment\\Path",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 62,
                                Name = "Value",
                                Description = "The value to write to the registry key.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "GetRemoteRegistryKey",
                        AlternateNames = new List<string>(),
                        Description = "Gets a value stored in registry on a remote system.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "GetRemoteRegistryKey" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 63,
                                Name = "Hostname",
                                Description = "The Hostname of the remote system to query.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 64,
                                Name = "RegPath",
                                Description = "The full path to the registry value to be read.",
                                Value = "HKEY_CURRENT_USER\\Environment\\Path",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "SetRemoteRegistryKey",
                        AlternateNames = new List<string>(),
                        Description = "Sets a value into the registry on a remote system.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "SetRemoteRegistryKey" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 65,
                                Name = "Hostname",
                                Description = "The Hostname of the remote system to write to.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 66,
                                Name = "RegPath",
                                Description = "The full path to the registry value to be read.",
                                Value = "HKEY_CURRENT_USER\\Environment\\Path",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 67,
                                Name = "Value",
                                Description = "The value to write to the registry key.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "ShellCode",
                        AlternateNames = new List<string>(),
                        Description = "Executes a specified shellcode byte array by copying it to pinned memory, modifying the memory permissions, and executing.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "ShellCode" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 68,
                                Name = "Hex",
                                Description = "Hex string representing the Shellcode bytes to execute.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "PrivExchange",
                        AlternateNames = new List<string>(),
                        Description = "Performs the PrivExchange attack by sending a push notification to EWS.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "PrivExchange" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 69,
                                Name = "EWSUri",
                                Description = "The URI of the Exchange EWS instance to perform the relay against. For example: http(s)://<hostname>:<port>/EWS/Exchange.asmx.",
                                Value = "https://exchange.example.local:443/EWS/Exchange.asmx",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 70,
                                Name = "RelayUri",
                                Description = "The URI of the external relay of the Exchange authentication.",
                                Value = "https://relay.example.local:443/relay",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 71,
                                Name = "ExchangeVersion",
                                Description = "Microsoft Exchange version. Defaults to Exchange2010.",
                                Value = "Exchange2010",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "PersistCOMHijack",
                        AlternateNames = new List<string>(),
                        Description = "Hijacks a CLSID key to execute a payload for persistence.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "PersistCOMHijack" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 72,
                                Name = "CLSID",
                                Description = "Missing CLSID to abuse.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 73,
                                Name = "ExecutablePath",
                                Description = "Path to the executable path.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "Set",
                        AlternateNames = new List<string>(),
                        Description = "Set a Grunt setting.",
                        Code = "",
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 74,
                                Name = "Setting",
                                Description = "Setting to set.",
                                Value = "",
                                SuggestedValues = new List<string> { "Delay", "ConnectAttempts", "JitterPercent" },
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "Jobs",
                        AlternateNames = new List<string>(),
                        Description = "Get active Jobs.",
                        Code = "",
                        Options = new List<GruntTaskOption>()
                    },
                    new GruntTask
                    {
                        Name = "Kill",
                        AlternateNames = new List<string>(),
                        Description = "Kill the Grunt.",
                        Code = "",
                        Options = new List<GruntTaskOption>()
                    },
                    new GruntTask
                    {
                        Name = "Connect",
                        AlternateNames = new List<string>(),
                        Description = "Connect to a P2P Grunt.",
                        Code = "",
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 75,
                                Name = "ComputerName",
                                Description = "ComputerName of Grunt to connect to.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 76,
                                Name = "PipeName",
                                Description = "PipeName of Grunt to connect to.",
                                Value = "",
                                SuggestedValues = new List<string> { "gruntsvc" },
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "Disconnect",
                        AlternateNames = new List<string>(),
                        Description = "Disconnect from a ChildGrunt.",
                        Code = "",
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 77,
                                Name = "GruntName",
                                Description = "Name of Grunt to disconnect from.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "SharpShell",
                        AlternateNames = new List<string>(),
                        Description = "Execute custom c# code.",
                        Code = "",
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 78,
                                Name = "Code",
                                Description = "C# code to execute.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "PowerShellImport",
                        AlternateNames = new List<string>(),
                        Description = "Import a PowerShell script.",
                        Code = "",
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 79,
                                Name = "Script",
                                Description = "PowerShell Script to import.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = false
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "Help",
                        AlternateNames = new List<string>(),
                        Description = "Show the help menu.",
                        Code = "",
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 80,
                                Name = "TaskName",
                                Description = "The GruntTask name to retrieve help information for.",
                                SuggestedValues = new List<string>(),
                                Optional = true,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "PersistStartup",
                        AlternateNames = new List<string>(),
                        Description = "Installs a payload into the current users startup folder.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "PersistStartup" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 81,
                                Name = "Payload",
                                Description = "Payload to write to a file.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 82,
                                Name = "FileName",
                                Description = "Name of the file to write. Defaults to \"startup.bat\"",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "PersistAutorun",
                        AlternateNames = new List<string>(),
                        Description = "Installs an autorun value in HKCU or HKLM to execute a payload.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "PersistAutorun" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 83,
                                Name = "TargetHive",
                                Description = "Target hive to install autorun. CurrentUser or LocalMachine.",
                                Value = "CurrentUser",
                                SuggestedValues = new List<string> {"CurrentUser", "LocalMachine"},
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 84,
                                Name = "Value",
                                Description = "Value to set in the registry.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 85,
                                Name = "Name",
                                Description = "Name for the registy value. Defaults to \"Updater\".",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    },
                    new GruntTask
                    {
                        Name = "PersistWMI",
                        AlternateNames = new List<string>(),
                        Description = "Creates a WMI Event, Consumer and Binding to execute a payload.",
                        Code = File.ReadAllText(Path.Combine(Common.CovenantTaskCSharpDirectory, "PersistWMI" + ".task")),
                        Options = new List<GruntTaskOption>
                        {
                            new GruntTaskOption
                            {
                                Id = 86,
                                Name = "EventName",
                                Description = "An arbitrary name to be assigned to the new WMI Event.",
                                Value = "CurrentUser",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 87,
                                Name = "ConsumerType",
                                Description = "Specifies the action to carry out. The options are CommandLine (OS Command) and ActiveScript (JScript or VBScript).",
                                Value = "",
                                SuggestedValues = new List<string> {"CommandLine", "ActiveScript"},
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 88,
                                Name = "Payload",
                                Description = "Specifies the CommandLine or ActiveScript payload to run.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 89,
                                Name = "ProcessName",
                                Description = "Specifies the process name when the ProcessStart trigger is selected. Defaults to notepad.exe.",
                                Value = "",
                                SuggestedValues = new List<string>(),
                                Optional = false,
                                DisplayInCommand = true
                            },
                            new GruntTaskOption
                            {
                                Id = 90,
                                Name = "ScriptEngine",
                                Description = "Specifies the scripting engine when the ActiveScript consumer is selected. Defaults to VBScript.",
                                Value = "",
                                SuggestedValues = new List<string> {"ActiveScript", "VBScript"},
                                Optional = false,
                                DisplayInCommand = true
                            }
                        }
                    }
                };
                await context.GruntTasks.AddRangeAsync(GruntTasks);
                await context.SaveChangesAsync();

                var ss = await context.GetReferenceSourceLibraryByName("SharpSploit");
                var ru = await context.GetReferenceSourceLibraryByName("Rubeus");
                var se = await context.GetReferenceSourceLibraryByName("Seatbelt");
                var sd = await context.GetReferenceSourceLibraryByName("SharpDPAPI");
                // var sc = await context.GetReferenceSourceLibraryByName("SharpChrome");
                var sdu = await context.GetReferenceSourceLibraryByName("SharpDump");
                var su = await context.GetReferenceSourceLibraryByName("SharpUp");
                var sw = await context.GetReferenceSourceLibraryByName("SharpWMI");
                await context.AddRangeAsync(
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("Shell") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("ShellCmd") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("PowerShell") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("Assembly") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("AssemblyReflect") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("ListDirectory") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("ChangeDirectory") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("ProcessList") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("Mimikatz") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("LogonPasswords") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("LsaSecrets") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("LsaCache") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("SamDump") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("Wdigest") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("PortScan") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ru, GruntTask = await context.GetGruntTaskByName("Rubeus") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("Kerberoast") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("SafetyKatz") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = sd, GruntTask = await context.GetGruntTaskByName("SharpDPAPI") },
    // new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = sc, GruntTask = await context.GetGruntTaskByName("SharpChrome") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = su, GruntTask = await context.GetGruntTaskByName("SharpUp") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = sdu, GruntTask = await context.GetGruntTaskByName("SharpDump") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = se, GruntTask = await context.GetGruntTaskByName("Seatbelt") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = sw, GruntTask = await context.GetGruntTaskByName("SharpWMI") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("WhoAmI") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("ImpersonateUser") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("ImpersonateProcess") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("GetSystem") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("MakeToken") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("RevertToSelf") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("WMICommand") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("WMIGrunt") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("DCOMCommand") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("DCOMGrunt") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("BypassUACCommand") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("BypassUACGrunt") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("GetDomainUser") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("GetDomainGroup") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("GetDomainComputer") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("GetNetLocalGroup") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("GetNetLocalGroupMember") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("GetNetLoggedOnUser") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("GetNetSession") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("GetRegistryKey") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("SetRegistryKey") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("GetRemoteRegistryKey") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("SetRemoteRegistryKey") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("ShellCode") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("SharpShell") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("PrivExchange") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("PersistCOMHijack") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("PersistStartup") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("PersistAutorun") },
    new GruntTaskReferenceSourceLibrary { ReferenceSourceLibrary = ss, GruntTask = await context.GetGruntTaskByName("PersistWMI") }
                );

                var er1 = await context.GetEmbeddedResourceByName("SharpSploit.Resources.powerkatz_x64.dll");
                var er2 = await context.GetEmbeddedResourceByName("SharpSploit.Resources.powerkatz_x86.dll");
                await context.AddRangeAsync(
                    new GruntTaskEmbeddedResource { EmbeddedResource = er1, GruntTask = await context.GetGruntTaskByName("Mimikatz") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er1, GruntTask = await context.GetGruntTaskByName("LogonPasswords") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er1, GruntTask = await context.GetGruntTaskByName("LsaSecrets") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er1, GruntTask = await context.GetGruntTaskByName("LsaCache") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er1, GruntTask = await context.GetGruntTaskByName("SamDump") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er1, GruntTask = await context.GetGruntTaskByName("Wdigest") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er1, GruntTask = await context.GetGruntTaskByName("SafetyKatz") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er2, GruntTask = await context.GetGruntTaskByName("Mimikatz") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er2, GruntTask = await context.GetGruntTaskByName("LogonPasswords") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er2, GruntTask = await context.GetGruntTaskByName("LsaSecrets") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er2, GruntTask = await context.GetGruntTaskByName("LsaCache") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er2, GruntTask = await context.GetGruntTaskByName("SamDump") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er2, GruntTask = await context.GetGruntTaskByName("Wdigest") },
                    new GruntTaskEmbeddedResource { EmbeddedResource = er2, GruntTask = await context.GetGruntTaskByName("SafetyKatz") }
                );
                var upload = await context.GetGruntTaskByName("Upload");
                var download = await context.GetGruntTaskByName("Download");
                var privexchange = await context.GetGruntTaskByName("PrivExchange");
                await context.AddRangeAsync(
    new GruntTaskReferenceAssembly { GruntTask = upload, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net35) },
    new GruntTaskReferenceAssembly { GruntTask = upload, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net40) },
    new GruntTaskReferenceAssembly { GruntTask = upload, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net35) },
    new GruntTaskReferenceAssembly { GruntTask = upload, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net40) },
    new GruntTaskReferenceAssembly { GruntTask = upload, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net35) },
    new GruntTaskReferenceAssembly { GruntTask = upload, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net40) },
    new GruntTaskReferenceAssembly { GruntTask = download, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net35) },
    new GruntTaskReferenceAssembly { GruntTask = download, ReferenceAssembly = await context.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net40) },
    new GruntTaskReferenceAssembly { GruntTask = download, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net35) },
    new GruntTaskReferenceAssembly { GruntTask = download, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net40) },
    new GruntTaskReferenceAssembly { GruntTask = download, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net35) },
    new GruntTaskReferenceAssembly { GruntTask = download, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net40) },
    new GruntTaskReferenceAssembly { GruntTask = privexchange, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.XML.dll", Common.DotNetVersion.Net35) },
    new GruntTaskReferenceAssembly { GruntTask = privexchange, ReferenceAssembly = await context.GetReferenceAssemblyByName("System.XML.dll", Common.DotNetVersion.Net40) }
                );
            }
        }

        public async static Task InitializeRoles(RoleManager<IdentityRole> roleManager)
        {
            List<string> roles = new List<string> { "Administrator", "User", "Listener" };
            foreach (string role in roles)
            {
                if (!(await roleManager.RoleExistsAsync(role)))
                {
                    IdentityResult roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
    }
}
