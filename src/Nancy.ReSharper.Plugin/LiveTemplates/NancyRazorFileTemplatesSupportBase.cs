using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.Properties;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Scope;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Support;
using JetBrains.ReSharper.Feature.Services.Razor.LiveTemplates.Scope;
using Nancy.ReSharper.Plugin.CustomReferences;

namespace Nancy.ReSharper.Plugin.LiveTemplates
{
    public abstract class NancyRazorFileTemplatesSupportBase : IFileTemplatesSupport
    {
        public abstract string Name { get; }

        public virtual IEnumerable<ITemplateScopePoint> ScopePoints
        {
            get
            {
                if (Language == ProjectLanguage.UNKNOWN)
                    yield return new InAnyRazorProject();
                else
                    yield return new InRazorSpecificProject(Language);
            }
        }

        protected abstract ProjectLanguage Language { get; }

        public virtual bool Accepts(IProject project)
        {
            if (!NancyCustomReferencesSettings.IsProjectReferencingNancyRazorViewEngine(project))
                return false;

            return Language == ProjectLanguage.UNKNOWN || Language == project.DefaultLanguage;
        }
    }
}