using System;
using System.Linq;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    // note: don't enter! here be dragons!
    // for ReSharper 7.x
    public static partial class NancyUtil
    {
        private static ISearchDomain GetSearchDomain(IPsiModule module, IArgumentsOwner argumentsOwner)
        {
            ISearchDomain searchDomain = SearchDomainFactory.Instance.CreateSearchDomain(
                module.GetPsiServices().ModuleManager
                      .GetAllModules().Where(m => m.References(module) || module.References(m))
                      .Prepend(module));

            return searchDomain;
        }

        private static ITypeElement GetNancyModuleInterface(IArgumentsOwner argumentsOwner, IPsiModule module)
        {
            return TypeFactory.CreateTypeByCLRName("Nancy.INancyModule", module).GetTypeElement();
        }
    }
}