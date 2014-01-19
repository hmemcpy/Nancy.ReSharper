using System;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Razor;
using JetBrains.ReSharper.Psi.Razor.CSharp.Impl.References;
using JetBrains.ReSharper.Psi.Razor.Impl.References;
using JetBrains.ReSharper.Psi.Razor.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences.Razor
{
    [ReferenceProviderFactory]
    public class NancyRazorCSharpReferenceProviderFactory : RazorCSharpReferenceProviderFactory, IReferenceProviderFactory
    {
        private readonly ISolution solution;
        private readonly IRazorServices razorServices;
        private readonly Func<IAssignmentExpression, Pair<IExpression, IExpression>> assignmentChecker;

        public NancyRazorCSharpReferenceProviderFactory(ISolution solution, IRazorServices razorServices)
            : base(solution, razorServices)
        {
            this.solution = solution;
            this.razorServices = razorServices;
            assignmentChecker = expr => Pair.Of((IExpression)expr.Dest, (IExpression)expr.Source);
        }

        IReferenceFactory IReferenceProviderFactory.CreateFactory(IPsiSourceFile sourceFile, IFile file)
        {
            if (!sourceFile.LanguageType.Is<RazorCSharpProjectFileType>())
                return null;
            if (file is IRazorFile)
                return new RazorReferenceProvider<IAssignmentExpression>(razorServices, assignmentChecker);
            if (file is ICSharpFile)
                return new NancyRazorCSharpCodeBehindReferenceProvider(solution, razorServices, assignmentChecker);
            
            return null;
        }
    }
}