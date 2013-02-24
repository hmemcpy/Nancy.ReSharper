using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using JetBrains.Util.DataStructures;
using JetBrains.Util.Special;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public abstract class NancyMvcReferenceProviderBase<TLiteral, TExpression, TMethod> : IReferenceFactory
        where TLiteral : class, ILiteralExpression
        where TExpression : class, IArgumentsOwner, IInvocationInfo, ITreeNode
        where TMethod : class, ITypeOwnerDeclaration, ITypeMemberDeclaration
    {
        private readonly NancyIndexer myIndexer;
        private readonly Version myVersion;

        protected NancyMvcReferenceProviderBase([NotNull] NancyIndexer indexer, [NotNull] Version version)
        {
            myIndexer = indexer;
            myVersion = version;
        }

        public IReference[] GetReferences(ITreeNode element, IReference[] oldReferences)
        {
            if (oldReferences != null && oldReferences.Any() && oldReferences.All(reference =>
            {
                if (reference is IMvcReference && reference.GetTreeNode() == element)
                    return ((IMvcReference)reference).IsInternalValid;

                return false;
            }))
                return oldReferences;
            var expression1 = element as TExpression;
            if (expression1 != null && HasImplicitReference(expression1, myIndexer.GetAllShortNames()))
                return GetImplicitReferences(expression1).ToArray();
            TExpression argumentExpression;
            string anonymousPropertyName;
            IExpression mvcLiteral = GetMvcLiteral(element, out argumentExpression, out anonymousPropertyName);
            if (mvcLiteral == null)
                return EmptyArray<IReference>.Instance;
            IParameter parameter = mvcLiteral.GetContainingNode<IArgument>().IfNotNull(d => d.MatchingParameter).IfNotNull(p => p.Element);
            if (parameter == null)
                return EmptyArray<IReference>.Instance;
            var jt = parameter.GetMvcKinds().FirstOrDefault(_ => StringComparer.OrdinalIgnoreCase.Equals(_.B, anonymousPropertyName));
            if (jt == null)
                return EmptyArray<IReference>.Instance;
            switch (jt.A)
            {
                case MvcKind.Area:
                    return new IReference[]
                    {
                        GetMvcAreaReference(mvcLiteral)
                    };
                case MvcKind.Controller:
                    return new IReference[]
                    {
                        GetMvcControllerReference(mvcLiteral, argumentExpression)
                    };
                case MvcKind.Action:
                    return new IReference[]
                    {
                        GetMvcActionReference(mvcLiteral, argumentExpression)
                    };
                case MvcKind.View:
                case MvcKind.PartialView:
                case MvcKind.Master:
                case MvcKind.DisplayTemplate:
                case MvcKind.EditorTemplate:
                    var list = NancyUtil.GetModules(argumentExpression)
                        .DefaultIfEmpty(JetTuple.Of((string)null, (string)null, MvcUtil.DeterminationKind.Explicit, (ICollection<IClass>)null)).ToList();
                    return new IReference[]
                    {
                        GetMvcViewReference(mvcLiteral, list, jt.A, myVersion)
                    };
                default:
                    return EmptyArray<IReference>.Instance;
            }
        }

        protected virtual MvcViewReference<ICSharpLiteralExpression, IMethodDeclaration> GetMvcViewReference(IExpression literal, ICollection<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>> names, MvcKind mvcKind, Version version)
        {
            return new MvcViewReference<ICSharpLiteralExpression, IMethodDeclaration>(literal, names, mvcKind, version);
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

        private IEnumerable<IReference> GetImplicitReferences([NotNull] TExpression expression)
        {
            foreach (JetTuple<MvcKind, string, IAttributeInstance> jt in ((IEnumerable<IReference>)expression.GetFirstClassReferences())
                .Select(reference => reference.Resolve().DeclaredElement)
                .OfType<IMethod>()
                .Where(method => !(method is IAccessor))
                .SelectMany(method => (IEnumerable<JetTuple<MvcKind, string, IAttributeInstance>>)method.GetMvcKinds()))
            {
                switch (jt.A)
                {
                    case MvcKind.Controller:
                        yield return new MvcImplicitControllerReference<TExpression>(expression);
                        continue;
                    case MvcKind.Action:
                        yield return new MvcImplicitActionReference<TExpression>(expression);
                        continue;
                    case MvcKind.View:
                    case MvcKind.PartialView:
                        yield return new MvcImplicitViewReference<TExpression, TLiteral, TMethod>(expression, jt.A, myVersion);
                        continue;
                    default:
                        continue;
                }
            }
        }

        [CanBeNull]
        protected abstract IExpression GetMvcLiteral([NotNull] ITreeNode element, [CanBeNull] out TExpression expression, [CanBeNull] out string anonymousPropertyName);

        protected abstract HybridCollection<string> GetExpressionNames([NotNull] TExpression expression);

        private bool HasImplicitReference([NotNull] TExpression expression, [NotNull] ICollection<string> names)
        {
            HybridCollection<string> expressionNames = GetExpressionNames(expression);
            if (expressionNames.Count == 0)
                return false;

            return Enumerable.Any(expressionNames, names.Contains);
        }

        public bool HasReference(ITreeNode element, ICollection<string> names)
        {
            TExpression expression1;
            string anonymousPropertyName;
            IExpression mvcLiteral = GetMvcLiteral(element, out expression1, out anonymousPropertyName);
            if (mvcLiteral != null)
                return names.Contains((string)mvcLiteral.ConstantValue.Value, StringComparer.OrdinalIgnoreCase);
            var expression2 = element as TExpression;

            return expression2 != null && HasImplicitReference(expression2, names);
        }
    }
}