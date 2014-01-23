using System;
using System.Linq;
using JetBrains.Metadata.Reader.API;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public static partial class NancyUtil
    {
        private static ISearchDomain GetSearchDomain(IPsiModule module, IModuleReferenceResolveContext context)
        {
            IPsiServices psiServices = module.GetPsiServices();
            ISearchDomain searchDomain = psiServices.SearchDomainFactory.CreateSearchDomain(
                module.GetPsiServices().Modules.GetModules()
                      .Where(m => m.References(module, context) || module.References(m, context))
                      .Prepend(module));

            return searchDomain;
        }

        private static ITypeElement GetNancyModuleInterface(IPsiModule module, IModuleReferenceResolveContext context)
        {
            return TypeFactory.CreateTypeByCLRName("Nancy.INancyModule", module, context).GetTypeElement();
        }
    }
}