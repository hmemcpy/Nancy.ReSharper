using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Feature.Services.CSharp.CodeCompletion.SmartCompletion;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public class NancyMvcActionReference : MvcActionReference<ICSharpLiteralExpression>, ISmartCompleatebleReference
    {
        public NancyMvcActionReference([NotNull] IExpression owner, IArgumentsOwner argumentsExpression)
            : base(owner, argumentsExpression)
        {
        }
    }
}