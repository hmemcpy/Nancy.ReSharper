using JetBrains.ProjectModel;
using JetBrains.ProjectModel.Properties;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Support;
using JetBrains.ReSharper.Feature.Services.Razor.CSharp.LiveTemplates.Support;
using Nancy.ReSharper.Plugin.CustomReferences;

namespace Nancy.ReSharper.Plugin.LiveTemplates
{
    [FileTemplates]
    public class NancyRazorCSharpFileTemplatesSupport : RazorCSharpFileTemplatesSupport
    {
        public override bool Accepts(IProject project)
        {
            if (!NancyCustomReferencesSettings.IsProjectReferencingNancyRazorViewEngine(project))
                return false;

            return Language == ProjectLanguage.UNKNOWN || Language == project.DefaultLanguage;
        }
    }
}