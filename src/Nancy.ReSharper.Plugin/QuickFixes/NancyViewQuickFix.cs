using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using JetBrains.PsiFeatures.VisualStudio.Core.Intentions.QuickFixes.MVC;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Daemon.Asp.Highlightings;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Feature.Services.Bulbs;
using JetBrains.ReSharper.Intentions.Extensibility;
using JetBrains.ReSharper.Intentions.Extensibility.Menu;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.QuickFixes
{
    [QuickFix]
    public class NancyViewQuickFix : IQuickFix
    {
        [CanBeNull]
        private readonly IMvcViewReference reference;

        public NancyViewQuickFix([NotNull] AspConfigurableNotResolvedErrorHighlighting<IMvcReference> highlighting)
        {
            reference = highlighting.Reference as IMvcViewReference;
        }

        public void CreateBulbItems(BulbMenu menu, Severity severity)
        {
            // BUG HACK!
            // This is the worst and nastiest hack I've ever had to do.
            // At the moment, the ASPX quickfixes will be always enabled in any web project.

            // The reason is that in the base class, MvcQuickFixTemplateProviderBase, the 'IsAvailable' method is not virtual,
            // and in case of ASPX, it is set to always return true. This means, there's no possible way to override or intercept
            // the ASPX quickfixes provider.

            // In a Nancy project they are not needed, so I'm removing the ASPX items directly from the bulb menu's underlying dictionary.

            // I'm sorry.

            dynamic exposedMenu = ExposedObject.Exposed.From(menu);
            Dictionary<Anchor, BulbGroup> groups = exposedMenu.myGroups;
            if (groups != null)
            {
                BulbGroup quickfixes = groups[AnchorsForBulbMenuGroups.QuickFixesAnchor];
                dynamic exposedQuickFixes = ExposedObject.Exposed.From(quickfixes);
                List<BulbMenuItem> bulbMenuItems = exposedQuickFixes.myMenuItems;
                if (bulbMenuItems != null)
                {
                    bulbMenuItems.RemoveAll(item => item.BulbAction.Text.Contains("ASPX")); // *shudders!* - removes all ASPX menu items
                    bulbMenuItems.RemoveAll(item => item.BulbAction is MvcViewVSCommandBulbItem); // *shudders x2!* - removes "create view by VS"
                }
            }
        }

        public bool IsAvailable(IUserDataHolder cache)
        {
            if (reference == null || !reference.IsValid() || reference.CheckResolveResult() == ResolveErrorType.OK)
                return false;
            
            return !PathReferenceUtil.CheckPathChars(reference.GetName());
        }
    }
}
