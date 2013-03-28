using System;
using System.Collections.Generic;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.QuickFixes
{
    [Language(typeof(CSharpLanguage))]
    public class RazorCSharpMvcQuickFixTemplateProvider : NancyQuickFixTemplateProviderBase
    {
        private static readonly IDictionary<Pair<bool, bool>, Guid> Templates = new Dictionary<Pair<bool, bool>, Guid>
        {
            { Pair.Of(true, false),  new Guid("28930f00-63d1-49ef-9108-d49218df0568") },
            { Pair.Of(true, true),   new Guid("259949c3-530e-41ff-b0a8-6ba5c123b7b6") },
            { Pair.Of(false, false), new Guid("2bad39df-7bf3-44d5-8d98-1614a5ffef92") },
            { Pair.Of(false, true),  new Guid("2bad39df-7bf3-44d5-8d98-1614a5ffef92") }
        };

        public override Guid GetTemplateGuid(bool viewPage, bool withMasterpage)
        {
            return Templates[Pair.Of(viewPage, withMasterpage)];
        }

        public override string GetExtension(bool viewPage, bool withMasterpage)
        {
            return ".cshtml";
        }
    }
}