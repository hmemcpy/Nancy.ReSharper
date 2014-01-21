using System;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Razor;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences.Razor
{
    public class NancyRazorCSharpCodeBehindReferenceProvider : NancyRazorCodeBehindReferenceProvider<IAssignmentExpression, ICSharpLiteralExpression>
    {
        public NancyRazorCSharpCodeBehindReferenceProvider(ISolution solution, IRazorServices razorServices, Func<IAssignmentExpression, Pair<IExpression, IExpression>> assigmentChecker)
            : base(solution, razorServices, assigmentChecker)
        {
        }

        protected override bool IsAppropriateNode(ITreeNode element)
        {
            var csharpExpression = element as ICSharpExpression;
            if (csharpExpression == null) return false;
            return AssignmentExpressionNavigator.GetBySource(csharpExpression) != null ||
                   CSharpArgumentNavigator.GetByValue(csharpExpression) != null;
        }
    }
}