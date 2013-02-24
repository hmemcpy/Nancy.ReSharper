using System.Collections.Generic;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.Util;
using Nancy.ReSharper.Plugin.Daemon;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    [MvcViewResolver]
    public class NancyViewResolver : IMvcViewResolver
    {
        private static readonly IDictionary<MvcViewLocationType, ICollection<string>> DefaultViewLocations = InitializeDefaultViews();

        private static Dictionary<MvcViewLocationType, ICollection<string>> InitializeDefaultViews()
        {
            return new Dictionary<MvcViewLocationType, ICollection<string>>
            {
                { MvcViewLocationType.Unknown, EmptyList<string>.InstanceList },
                {
                    MvcViewLocationType.View, new[]
                    {
                        "~\\{0}.html",
                        "~\\{0}.htm",
                        "~\\{0}.sshtml",
                    }
                }
            };
        }

        public bool IsApplicable(IProject project)
        {
            return NancyDaemonStage.IsNancyProject(project);
        }

        public IDictionary<MvcViewLocationType, ICollection<string>> Values
        {
            get { return DefaultViewLocations; }
        }
    }
}