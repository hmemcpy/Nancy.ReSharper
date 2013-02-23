using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.Application;
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

namespace Nancy.ReSharper.Plugin.Daemon
{
    public class NancyDaemonStageProcess : IDaemonStageProcess
    {
        private readonly SearchDomainFactory searchDomainFactory;
        private readonly IDaemonProcess daemonProcess;
        private readonly CollectUsagesStageProcess collectUsagesStageProcess;
        private readonly IContextBoundSettingsStore settingsStore;

        public NancyDaemonStageProcess(SearchDomainFactory searchDomainFactory, 
            [NotNull] IDaemonProcess daemonProcess, 
            [CanBeNull] CollectUsagesStageProcess collectUsagesStageProcess, 
            IContextBoundSettingsStore settingsStore)
        {
            this.searchDomainFactory = searchDomainFactory;
            this.daemonProcess = daemonProcess;
            this.collectUsagesStageProcess = collectUsagesStageProcess;
            this.settingsStore = settingsStore;
        }

        private static readonly IDictionary<MvcResolveErrorType, Func<IMvcReference, IHighlighting>>
            errorHighlightings = new Dictionary<MvcResolveErrorType, Func<IMvcReference, IHighlighting>>
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

        public void Execute(Action<DaemonStageResult> commiter)
        {
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
                        highlighting = CheckResolved(reference,
                            (_ => (IHighlighting)new MvcControllerHighlighting(_)));
                        break;
                    case MvcKind.Action:
                        highlighting = CheckResolved(reference,
                            (_ => (IHighlighting)new MvcActionHighlighting(_)));
                        break;
                    case MvcKind.View:
                    case MvcKind.PartialView:
                    case MvcKind.Master:
                    case MvcKind.DisplayTemplate:
                    case MvcKind.EditorTemplate:
                        highlighting = CheckResolved(reference,
                            (_ => (IHighlighting) new MvcViewHighlighting((IMvcViewReference)_)));
                        break;
                    default:
                        highlighting = null;
                        break;
                }
                if (highlighting == null)
                {
                    return;
                }

                consumer.AddHighlighting(highlighting, GetMvcReferenceHighlightingRange
                                                                      (reference),
                                                                  reference.GetTreeNode().GetContainingFile(),
                                                                  new Severity?(), null, new OverlapResolveKind?(),
                                                                  new int?());
            });

            daemonProcess.SourceFile.EnumerateNonInjectedPsiFiles().ForEach(
                psiFile => psiFile.ProcessDescendants(referenceProcessor));

        }

        private static DocumentRange GetMvcReferenceHighlightingRange(IMvcReference reference)
        {
            return reference.GetName().IsEmpty() ? reference.GetTreeNode().GetDocumentRange() 
                                                 : reference.GetDocumentRange();
        }


        public IDaemonProcess DaemonProcess
        {
            get { return daemonProcess; }
        }

        private static IHighlighting CheckArea(IMvcReference reference)
        {
            if (reference.GetName().IsEmpty() &&
                (reference).CheckResolveResult() == MvcResolveErrorType.MVC_AREA_NOT_RESOLVED)
            {
                return null;
            }

            return CheckResolved(reference, (_ => (IHighlighting)new MvcAreaHighlighting(_)));
        }

        [CanBeNull]
        private static IHighlighting CheckResolved([NotNull] IMvcReference reference,
                                                   [NotNull] Func<IMvcReference, IHighlighting> highlighter)
        {
            ResolveErrorType resolveErrorType = reference.CheckResolveResult();
            if (resolveErrorType == ResolveErrorType.IGNORABLE)
                return null;
            if (resolveErrorType is MvcResolveErrorType)
                return errorHighlightings.GetOrCreateValue((MvcResolveErrorType)resolveErrorType, 
                    () => (Func<IMvcReference, IHighlighting>)(_ => (IHighlighting)new AspConfigurableNotResolvedErrorHighlighting<IMvcReference>(_, null, new object[0])))(reference);
            
            return highlighter(reference);
        }
    }
}