using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;

namespace Nancy.ReSharper.Plugin.CustomReferences.ViewResolvers
{
    [MvcViewResolver]
    public class NancyMarkdownViewResolver : NancyViewResolverBase
    {
        public NancyMarkdownViewResolver()
            : base(".md", ".markdown")
        {   
        }

        public override bool IsApplicable(IProject project)
        {
        }
    }
}