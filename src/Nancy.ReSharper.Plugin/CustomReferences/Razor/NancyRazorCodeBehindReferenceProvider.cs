using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.Razor;
using JetBrains.ReSharper.Psi.Razor.Impl.References;
using JetBrains.ReSharper.Psi.Razor.References;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Web.References;
using JetBrains.Util;
using JetBrains.Util.Special;

namespace Nancy.ReSharper.Plugin.CustomReferences.Razor
{
    public abstract class NancyRazorCodeBehindReferenceProvider<TAssignmentExpression, TLiteralExpression> : IReferenceFactory
        where TAssignmentExpression : class, ITreeNode
        where TLiteralExpression : class, ILiteralExpression
    {
        private static readonly string RazorSectionExternalAttributeName = typeof(RazorSectionAttribute).Name;
        private static readonly string RazorLayoutExternalAttributeName = typeof(RazorLayoutAttribute).Name;

        private readonly Func<TAssignmentExpression, Pair<IExpression, IExpression>> assignmentChecker;
        private readonly IRazorServices razorServices;
        private readonly ISolution solution;

        protected NancyRazorCodeBehindReferenceProvider(ISolution solution, IRazorServices razorServices,
            Func<TAssignmentExpression, Pair<IExpression, IExpression>> assignmentChecker)
        {
            this.solution = solution;
            this.razorServices = razorServices;
            this.assignmentChecker = assignmentChecker;
        }

        public virtual IReference[] GetReferences(ITreeNode element, IReference[] oldReferences)
        {
            if (ResolveUtil.CheckThatAllReferencesBelongToElement<IRazorReference>(oldReferences, element))
            {
                return oldReferences;
            }
            if (!IsAppropriateNode(element))
            {
                return EmptyArray<IReference>.Instance;
            }

            IExpression annotatedSectionExpression = razorServices.GetAnnotatedLiteralExpression(element, RazorSectionExternalAttributeName, assignmentChecker);
            if (annotatedSectionExpression is TLiteralExpression && annotatedSectionExpression.ConstantValue.IsString())
            {
                return new IReference[]
                {
                    new RazorSectionDeclarationReference<TLiteralExpression>(annotatedSectionExpression)
                };
            }

            IExpression annotatedLiteralExpression = razorServices.GetAnnotatedLiteralExpression(element, RazorLayoutExternalAttributeName, assignmentChecker);
            if (annotatedLiteralExpression == null || !annotatedLiteralExpression.ConstantValue.IsString())
            {
                return EmptyArray<IReference>.Instance;
            }

            IPsiSourceFile sourceFile = element.GetDocumentRange()
                                               .Document.IfNotNull(_ => _.GetPsiSourceFiles(solution),
                                                   EmptyList<IPsiSourceFile>.InstanceList)
                                               .Concat(element.GetSourceFile())
                                               .WhereNotNull()
                                               .FirstOrDefault();

            FileSystemPath location = sourceFile.GetLocation();
            PathQualifier pathQualifier = (!location.IsEmpty) ? new PathQualifier(solution, location.Directory) : null;

            return new IReference[]
            {
                new NancyRazorLayoutReference<ITreeNode>(annotatedLiteralExpression, pathQualifier,
                    annotatedLiteralExpression,
                    TreeTextRange.FromLength(annotatedLiteralExpression.GetTextLength()),
                    sourceFile.IfNotNull(_ => _.LanguageType), true, true)
            };
        }

        public virtual bool HasReference(ITreeNode element, ICollection<string> names)
        {
            if (!IsAppropriateNode(element))
            {
                return false;
            }
            var expression = element as IExpression;
            if (expression == null)
            {
                return false;
            }
            ConstantValue constantValue = expression.ConstantValue;
            if (!constantValue.IsString())
            {
                return false;
            }
            var stringValue = (string)constantValue.Value;
            if (stringValue.IsNullOrEmpty())
            {
                return false;
            }
            return names.Any(_ => stringValue.IndexOf(_, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        protected abstract bool IsAppropriateNode(ITreeNode element);
    }
}