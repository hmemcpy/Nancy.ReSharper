using System;
using System.IO;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.Caches;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.Html.Utils;
using JetBrains.ReSharper.Psi.Impl;
using JetBrains.ReSharper.Psi.Impl.Resolve;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.ReSharper.Psi.Web.Util;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences.Razor
{
    internal static class NancyReferenceSymbolTableUtil
    {

        public static ISymbolTable GetReferenceSymbolTableByLocation(IPsiServices psiServices, string name, IProject project)
        {
            var mvcCache = psiServices.GetComponent<MvcCache>();
            var symbolTable = new SymbolTable(psiServices);
            try
            {
                bool hasExtension = Path.HasExtension(name);

                foreach (string location in mvcCache.GetLocations(project, MvcUtil.GetViewLocationType(MvcKind.View, null)))
                {
                    string path = string.Format(location, name, null, null);
                    if (hasExtension)
                    {
                        path = Path.ChangeExtension(path, null);
                    }
                    symbolTable.AddSymbol(new PathDeclaredElement(psiServices, FileSystemPath.Parse(path)));
                }
            }
            catch (InvalidPathException)
            {
            }

            return symbolTable.Distinct(PathInfoComparer.Instance);
        }

        public static ISymbolTable GetReferenceSymbolTable(IPsiServices psiServices, [CanBeNull] string view, [CanBeNull] IProject project)
        {
            if (project == null || String.IsNullOrEmpty(view))
            {
                return EmptySymbolTable.INSTANCE;
            }

            if (view.IndexOfAny(FileSystemDefinition.InvalidPathChars) != -1 || view == "???")
            {
                return EmptySymbolTable.INSTANCE;
            }

            return GetReferenceSymbolTableByLocation(psiServices, view, project);
        }
    }
}