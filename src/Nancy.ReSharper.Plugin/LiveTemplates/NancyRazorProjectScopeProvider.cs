﻿using System;
using System.Collections.Generic;
using JetBrains.Application;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Context;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Scope;
using JetBrains.ReSharper.Feature.Services.Razor.LiveTemplates.Scope;
using JetBrains.ReSharper.Psi.Web.Util;
using JetBrains.Util;
using Nancy.ReSharper.Plugin.CustomReferences;

namespace Nancy.ReSharper.Plugin.LiveTemplates
{
    [ShellComponent]
    public class NancyRazorProjectScopeProvider : ScopeProvider
    {
        public NancyRazorProjectScopeProvider()
        {
            Creators.AddRange(new List<Func<string, ITemplateScopePoint>>
            {
                TryToCreate<InAnyRazorProject>
            });
        }

        public override IEnumerable<ITemplateScopePoint> ProvideScopePoints(TemplateAcceptanceContext context)
        {
            IProject project = context.GetProject();
            if (project != null && NancyCustomReferencesSettings.IsProjectReferencingNancyRazorViewEngine(project))
                yield return new InAnyRazorProject();
        }
    }
}