using System;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public partial class NancyReferenceProviderFactory 
    {
        private NancyMvcReferenceProvider CreateProvider()
        {
            return new NancyMvcReferenceProvider();
        }
    }
}