using System.Collections.Generic;
using JetBrains.Application;
using JetBrains.ReSharper.Feature.Services.Asp.CSharp.LiveTemplates;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Context;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Scope;

namespace Nancy.ReSharper.Plugin.LiveTemplates
{
    [ShellComponent]
    public class NancyAspCSharpProjectScopeProvider : AspCSharpProjectScopeProvider
    {
        public override IEnumerable<ITemplateScopePoint> ProvideScopePoints(TemplateAcceptanceContext context)
        {
            yield break;
        }
    }
}