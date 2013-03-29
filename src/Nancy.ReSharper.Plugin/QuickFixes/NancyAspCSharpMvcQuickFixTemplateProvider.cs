using JetBrains.ProjectModel;
using JetBrains.ReSharper.Intentions.Asp.CSharp.QuickFixes.Mvc;
using JetBrains.ReSharper.Intentions.Web.QuickFixes.Mvc;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;

namespace Nancy.ReSharper.Plugin.QuickFixes
{
    [Language(typeof(CSharpLanguage))]
    public class NancyCSharpMvcQuickFixTemplateProvider : CSharpMvcQuickFixTemplateProvider, IMvcQuickFixTemplateProvider
    {
        bool IMvcQuickFixTemplateProvider.IsAvailable(IProjectItem context)
        {
            // ReSharper will suggest the "default" quickfix items for MVC-like projects, which include ASPX views generation.
            // ASPX views are not used in Nancy, so we return false here to prevent R# from suggesting them.
            return false;
        }
    }
}