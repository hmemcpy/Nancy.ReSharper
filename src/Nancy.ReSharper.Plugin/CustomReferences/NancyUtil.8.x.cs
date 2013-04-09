using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.Application.Progress;
using JetBrains.Metadata.Reader.API;
using JetBrains.ReSharper.Feature.Services.Asp.Caches;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public static partial class NancyUtil
    {
        public static OneToListMap<string, IClass> GetAvailableControllers([NotNull] IPsiModule module,
                                                                           IModuleReferenceResolveContext context,
                                                                           [CanBeNull] ICollection<string> areas = null,
                                                                           bool includingIntermediateControllers = false,
                                                                           ITypeElement baseClass = null)
        {
            ISearchDomain searchDomain = SearchDomainFactory.Instance.CreateSearchDomain(
                    module.GetPsiServices().Modules.GetModules()
                    .Where(m => m.References(module, context) || module.References(m, context)).Prepend(new[] { module }));

            return GetAvailableModules(module, searchDomain, context, includingIntermediateControllers, baseClass);
        }

        public static OneToListMap<string, IClass> GetAvailableModules([NotNull] IPsiModule module, [NotNull] ISearchDomain searchDomain, IModuleReferenceResolveContext context, bool includingIntermediateControllers = false, ITypeElement baseClass = null)
        {
            IMvcElementsCache instance = MvcElementsCache.GetInstance(module, context);
            ITypeElement[] typeElementArray;
            if (baseClass != null)
            {
                if (!baseClass.IsDescendantOf(instance.MvcControllerInterface) && !baseClass.IsDescendantOf(instance.MvcHttpControllerInterface))
                    return new OneToListMap<string, IClass>(0);
                typeElementArray = new ITypeElement[1]
        {
          baseClass
        };
            }
            else
                typeElementArray = new ITypeElement[2]
        {
          instance.MvcControllerInterface,
          instance.MvcHttpControllerInterface
        };
            List<IClass> list = new List<IClass>();
            foreach (ITypeElement typeElement in typeElementArray)
            {
                if (typeElement != null)
                {
                    if (typeElement is IClass)
                        list.Add((IClass)typeElement);
                    module.GetPsiServices().Finder.FindInheritors<IClass>(typeElement, searchDomain, FindResultConsumerExtensions.ConsumeDeclaredElements<IClass>((ICollection<IClass>)list), (IProgressIndicator)NullProgressIndicator.Instance);
                }
            }
            IEnumerable<IClass> source = Enumerable.Where<IClass>((IEnumerable<IClass>)list, (Func<IClass, bool>)(@class => @class.GetAccessRights() == AccessRights.PUBLIC));
            if (!includingIntermediateControllers)
                source = Enumerable.Where<IClass>(source, (Func<IClass, bool>)(@class =>
                {
                    if (!@class.IsAbstract)
                        return @class.ShortName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase);
                    else
                        return false;
                }));
            return new OneToListMap<string, IClass>(Enumerable.GroupBy<IClass, string, KeyValuePair<string, IList<IClass>>>(source, new Func<IClass, string>(MvcUtil.GetControllerName), (Func<string, IEnumerable<IClass>, KeyValuePair<string, IList<IClass>>>)((name, enumerable) => new KeyValuePair<string, IList<IClass>>(name, (IList<IClass>)Enumerable.ToList<IClass>(enumerable)))), (IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase);
        }

    }
}