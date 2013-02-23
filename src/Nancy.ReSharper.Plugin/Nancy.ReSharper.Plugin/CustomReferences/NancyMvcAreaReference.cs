using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Feature.Services.CSharp.CodeCompletion.SmartCompletion;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public class NancyMvcAreaReference : MvcAreaReference<ICSharpLiteralExpression>, ISmartCompleatebleReference
    {
        public NancyMvcAreaReference([NotNull] IExpression owner)
            : base(owner)
        {
        }
    }
}