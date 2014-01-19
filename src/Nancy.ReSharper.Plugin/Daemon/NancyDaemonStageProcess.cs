using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.Settings;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Daemon.Asp.Highlightings;
using JetBrains.ReSharper.Daemon.Asp.Highlightings.Mvc;
using JetBrains.ReSharper.Daemon.Stages;
using JetBrains.ReSharper.Daemon.UsageChecking;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using Nancy.ReSharper.Plugin.CustomReferences;

namespace Nancy.ReSharper.Plugin.Daemon
{
    public class NancyDaemonStageProcess : IDaemonStageProcess
    {
        private static readonly IDictionary<MvcResolveErrorType, Func<IMvcReference, IHighlighting>> ErrorHighlightings;

        private readonly CollectUsagesStageProcess collectUsagesStageProcess;
        private readonly SearchDomainFactory searchDomainFactory;
        private readonly IContextBoundSettingsStore settingsStore;

        static NancyDaemonStageProcess()
        {
            ErrorHighlightings = new Dictionary<MvcResolveErrorType, Func<IMvcReference, IHighlighting>>
            {
                {
                    MvcResolveErrorType.MVC_CONTROLLER_NOT_RESOLVED,
                    _ => (IHighlighting)new MvcConfigurableControllerNotResolvedErrorHighlighting(_)
                },
                {
                    MvcResolveErrorType.MVC_ACTION_NOT_RESOLVED,
                    _ => (IHighlighting)new MvcConfigurableActionNotResolvedErrorHighlighting(_)
                },
                {
                    MvcResolveErrorType.MVC_VIEW_NOT_RESOLVED,
                    _ => (IHighlighting)new MvcConfigurableViewNotResolvedErrorHighlighting(_)
                },
                {
                    MvcResolveErrorType.MVC_PARTIAL_VIEW_NOT_RESOLVED,
                    _ => (IHighlighting)new MvcConfigurablePartialViewNotResolvedErrorHighlighting(_)
                },
                {
                    MvcResolveErrorType.MVC_AREA_NOT_RESOLVED,
                    _ => (IHighlighting)new MvcConfigurableAreaNotResolvedErrorHighlighting(_)
                },
                {
                    MvcResolveErrorType.MVC_TEMPLATE_NOT_RESOLVED,
                    _ => (IHighlighting)new MvcConfigurableTemplateNotResolvedErrorHighlighting(_)
                },
                {
                    MvcResolveErrorType.MVC_MASTERPAGE_NOT_RESOLVED,
                    _ => (IHighlighting)new MvcConfigurableMasterpageNotResolvedErrorHighlighting(_)
                }
            };
        }

        public NancyDaemonStageProcess(SearchDomainFactory searchDomainFactory, [NotNull] IDaemonProcess daemonProcess,
            [CanBeNull] CollectUsagesStageProcess collectUsagesStageProcess, IContextBoundSettingsStore settingsStore)
        {
            DaemonProcess = daemonProcess;
            this.searchDomainFactory = searchDomainFactory;
            this.collectUsagesStageProcess = collectUsagesStageProcess;
            this.settingsStore = settingsStore;
        }

        public IDaemonProcess DaemonProcess { get; private set; }

        public void Execute(Action<DaemonStageResult> committer)
        {
            MarkModulesAsUsed(collectUsagesStageProcess);

            var consumer = new DefaultHighlightingConsumer(this, settingsStore);
            var referenceProcessor = new RecursiveReferenceProcessor<IMvcReference>(reference =>
            {
                InterruptableActivityCookie.CheckAndThrow();
                IHighlighting highlighting;
                switch (reference.MvcKind)
                {
                    case MvcKind.Area:
                        highlighting = CheckArea(reference);
                        break;
                    case MvcKind.Controller:
                        highlighting = CheckResolved(reference, _ => (IHighlighting)new MvcControllerHighlighting(_));
                        break;
                    case MvcKind.Action:
                        highlighting = CheckResolved(reference, _ => (IHighlighting)new MvcActionHighlighting(_));
                        break;
                    case MvcKind.View:
                    case MvcKind.PartialView:
                    case MvcKind.Master:
                    case MvcKind.DisplayTemplate:
                    case MvcKind.EditorTemplate:
                    case MvcKind.Template:
                        highlighting = CheckResolved(reference,
                            _ => (IHighlighting)new MvcViewHighlighting((IMvcViewReference)_));
                        break;
                    default:
                        highlighting = null;
                        break;
                }
                if (highlighting == null)
                {
                    return;
                }
                consumer.AddHighlighting(highlighting, GetMvcReferenceHighlightingRange(reference),
                    reference.GetTreeNode().GetContainingFile());
            });

            foreach (IFile file in DaemonProcess.SourceFile.EnumerateDominantPsiFiles())
            {
                file.ProcessDescendants(referenceProcessor);
            }

            committer(new DaemonStageResult(consumer.Highlightings));
        }

        private void MarkModulesAsUsed([CanBeNull] CollectUsagesStageProcess usagesStageProcess)
        {
            if (usagesStageProcess != null)
            {
                IEnumerable<IClass> nancyModules = NancyUtil.GetAvailableModules(DaemonProcess.PsiModule,
                    searchDomainFactory.CreateSearchDomain(DaemonProcess.SourceFile),
                    DaemonProcess.SourceFile.ResolveContext, true, null).SelectMany(_ => _.Value);

                foreach (IClass @class in nancyModules)
                {
                    InterruptableActivityCookie.CheckAndThrow();
                    collectUsagesStageProcess.SetElementState(@class, UsageState.ACCESSED | UsageState.TYPEOF);
                }
            }
        }

        private static IHighlighting CheckArea(IMvcReference reference)
        {
            if (reference.GetName().IsEmpty() &&
                reference.CheckResolveResult() == MvcResolveErrorType.MVC_AREA_NOT_RESOLVED)
            {
                return null;
            }
            return CheckResolved(reference, _ => (IHighlighting)new MvcAreaHighlighting(_));
        }

        private static DocumentRange GetMvcReferenceHighlightingRange(IMvcReference reference)
        {
            if (reference.GetName().IsEmpty())
            {
                return reference.GetTreeNode().GetDocumentRange();
            }
            return reference.GetDocumentRange();
        }

        [CanBeNull]
        private static IHighlighting CheckResolved([NotNull] IMvcReference reference, [NotNull] Func<IMvcReference, IHighlighting> highlighter)
        {
            ResolveErrorType resolveErrorType = reference.CheckResolveResult();
            if (resolveErrorType == ResolveErrorType.IGNORABLE)
            {
                return null;
            }
            
            var mvcResolveErrorType = resolveErrorType as MvcResolveErrorType;
            if (mvcResolveErrorType != null)
            {
                return ErrorHighlightings.GetOrCreateValue(mvcResolveErrorType, () => _ => new AspConfigurableNotResolvedErrorHighlighting<IMvcReference>(_, null, new object[0]))(reference);
            }

            return highlighter(reference);
        }
    }
}