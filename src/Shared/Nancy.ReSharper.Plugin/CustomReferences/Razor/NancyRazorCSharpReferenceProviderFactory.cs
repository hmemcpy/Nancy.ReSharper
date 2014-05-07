using System;
using System.Diagnostics;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Psi.Razor;
using JetBrains.ReSharper.Psi.Razor.CSharp.Impl.References;
using JetBrains.ReSharper.Psi.Razor.Impl.Generate;
using JetBrains.ReSharper.Psi.Razor.Impl.References;
using JetBrains.ReSharper.Psi.Razor.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Web.Cache;
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
            {
                bool isNancyRazorPage = IsNancyRazorPage(sourceFile);
                if (isNancyRazorPage)
                {
                    return new NancyRazorCSharpCodeBehindReferenceProvider(solution, razorServices, assignmentChecker);
                }

                return new RazorCSharpCodeBehindReferenceProvider(solution, razorServices, assignmentChecker);
            }

            return null;
        }

        private static bool IsNancyRazorPage(IPsiSourceFile sourceFile)
        {
             var project = sourceFile.GetProject();
            if (!project.IsProjectReferencingNancy() || !project.IsProjectReferencingNancyRazorViewEngine())
            {
                return false;
            }

            string pageBaseType = WebConfigCache.GetData(sourceFile).GetRazorBasePageType(isCSharp: true);
            return !string.IsNullOrWhiteSpace(pageBaseType) && pageBaseType.StartsWith("Nancy.ViewEngines.Razor.NancyRazorViewBase");
        }
    }
}