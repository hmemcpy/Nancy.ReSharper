using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CSharp.LiveTemplates.Support;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Support;

namespace Nancy.ReSharper.Plugin.LiveTemplates
{
    [FileTemplates]
    public class NancyAspCSharpFileTemplatesSupport : AspCSharpFileTemplatesSupport
    {
        public override bool Accepts(IProject project)
        {
            return false;
        }
    }
}