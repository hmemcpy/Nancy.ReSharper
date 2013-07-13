using System;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public partial class NancyReferenceProviderFactory 
    {
        private NancyMvcReferenceProvider CreateProvider(Version version)
        {
            return new NancyMvcReferenceProvider(version);
        }
    }
}