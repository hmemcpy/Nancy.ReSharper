using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Feature.Services.CSharp.CodeCompletion.SmartCompletion;
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
    public class NancyRazorLayoutReference<TToken> : RazorFileReference<TToken>, IRazorLayoutReference, IMvcViewReference, ISmartCompleatebleReference
        where TToken : class, ITreeNode
    {
        public NancyRazorLayoutReference(IExpression owner, IQualifier qualifier, TToken token,
            TreeTextRange rangeWithin, ProjectFileType expectedFileType, bool noCircular, bool allowEmptyName)
            : base(owner, qualifier, token, rangeWithin, expectedFileType, noCircular, allowEmptyName)
        {
            Assertion.Assert(owner.ConstantValue.IsString(), "expression is not string constant");
            
            ResolveFilter = element =>
            {
                var pathDeclaredElement = element as IPathDeclaredElement;
                if (pathDeclaredElement == null || pathDeclaredElement.GetProjectItem() == null)
                {
                    return false;
                }

                if (pathDeclaredElement.Path.ExistsDirectory)
                {
                    return false;
                }
                return true;
            };
        }

        public Func<IDeclaredElement, bool> ResolveFilter { get; private set; }

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

        public Refers RefersToDeclaredElement(IDeclaredElement declaredElement)
        {
            IResolveResult resolveResult = Resolve().Result;
            if (Equals(resolveResult.DeclaredElement, declaredElement))
                return Refers.YES;

            return resolveResult.Candidates.Any(element => Equals(element, declaredElement)) ? Refers.MAYBE : Refers.NO;
        }

        public override ResolveResultWithInfo GetResolveResult(ISymbolTable symbolTable, string referenceName)
        {
            ResolveResultWithInfo resolveResult = LocalResolveWithoutCache();

            resolveResult = new ResolveResultWithInfo(resolveResult.Result, GetResolveInfo(resolveResult.Info));

            return resolveResult;
        }

        private ResolveResultWithInfo LocalResolveWithoutCache()
        {
            List<IDeclaredElement> list = GetReferenceSymbolTable(true).GetAllSymbolInfos().Select(info => info.GetDeclaredElement()).Where(ResolveFilter).ToList();
            switch (list.Count)
            {
                case 0:
                    return ResolveResultWithInfo.Unresolved;
                case 1:
                    return new ResolveResultWithInfo(ResolveResultFactory.CreateResolveResult(list.First()), ResolveErrorType.OK);
                default:
                    return new ResolveResultWithInfo(ResolveResultFactory.CreateResolveResult(list), ResolveErrorType.MULTIPLE_CANDIDATES);
            }
        }

        private IResolveInfo GetResolveInfo(IResolveInfo resolveInfo)
        {
            return MvcViewReference<ICSharpLiteralExpression, IMethodDeclaration>.CheckViewResolveResult(
                MvcUtil.CheckMvcResolveResult(resolveInfo, MvcKind.View), this);
        }

        public MvcKind MvcKind
        {
            get { return MvcKind.View; }
        }

        public bool IsInternalValid
        {
            get { return IsValid(); }
        }

        public FileSystemPath GetControllerFolder()
        {
            return NancyUtil.GetControllerFolder(myOwner.GetProject(), null);
        }
    }
}