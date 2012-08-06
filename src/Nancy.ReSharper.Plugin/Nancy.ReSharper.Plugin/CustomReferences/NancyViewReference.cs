using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public class NancyViewReference<TLiteral, TMethod> : MvcReference<TLiteral>, IMvcViewReference
        where TLiteral : ILiteralExpression
        where TMethod : class, ITypeOwnerDeclaration, ITypeMemberDeclaration
    {
        public NancyViewReference([NotNull] IExpression owner)
            : base(owner)
        {
        }

        public override ISymbolTable GetReferenceSymbolTable(bool useReferenceName)
        {
            throw new System.NotImplementedException();
        }

        public override IReference BindTo(IDeclaredElement element)
        {
            throw new System.NotImplementedException();
        }

        public override MvcKind MvcKind
        {
            get { return MvcKind.View; }
        }

        public FileSystemPath GetBasePath()
        {
            throw new System.NotImplementedException();
        }

        public ISymbolFilter[] GetPathFilters()
        {
            throw new System.NotImplementedException();
        }

        public string GetViewName(FileSystemPath path)
        {
            throw new System.NotImplementedException();
        }

        public FileSystemPath GetControllerFolder()
        {
            throw new System.NotImplementedException();
        }
    }
}