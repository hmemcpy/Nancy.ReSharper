using System;
using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public partial class NancyMvcReferenceProvider
    {
        public NancyMvcReferenceProvider([NotNull] MvcIndexer indexer, [NotNull] Version version)
            : base(indexer, version)
        {
        }
    }
}