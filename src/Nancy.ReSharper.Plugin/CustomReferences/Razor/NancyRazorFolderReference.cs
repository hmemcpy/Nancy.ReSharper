using System.IO;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Dependencies;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.Html.Impl.References;
using JetBrains.ReSharper.Psi.Html.Utils;
using JetBrains.ReSharper.Psi.Impl;
using JetBrains.ReSharper.Psi.Razor.References;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using Nancy.ReSharper.Plugin.Daemon;

namespace Nancy.ReSharper.Plugin.CustomReferences.Razor
{
    public sealed class NancyRazorFolderReference<TToken> : HtmlFolderReference<IExpression, TToken>, IRazorReference where TToken : class, ITreeNode
    {
        public NancyRazorFolderReference(IExpression owner, IQualifier qualifier, TToken token, TreeTextRange rangeWithin)
            : base(owner, qualifier, token, rangeWithin)
        {
            Assertion.Assert(owner.ConstantValue.IsString(), "expression is not string constant");
        }

        public override bool IsValid()
        {
            return base.IsValid() && myOwner.ConstantValue.IsString();
        }

        public override ISymbolTable GetReferenceSymbolTable(bool useReferenceName)
        {
            IPsiServices psiServices = myOwner.GetPsiServices();
            string name = GetName();
            IProject project = myOwner.GetProject();

            return NancyReferenceSymbolTableUtil.GetReferenceSymbolTable2RenameThis(psiServices, name, project);
        }
    }
}