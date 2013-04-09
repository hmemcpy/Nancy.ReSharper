using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.Settings;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Daemon.Asp.Stages;
using JetBrains.ReSharper.Daemon.UsageChecking;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Search;
using Nancy.ReSharper.Plugin.CustomReferences;

namespace Nancy.ReSharper.Plugin.Daemon
{
    [DaemonStage(StagesBefore = new[] { typeof(LanguageSpecificDaemonStage), typeof(CollectUsagesStage) })]
    public class NancyDaemonStage : IDaemonStage
    {
        private readonly SearchDomainFactory searchDomainFactory;

        public NancyDaemonStage(SearchDomainFactory searchDomainFactory)
        {
            this.searchDomainFactory = searchDomainFactory;
        }

        public IEnumerable<IDaemonStageProcess> CreateProcess(IDaemonProcess process, IContextBoundSettingsStore settings, DaemonProcessKind processKind)
        {
            process.Solution.GetPsiServices().DependencyStore.AddDependency(MvcSpecificFileImageContributor.Dependency);

            IProjectFile projectFile = process.SourceFile.ToProjectFile();
            if (projectFile == null)
            {
                return Enumerable.Empty<IDaemonStageProcess>();
            }

            Version version;
            if (!projectFile.IsProjectReferencingNancy(out version))
            {
                return Enumerable.Empty<IDaemonStageProcess>();
            }

            return new[]
            {
                new MvcDaemonStageProcess(searchDomainFactory,
                                          process,
                                          process.GetStageProcess<CollectUsagesStageProcess>(),
                                          settings)
            };
        }

        public ErrorStripeRequest NeedsErrorStripe(IPsiSourceFile sourceFile, IContextBoundSettingsStore settings)
        {
            return ErrorStripeRequest.NONE;
        }

        internal static bool IsNancyProject(IProjectElement project)
        {
            Version version;
            return NancyCustomReferencesSettings.IsProjectReferencingNancy(project, out version);
        }
    }
}