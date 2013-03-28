using JetBrains.ProjectModel.Properties;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Support;

namespace Nancy.ReSharper.Plugin.LiveTemplates
{
    [FileTemplates]
    public class NancyRazorFileTemplatesSupport : NancyRazorFileTemplatesSupportBase
    {
        public override string Name
        {
            get
            {
                return "Razor";
            }
        }

        protected override ProjectLanguage Language
        {
            get
            {
                return ProjectLanguage.UNKNOWN;
            }
        }
    }
}