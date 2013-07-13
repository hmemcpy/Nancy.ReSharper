using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
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

        private ICollection<string> GetAllMvcNames(TExpression expression)
        {
            ICollection<string> val = expression.UserData.GetData(AllMvcNamesKey);
            if (val == null)
            {
                ISolution solution = expression.GetSolution();
                val = new HashSet<string>(NancyMvcAttributeNames.AllMvcAttributeNames.SelectMany(
                    typeName => solution.GetMembersByAttributeName(typeName.ShortName)), StringComparer.OrdinalIgnoreCase);
                expression.UserData.PutData(AllMvcNamesKey, val);
            }
            return val;

        }
    }

    [PsiSharedComponent]
    public class NancyMvcAttributeNames : IExternalAnnotationsAttributeWatcher
    {
        private static readonly IClrTypeName[] MvcControllerAttributeNames = new IClrTypeName[1]
        {
            new ClrTypeName(typeof(AspMvcControllerAttribute).FullName)
        };

        private static readonly IClrTypeName[] MvcActionAttributeNames = new IClrTypeName[1]
        {
            new ClrTypeName(typeof(AspMvcActionAttribute).FullName)
        };

        private static readonly IClrTypeName[] MvcViewAttributeNames = new IClrTypeName[5]
        {
            new ClrTypeName(typeof(AspMvcViewAttribute).FullName),
            new ClrTypeName(typeof(AspMvcPartialViewAttribute).FullName),
            new ClrTypeName(typeof(AspMvcMasterAttribute).FullName),
            new ClrTypeName(typeof(AspMvcDisplayTemplateAttribute).FullName),
            new ClrTypeName(typeof(AspMvcEditorTemplateAttribute).FullName)
        };

        internal static readonly IClrTypeName[] AllMvcAttributeNames =
            MvcActionAttributeNames.Concat(MvcControllerAttributeNames)
                                   .Concat(MvcViewAttributeNames).ToArray();

        public IEnumerable<IClrTypeName> AttributeClrNamesToWatch
        {
            get
            {
                return AllMvcAttributeNames;
            }
        }
    }

}