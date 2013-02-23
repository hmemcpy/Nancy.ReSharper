using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.Settings;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Daemon.Asp.Stages;
using JetBrains.ReSharper.Daemon.UsageChecking;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Search;
using Nancy.ReSharper.Plugin.CustomReferences;

namespace Nancy.ReSharper.Plugin.Daemon
{
    [DaemonStage(StagesBefore = new[] { typeof(LanguageSpecificDaemonStage), typeof(CollectUsagesStage) })]
    public class NancyDaemonStage : IDaemonStage
    {
        private readonly SearchDomainFactory searchDomainFactory;
        private readonly ISettingsOptimization settingsOptimization;

        public NancyDaemonStage(SearchDomainFactory searchDomainFactory, ISettingsOptimization settingsOptimization)
        {
            this.searchDomainFactory = searchDomainFactory;
            this.settingsOptimization = settingsOptimization;
        }

        public IEnumerable<IDaemonStageProcess> CreateProcess(IDaemonProcess process, IContextBoundSettingsStore settings, DaemonProcessKind processKind)
        {
            process.Solution.GetPsiServices().DependencyStore.AddDependency(MvcSpecificFileImageContributor.Dependency);

            IProjectFile projectFile = process.SourceFile.ToProjectFile();
            if (projectFile == null)
            {
                return Enumerable.Empty<IDaemonStageProcess>();
            }

            if (!IsNancyProject(projectFile))
            {
                return Enumerable.Empty<IDaemonStageProcess>();
            }

            return new[]
            {
                new NancyDaemonStageProcess(searchDomainFactory,
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