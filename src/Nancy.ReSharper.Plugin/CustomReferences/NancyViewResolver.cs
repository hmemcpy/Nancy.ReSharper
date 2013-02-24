using System;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using Nancy.ReSharper.Plugin.Daemon;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    [MvcViewResolver]
    public class NancyViewResolver : NancyViewResolverBase
    {
        public NancyViewResolver()
            : base(".htm", ".html", ".sshtml")
        {
            
        }

        public override bool IsApplicable(IProject project)
        {
            return NancyDaemonStage.IsNancyProject(project);
        }
    }
}