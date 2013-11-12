using System;
using System.Linq;
using JetBrains.Metadata.Reader.API;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public static partial class NancyUtil
    {
        private static ISearchDomain GetSearchDomain(IPsiModule module, IArgumentsOwner argumentsOwner)
        {
            IModuleReferenceResolveContext context = argumentsOwner.GetResolveContext();
            var searchDomainFactory = argumentsOwner.GetSolution().GetComponent<SearchDomainFactory>();

            ISearchDomain searchDomain = searchDomainFactory.CreateSearchDomain(
                module.GetPsiServices().Modules
                      .GetModules()
                      .Where(m => m.References(module, context) || module.References(m, context))
                      .Prepend(module));

            return searchDomain;
        }

        private static ITypeElement GetNancyModuleInterface(IArgumentsOwner argumentsOwner, IPsiModule module)
        {
            return TypeFactory.CreateTypeByCLRName("Nancy.INancyModule", module, argumentsOwner.GetResolveContext()).GetTypeElement();            
        }
    }
}