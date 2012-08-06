using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Resolve;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util.DataStructures;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public class CSharpNancyReferenceProvider : NancyReferenceProvider<ICSharpLiteralExpression, ICSharpArgumentsOwner, IMethodDeclaration>
    {
        private readonly NancyIndexer nancyIndexer;
        private readonly IPsiSourceFile sourceFile;

        public CSharpNancyReferenceProvider(NancyIndexer nancyIndexer, IPsiSourceFile sourceFile)
            : base(nancyIndexer, sourceFile)
        {
            this.nancyIndexer = nancyIndexer;
            this.sourceFile = sourceFile;
        }

        protected override IExpression GetNancyLiteral(ITreeNode element, out ICSharpArgumentsOwner expression, out string anonymousPropertyName)
        {
            expression = null;
            anonymousPropertyName = null;
            
            var csharpExpression = element as ICSharpExpression;
            if (csharpExpression == null)
                return null;

            if (CSharpArgumentNavigator.GetByValue(csharpExpression) == null && 
                CSharpArgumentNavigator.GetByValue(
                    AnonymousObjectCreationExpressionNavigator.GetByAnonymousInitializer(
                        AnonymousObjectInitializerNavigator.GetByMemberInitializer(
                            AnonymousMemberDeclarationNavigator.GetByExpression(csharpExpression)))) == null)
                return null;
            
            if (!csharpExpression.ConstantValue.IsString())
                return null;

            return MvcUtil.GetNancyLiteral(csharpExpression, out expression, out anonymousPropertyName);
        }

        protected override HybridCollection<string> GetExpressionNames(ICSharpArgumentsOwner expression)
        {
            IReferenceExpressionReference reference = null;

            var elementAccessExpression = expression as IElementAccessExpression;
            if (elementAccessExpression != null)
            {
                var referenceExpression = elementAccessExpression.Operand as IReferenceExpression;
                if (referenceExpression != null)
                {
                    reference = referenceExpression.Reference;
                }
            }

            if (reference == null)
            {
                return HybridCollection<string>.Empty;
            }

            return reference.HasMultipleNames 
                ? new HybridCollection<string>(reference.GetAllNames().ToList()) 
                : new HybridCollection<string>(reference.GetName());
        }
    }
}