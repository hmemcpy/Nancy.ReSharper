using JetBrains.ReSharper.Feature.Services.Asp.Caches;
using JetBrains.ReSharper.Psi;

namespace Nancy.ReSharper.Plugin.Daemon
{
    internal class NancyElementsCache : IMvcElementsCache
    {
        public NancyElementsCache(IPsiModule module)
        {
            CustomModelBinderAttribute = TypeFactory.CreateTypeByCLRName("System.Web.Mvc.CustomModelBinderAttribute", module);
            HttpPostedFileBase = TypeFactory.CreateTypeByCLRName("System.Web.HttpPostedFileBase", module);
            MvcControllerInterface = TypeFactory.CreateTypeByCLRName("Nancy.INancyModule", module).GetTypeElement();
            MvcControllerClassType = TypeFactory.CreateTypeByCLRName("Nancy.NancyModule", module);
            MvcControllerClass = MvcControllerClassType.GetTypeElement();
            MvcAsyncControllerClass = MvcControllerClass;
            MvcActionResultClassType = TypeFactory.CreateTypeByCLRName("System.Web.Mvc.ActionResult", module);
            MvcViewDataDictionaryClass = TypeFactory.CreateTypeByCLRName("System.Web.Mvc.ViewDataDictionary", module).GetTypeElement();
            MvcTypedViewDataDictionaryClass = TypeFactory.CreateTypeByCLRName("System.Web.Mvc.ViewDataDictionary`1", module).GetTypeElement();
            MvcHttpControllerInterface = TypeFactory.CreateTypeByCLRName("System.Web.Http.Controllers.IHttpController", module).GetTypeElement();
            MvcApiControllerClass = MvcControllerClass;
        }

        public IDeclaredType CustomModelBinderAttribute { get; private set; }
        public IDeclaredType HttpPostedFileBase { get; private set; }
        public IDeclaredType MvcActionResultClassType { get; private set; }
        public IDeclaredType MvcControllerClassType { get; private set; }
        public ITypeElement MvcControllerInterface { get; private set; }
        public ITypeElement MvcControllerClass { get; private set; }
        public ITypeElement MvcAsyncControllerClass { get; private set; }
        public ITypeElement MvcViewDataDictionaryClass { get; private set; }
        public ITypeElement MvcTypedViewDataDictionaryClass { get; private set; }
        public ITypeElement MvcHttpControllerInterface { get; private set; }
        public ITypeElement MvcApiControllerClass { get; private set; }
    }
}