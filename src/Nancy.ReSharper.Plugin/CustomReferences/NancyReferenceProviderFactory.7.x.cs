using System;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public partial class NancyReferenceProviderFactory 
    {
        private NancyMvcReferenceProvider CreateProvider(Version version)
        {
            return new NancyMvcReferenceProvider(solution.GetComponent<MvcIndexer>(), version);
        }
    }
}