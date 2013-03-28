using System;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Intentions.Web.QuickFixes.Mvc;
using Nancy.ReSharper.Plugin.CustomReferences;

namespace Nancy.ReSharper.Plugin.QuickFixes
{
    public abstract class NancyQuickFixTemplateProviderBase : IMvcQuickFixTemplateProvider
    {
        public abstract Guid GetTemplateGuid(bool viewPage, bool withMasterpage);

        public abstract string GetExtension(bool viewPage, bool withMasterpage);

        public string GetQuickFixTitle(bool viewPage, bool withMasterpage)
        {
            if (!viewPage)
                return "Create Razor partial view '{0}'";
            return !withMasterpage ? "Create Razor view '{0}'" : "Create Razor view '{0}' with layout";
        }

        public bool IsAvailable(IProjectItem context)
        {
            return NancyCustomReferencesSettings.IsProjectReferencingNancyRazorViewEngine(context);
        }
    }
}