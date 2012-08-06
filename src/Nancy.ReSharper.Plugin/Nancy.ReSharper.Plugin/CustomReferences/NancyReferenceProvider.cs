using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using JetBrains.Util.DataStructures;
using JetBrains.Util.Special;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public abstract class NancyReferenceProvider<TLiteral, TExpression, TMethod> : IReferenceFactory
        where TLiteral : class, ILiteralExpression
        where TExpression : class, IArgumentsOwner, IInvocationInfo, ITreeNode
        where TMethod : class, ITypeOwnerDeclaration, ITypeMemberDeclaration
    {
        private readonly NancyIndexer nancyIndexer;
        private readonly IProject project;
        private readonly IPsiSourceFile sourceFile;

        protected NancyReferenceProvider(NancyIndexer nancyIndexer, IPsiSourceFile sourceFile)
        {
            this.nancyIndexer = nancyIndexer;
            this.sourceFile = sourceFile;

            project = sourceFile.GetProject();
        }

        public IReference[] GetReferences(ITreeNode element, IReference[] oldReferences)
        {
            if (oldReferences != null && oldReferences.Any() &&
                (oldReferences.OfType<IMvcReference>().All(mvcReference => mvcReference.IsInternalValid) &&
                 oldReferences.All(reference => reference.GetTreeNode() == element)))
            {
                return oldReferences;
            }

            //var expressionElement = element as TExpression;
            //if (expressionElement != null && HasImplicitReference(expressionElement, nancyIndexer.GetAllShortNames()))
            //{
            //    return GetImplicitReferences(expressionElement).ToArray();
            //}

            TExpression expression;
            string anonymousPropertyName;
            IExpression nancyLiteral = GetNancyLiteral(element, out expression, out anonymousPropertyName);
            if (nancyLiteral == null)
            {
                return EmptyArray<IReference>.Instance;
            }

            IParameter parameter = nancyLiteral.GetContainingNode<IArgument>()
                .IfNotNull((d => d.MatchingParameter)).IfNotNull((p => p.Element));

            if (parameter == null)
            {
                return EmptyArray<IReference>.Instance;
            }

            var jetTuple = ((parameter).GetMvcKinds()).FirstOrDefault();
            if (jetTuple == null)
            {
                return EmptyArray<IReference>.Instance;
            }
            switch (jetTuple.A)
            {
                case MvcKind.Area:
                    return new[] 
                    { 
                        (IReference)GetMvcAreaReference(nancyLiteral) 
                    };
                case MvcKind.Controller:
                    return new IReference[]
                    {
                        GetMvcControllerReference(nancyLiteral, expression)
                    };
                case MvcKind.Action:
                    return new IReference[]
                    {
                        GetMvcActionReference(nancyLiteral, expression)
                    };
                case MvcKind.View:
                case MvcKind.PartialView:
                case MvcKind.Master:
                case MvcKind.DisplayTemplate:
                case MvcKind.EditorTemplate:
                    var list = (ICollection<JetTuple<string, string, JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind, ICollection<IClass>>>)
                        MvcUtil.GetControllers(expression).ToList();
                    return new IReference[1]
                    {
                        this.GetMvcViewReference(nancyLiteral, list, jetTuple.A, null)
                    };
                //case MvcKind.PathReference:
                //    if (!(nancyLiteral is TLiteral))
                //    {
                //        return EmptyArray<IReference>.Instance;
                //    }
                //    var baseQualifier = (IQualifier)jetTuple.C.PositionParameters().Take(1)
                //        .SelectNotNull(_ => _.ConstantValue.Value as string)
                //        .Select(path => new PathQualifier(project, path)).FirstOrDefault();
                //    return HtmlPathReferenceUtil.CreatePathAndIdReferences(
                //        element, nancyLiteral, baseQualifier,
                //        ((el, qualifier, token, range) => 
                //            (IPathReference) new HtmlFolderLateBoundReference<ITreeNode, ITreeNode>(el, qualifier, token, range)),
                //        ((el, qualifier, token, range) => 
                //            (IPathReference) new HtmlFileLateBoundReference<ITreeNode, ITreeNode>(el, qualifier, token, range)));
                default:
                    return EmptyArray<IReference>.Instance;
            }
        }

        protected virtual MvcAreaReference<TLiteral> GetMvcAreaReference(IExpression literal)
        {
            return new MvcAreaReference<TLiteral>(literal);
        }

        protected virtual MvcControllerReference<TLiteral> GetMvcControllerReference(IExpression literal, TExpression argumentsExpression)
        {
            return new MvcControllerReference<TLiteral>(literal, argumentsExpression);
        }

        protected virtual MvcActionReference<TLiteral> GetMvcActionReference(IExpression literal, TExpression argumentsExpression)
        {
            return new MvcActionReference<TLiteral>(literal, argumentsExpression);
        }

        protected virtual NancyViewReference<TLiteral, TMethod> GetMvcViewReference(IExpression literal, ICollection<JetTuple<string, string, JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind, ICollection<IClass>>> names, MvcKind mvcKind, Version version)
        {
            return new NancyViewReference<TLiteral, TMethod>(literal/*, names, mvcKind, version*/);
        }

        public bool HasReference(ITreeNode element, ICollection<string> names)
        {
            TExpression expression;
            string anonymousPropertyName;
            IExpression nancyLiteral = GetNancyLiteral(element, out expression, out anonymousPropertyName);
            if (nancyLiteral != null)
                return names.Contains((string)nancyLiteral.ConstantValue.Value, StringComparer.OrdinalIgnoreCase);
            var otherExpression = element as TExpression;
            
            return otherExpression != null && HasImplicitReference(otherExpression, names);
        }

        private bool HasImplicitReference([NotNull] TExpression expression, [NotNull] ICollection<string> names)
        {
            HybridCollection<string> expressionNames = GetExpressionNames(expression);
            if (expressionNames.Count == 0)
            {
                return false;
            }
            
            return Enumerable.Any(expressionNames, str => names.Contains(str));
        }

        private IEnumerable<IReference> GetImplicitReferences([NotNull] TExpression expression)
        {
            foreach (var jetTuple in ((IEnumerable<IReference>)expression.GetFirstClassReferences()).Select(
                        (reference => reference.Resolve().DeclaredElement)).OfType<IMethod>().Where(
                             (method => !(method is IAccessor))).SelectMany(
                                 (method =>
                                  (IEnumerable<JetTuple<MvcKind, string, IAttributeInstance>>)(method).GetMvcKinds())))
            {
                switch (jetTuple.A)
                {
                    case MvcKind.Controller:
                        yield return new MvcImplicitControllerReference<TExpression>(expression);
                        continue;
                    case MvcKind.Action:
                        yield return new MvcImplicitActionReference<TExpression>(expression);
                        continue;
                    case MvcKind.View:
                    case MvcKind.PartialView:
                        yield return
                            new MvcImplicitViewReference<TExpression, TLiteral, TMethod>(expression, jetTuple.A, null);
                        continue;
                    default:
                        continue;
                }
            }
        }

        [CanBeNull]
        protected abstract IExpression GetNancyLiteral([NotNull] ITreeNode element, [CanBeNull] out TExpression expression, [CanBeNull] out string anonymousPropertyName);
        
        protected abstract HybridCollection<string> GetExpressionNames([NotNull] TExpression expression);
    }
}