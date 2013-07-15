using System;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Intentions.Razor.CSharp.QuickFixes.Mvc;
using JetBrains.ReSharper.Intentions.Web.QuickFixes.Mvc;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using Nancy.ReSharper.Plugin.CustomReferences;

namespace Nancy.ReSharper.Plugin.QuickFixes
{
    [Language(typeof(CSharpLanguage))]
    public class NancyRazorCSharpMvcQuickFixTemplateProvider : RazorCSharpMvcQuickFixTemplateProvider, IMvcQuickFixTemplateProvider
    {
        bool IMvcQuickFixTemplateProvider.IsAvailable(IProjectItem context)
        {
            return context.IsProjectReferencingNancyRazorViewEngine();
        }
    }
}