using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Tree;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public abstract partial class NancyMvcReferenceProviderBase<TLiteral, TExpression, TMethod>
        where TLiteral : class, ILiteralExpression
        where TExpression : class, IArgumentsOwner, IInvocationInfo, ITreeNode
        where TMethod : class, ITypeOwnerDeclaration, ITypeMemberDeclaration
    {
        private readonly MvcIndexer myIndexer;

        protected NancyMvcReferenceProviderBase([NotNull] MvcIndexer indexer, [NotNull] Version version)
        {
            myIndexer = indexer;
            myVersion = version;
        }

// ReSharper disable once UnusedParameter.Local
        private ICollection<string> GetAllMvcNames(TExpression expression)
        {
            return myIndexer.GetAllShortNames();
        }
    }
}