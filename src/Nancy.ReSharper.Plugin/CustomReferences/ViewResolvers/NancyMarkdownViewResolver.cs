using JetBrains.Metadata.Utils;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;

namespace Nancy.ReSharper.Plugin.CustomReferences.ViewResolvers
{
    [MvcViewResolver]
    public class NancyMarkdownViewResolver : NancyViewResolverBase
    {
        private static readonly AssemblyNameInfo NancyMarkdownAssemblyName = new AssemblyNameInfo("Nancy.ViewEngines.Markdown");

        public NancyMarkdownViewResolver()
            : base(".md", ".markdown")
        {   
        }

        public override bool IsApplicable(IProject project)
        {
            return project.IsProjectReferencingAssembly(NancyMarkdownAssemblyName);
        }
    }
}