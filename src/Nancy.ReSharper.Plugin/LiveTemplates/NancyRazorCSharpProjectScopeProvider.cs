using System;
using System.Collections.Generic;
using JetBrains.Application;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.Properties;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Context;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Scope;
using JetBrains.ReSharper.Feature.Services.Razor.CSharp.LiveTemplates;
using Nancy.ReSharper.Plugin.CustomReferences;

namespace Nancy.ReSharper.Plugin.LiveTemplates
{
    [ShellComponent]
    public class NancyRazorCSharpProjectScopeProvider : RazorCSharpProjectScopeProvider
    {
        public override IEnumerable<ITemplateScopePoint> ProvideScopePoints(TemplateAcceptanceContext context)
        {
            IProject project = context.GetProject();
            if (project != null && project.IsProjectReferencingNancyRazorViewEngine())
            {
                ProjectLanguage lang = project.DefaultLanguage;
                if (lang == ProjectLanguage.CSHARP)
                    yield return new InRazorCSharpProject();
            }
        }
    }
}