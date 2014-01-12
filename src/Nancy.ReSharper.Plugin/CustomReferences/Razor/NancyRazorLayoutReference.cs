using System;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.Razor.Impl.References;
using JetBrains.ReSharper.Psi.Razor.References;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences.Razor
{
    public class NancyRazorLayoutReference<TToken> : RazorFileReference<TToken>, IRazorLayoutReference
        where TToken : class, ITreeNode
    {
        public NancyRazorLayoutReference(IExpression owner, IQualifier qualifier, TToken token,
            TreeTextRange rangeWithin, ProjectFileType expectedFileType, bool noCircular, bool allowEmptyName)
            : base(owner, qualifier, token, rangeWithin, expectedFileType, noCircular, allowEmptyName)
        {
            Assertion.Assert(owner.ConstantValue.IsString(), "expression is not string constant");
        }

        public override ISymbolTable GetReferenceSymbolTable(bool useReferenceName)
        {
            IPsiServices psiServices = myOwner.GetPsiServices();
            string name = GetName();
            IProject project = myOwner.GetProject();

            return NancyReferenceSymbolTableUtil.GetReferenceSymbolTable(psiServices, name, project);
        }

        public override string GetName()
        {
            return myOwner.ConstantValue.Value as string ?? "";
        }

        public override bool IsValid()
        {
            return base.IsValid() && myOwner.ConstantValue.IsString();
        }

        public override ResolveResultWithInfo GetResolveResult(ISymbolTable symbolTable, string referenceName)
        {
            ResolveResultWithInfo resolveResult = base.GetResolveResult(symbolTable, referenceName);

            resolveResult = new ResolveResultWithInfo(resolveResult.Result, GetResolveInfo(resolveResult.Info));

            return resolveResult;
        }

        private IResolveInfo GetResolveInfo(IResolveInfo resolveInfo)
        {
            return MvcViewReference<ICSharpLiteralExpression, IMethodDeclaration>.CheckViewResolveResult(
                MvcUtil.CheckMvcResolveResult(resolveInfo, MvcKind.View), this);
        }
    }
}