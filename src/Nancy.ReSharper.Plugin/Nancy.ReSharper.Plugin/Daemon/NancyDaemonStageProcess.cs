using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.Progress;
using JetBrains.Application.Settings;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Daemon.Asp.Highlightings;
using JetBrains.ReSharper.Daemon.Asp.Highlightings.Mvc;
using JetBrains.ReSharper.Daemon.Asp.Stages;
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
        private readonly SearchDomainFactory searchDomainFactory;
        private readonly IDaemonProcess daemonProcess;
        private readonly CollectUsagesStageProcess collectUsagesStageProcess;
        private readonly IContextBoundSettingsStore settingsStore;

        private static readonly IDictionary<MvcResolveErrorType, Func<IMvcReference, IHighlighting>> ErrorHighlightings = new Dictionary<MvcResolveErrorType, Func<IMvcReference, IHighlighting>>()
        {
            {
                MvcResolveErrorType.MVC_CONTROLLER_NOT_RESOLVED,
                _ => (IHighlighting) new MvcConfigurableControllerNotResolvedErrorHighlighting(_)
            },
            {
                MvcResolveErrorType.MVC_ACTION_NOT_RESOLVED,
                _ => (IHighlighting) new MvcConfigurableActionNotResolvedErrorHighlighting(_)
            },
            {
                MvcResolveErrorType.MVC_VIEW_NOT_RESOLVED,
                _ => (IHighlighting) new MvcConfigurableViewNotResolvedErrorHighlighting(_)
            },
            {
                MvcResolveErrorType.MVC_PARTIAL_VIEW_NOT_RESOLVED,
                _ => (IHighlighting) new MvcConfigurablePartialViewNotResolvedErrorHighlighting(_)
            },
            {
                MvcResolveErrorType.MVC_AREA_NOT_RESOLVED,
                _ => (IHighlighting) new MvcConfigurableAreaNotResolvedErrorHighlighting(_)
            },
            {
                MvcResolveErrorType.MVC_TEMPLATE_NOT_RESOLVED,
                _ => (IHighlighting) new MvcConfigurableTemplateNotResolvedErrorHighlighting(_)
            },
            {
                MvcResolveErrorType.MVC_MASTERPAGE_NOT_RESOLVED,
                _ => (IHighlighting) new MvcConfigurableMasterpageNotResolvedErrorHighlighting(_)
            }
        };


        public NancyDaemonStageProcess(SearchDomainFactory searchDomainFactory, IDaemonProcess daemonProcess, CollectUsagesStageProcess collectUsagesStageProcess, IContextBoundSettingsStore settingsStore)
        {
            this.searchDomainFactory = searchDomainFactory;
            this.daemonProcess = daemonProcess;
            this.collectUsagesStageProcess = collectUsagesStageProcess;
            this.settingsStore = settingsStore;
        }

        public void Execute(Action<DaemonStageResult> commiter)
        {
            if (collectUsagesStageProcess != null)
            {
                foreach (IClass @class in NancyUtil.GetAvailableModules(daemonProcess.PsiModule,
                                                                        searchDomainFactory.CreateSearchDomain(daemonProcess.SourceFile), true).SelectMany(_ => (IEnumerable<IClass>)_.Value))
                {
                    InterruptableActivityCookie.CheckAndThrow();

                    collectUsagesStageProcess.SetElementState(@class, UsageState.ACCESSED | UsageState.TYPEOF);
                    //foreach (IDeclaredElement element in MvcUtil.GetControllerActions(@class, daemonProcess.PsiModule))
                    //    collectUsagesStageProcess.SetElementState(element, UsageState.ACCESSED | UsageState.USED_EXCEPT_BASE_CALL | UsageState.RETURN_VALUE_USED | UsageState.CANNOT_BE_PRIVATE | UsageState.CANNOT_BE_INTERNAL | UsageState.CANNOT_BE_PROTECTED | UsageState.CANNOT_BE_STATIC);
                }
            }
            var consumer = new DefaultHighlightingConsumer(this, settingsStore);
            var referenceProcessor = new RecursiveReferenceProcessor<IMvcReference>(reference =>
            {
                //Debugger.Break();
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
                        highlighting = CheckResolved(reference, _ => (IHighlighting)new MvcViewHighlighting((IMvcViewReference)_));
                        break;
                    default:
                        highlighting = null;
                        break;
                }
                if (highlighting == null)
                    return;
                consumer.AddHighlighting(highlighting, GetMvcReferenceHighlightingRange(reference), reference.GetTreeNode().GetContainingFile(), new Severity?(), null, new OverlapResolveKind?(), new int?());
            });
            daemonProcess.SourceFile.EnumerateNonInjectedPsiFiles().ForEach(psiFile => psiFile.ProcessDescendants(referenceProcessor));
            commiter(new DaemonStageResult(consumer.Highlightings));

        }

        private static DocumentRange GetMvcReferenceHighlightingRange(IMvcReference reference)
        {
            if (reference.GetName().IsEmpty())
                return reference.GetTreeNode().GetDocumentRange();

            return reference.GetDocumentRange();
        }


        private IHighlighting CheckArea(IMvcReference reference)
        {
            if (reference.GetName().IsEmpty() && reference.CheckResolveResult() == MvcResolveErrorType.MVC_AREA_NOT_RESOLVED)
                return null;

            return CheckResolved(reference, _ => (IHighlighting)new MvcAreaHighlighting(_));
        }


        [CanBeNull]
        private static IHighlighting CheckResolved([NotNull] IMvcReference reference, [NotNull] Func<IMvcReference, IHighlighting> highlighter)
        {
            ResolveErrorType resolveErrorType = reference.CheckResolveResult();
            if (resolveErrorType == ResolveErrorType.IGNORABLE)
                return null;
            var errorType = resolveErrorType as MvcResolveErrorType;
            if (errorType != null)
                return ErrorHighlightings.GetOrCreateValue(errorType, () => (Func<IMvcReference, IHighlighting>)(_ => (IHighlighting)new AspConfigurableNotResolvedErrorHighlighting<IMvcReference>(_, null, new object[0])))(reference);

            return highlighter(reference);
        }
        public IDaemonProcess DaemonProcess
        {
            get { return daemonProcess; }
        }
    }
}