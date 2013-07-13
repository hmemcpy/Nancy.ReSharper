using System;
using JetBrains.Annotations;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public partial class NancyMvcReferenceProvider
    {
        public NancyMvcReferenceProvider([NotNull] Version version)
            : base(version)
        {
        }
    }
}