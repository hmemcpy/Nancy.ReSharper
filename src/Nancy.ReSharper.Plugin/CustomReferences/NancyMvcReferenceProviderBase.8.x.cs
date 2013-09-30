using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.Search;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Caches;
using JetBrains.ReSharper.Psi.Impl.Reflection2.ExternalAnnotations;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public abstract partial class NancyMvcReferenceProviderBase<TLiteral, TExpression, TMethod> : IReferenceFactory
        where TLiteral : class, ILiteralExpression
        where TExpression : class, IArgumentsOwner, IInvocationInfo, ITreeNode
        where TMethod : class, ITypeOwnerDeclaration, ITypeMemberDeclaration
    {
        private static readonly Key<ICollection<string>> AllMvcNamesKey = new Key<ICollection<string>>("AllMvcMembersNames");


        protected NancyMvcReferenceProviderBase([NotNull] Version version)
        {
            myVersion = version;
        }

        private static ICollection<string> GetAllMvcNames(TExpression expression)
        {
            ICollection<string> val = expression.UserData.GetData(AllMvcNamesKey);
            if (val == null)
            {
                ISolution solution = expression.GetSolution();
                var attributeNames = solution.GetComponent<MvcAttributeNames>();
                val = new HashSet<string>(attributeNames.AttributeClrNamesToWatch.SelectMany(
                    typeName => solution.GetMembersByAttributeName(typeName.ShortName)), StringComparer.OrdinalIgnoreCase);
                expression.UserData.PutData(AllMvcNamesKey, val);
            }

            return val;
        }
    }
}