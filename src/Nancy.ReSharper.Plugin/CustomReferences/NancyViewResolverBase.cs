using System.Collections.Generic;
using System.Linq;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public abstract class NancyViewResolverBase : IMvcViewResolver
    {
        private readonly string[] allExtensions;
        private readonly IDictionary<MvcViewLocationType, ICollection<string>> DefaultViewLocations;

        private Dictionary<MvcViewLocationType, ICollection<string>> InitializeDefaultViews()
        {
            return new Dictionary<MvcViewLocationType, ICollection<string>>
            {
                { MvcViewLocationType.Unknown, EmptyList<string>.InstanceList },
                {
                    MvcViewLocationType.View, GetAllPaths(
                        "~\\{0}",
                        "~\\views\\{0}"
                        )
                }
            };
        }

        protected NancyViewResolverBase(params string[] allExtensions)
        {
            this.allExtensions = allExtensions;

            DefaultViewLocations = InitializeDefaultViews();
        }

        private string[] GetAllPaths(params string[] viewLocations)
        {
            return viewLocations.SelectMany(view => allExtensions.Select(extension => view + extension)).ToArray();
        }

        public abstract bool IsApplicable(IProject project);

        public IDictionary<MvcViewLocationType, ICollection<string>> Values { get; private set; }
    }
}