using System.Collections.Generic;
using JetBrains.ProjectModel.Properties;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Scope;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Support;
using JetBrains.ReSharper.Feature.Services.Razor.CSharp.LiveTemplates;

namespace Nancy.ReSharper.Plugin.LiveTemplates
{
    [FileTemplates]
    public class NancyRazorCSharpFileTemplatesSupport : NancyRazorFileTemplatesSupportBase
    {
        public override IEnumerable<ITemplateScopePoint> ScopePoints
        {
            get
            {
                yield return new InRazorCSharpProject();
            }
        }

        public override string Name
        {
            get
            {
                return "Razor.CSharp";
            }
        }

        protected override ProjectLanguage Language
        {
            get
            {
                return ProjectLanguage.CSHARP;
            }
        }
    }
}