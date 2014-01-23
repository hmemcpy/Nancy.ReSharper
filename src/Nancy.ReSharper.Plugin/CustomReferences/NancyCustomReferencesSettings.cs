using System;
using JetBrains.Annotations;
using JetBrains.Metadata.Utils;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi.Web.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public static class NancyCustomReferencesSettings
    {
        private static readonly AssemblyNameInfo NancyAssemblyName = new AssemblyNameInfo("Nancy");
        private static readonly AssemblyNameInfo NancyRazorAssemblyName = new AssemblyNameInfo("Nancy.ViewEngines.Razor");

        public static bool IsProjectReferencingNancy([CanBeNull] this IProjectElement projectElement)
        {
            AssemblyNameInfo referencedAssembly;
            if (!ReferencedAssembliesService.IsProjectReferencingAssemblyByName(projectElement, NancyAssemblyName, out referencedAssembly))
                return false;

            return true;
        }

        public static bool IsProjectReferencingNancyRazorViewEngine([CanBeNull] this IProjectElement projectElement)
        {
            AssemblyNameInfo referencedAssembly;
            return ReferencedAssembliesService.IsProjectReferencingAssemblyByName(projectElement, NancyRazorAssemblyName, out referencedAssembly);
        }

        public static bool IsProjectReferencingAssembly([CanBeNull] this IProjectElement projectElement, AssemblyNameInfo assemblyNameInfo)
        {
            AssemblyNameInfo referencedAssembly;
            return ReferencedAssembliesService.IsProjectReferencingAssemblyByName(projectElement, assemblyNameInfo, out referencedAssembly);
        }
    }
}