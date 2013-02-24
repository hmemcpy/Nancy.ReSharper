using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Resolve;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using JetBrains.Util.DataStructures;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public class NancyMvcReferenceProvider : NancyMvcReferenceProviderBase<ICSharpLiteralExpression, ICSharpArgumentsOwner, IMethodDeclaration>
    {
        public NancyMvcReferenceProvider([NotNull] NancyIndexer indexer, [NotNull] Version version)
            : base(indexer, version)
        {
        }

        protected override MvcActionReference<ICSharpLiteralExpression> GetMvcActionReference([NotNull] IExpression literal, [NotNull] ICSharpArgumentsOwner argumentsExpression)
        {
            return new NancyMvcActionReference(literal, argumentsExpression);
        }

        protected override IExpression GetMvcLiteral(ITreeNode element, out ICSharpArgumentsOwner expression, out string anonymousPropertyName)
        {
            expression = null;
            anonymousPropertyName = null;

            var csharpExpression = element as ICSharpExpression;
            if (csharpExpression == null)
                return null;

            if (CSharpArgumentNavigator.GetByValue(csharpExpression) == null && CSharpArgumentNavigator.GetByValue(
                AnonymousObjectCreationExpressionNavigator.GetByAnonymousInitializer(
                    AnonymousObjectInitializerNavigator.GetByMemberInitializer(
                        AnonymousMemberDeclarationNavigator.GetByExpression(csharpExpression)))) == null)
                return null;

            if (!csharpExpression.ConstantValue.IsString())
                return null;

            return NancyUtil.GetMvcLiteral(csharpExpression, out expression, out anonymousPropertyName);
        }

        protected override HybridCollection<string> GetExpressionNames(ICSharpArgumentsOwner expression)
        {
            ICSharpInvocationReference reference = expression.Reference;
            if (reference == null)
                return HybridCollection<string>.Empty;

            if (reference.HasMultipleNames)
                return new HybridCollection<string>(reference.GetAllNames().ToList());

            return new HybridCollection<string>(reference.GetName());
        }

        protected override MvcControllerReference<ICSharpLiteralExpression> GetMvcControllerReference([NotNull] IExpression literal, [NotNull] ICSharpArgumentsOwner argumentsExpression)
        {
            return new NancyMvcControllerReference(literal, argumentsExpression);
        }

        protected override MvcAreaReference<ICSharpLiteralExpression> GetMvcAreaReference([NotNull] IExpression literal)
        {
            return new NancyMvcAreaReference(literal);
        }

        protected override MvcViewReference<ICSharpLiteralExpression, IMethodDeclaration> GetMvcViewReference([NotNull] IExpression literal, ICollection<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>> names, MvcKind mvcKind, Version version)
        {
            return new NancyMvcViewReference(literal, names, mvcKind, version);
        }
    }
}