using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;

namespace Nancy.ReSharper.Plugin.CustomReferences.ViewResolvers
{
    [MvcViewResolver]
    public class NancyRazorViewResolver : NancyViewResolverBase
    {
        public NancyRazorViewResolver()
            : base(".cshtml")
        {
        }

        public override bool IsApplicable(IProject project)
        {
            return NancyCustomReferencesSettings.IsProjectReferencingNancyRazorViewEngine(project);
        }
    }
}