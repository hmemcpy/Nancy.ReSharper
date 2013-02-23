using System;
using JetBrains.Annotations;
using JetBrains.DataFlow;
using JetBrains.Metadata.Utils;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi.Web.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public static class NancyCustomReferencesSettings
    {
        private static readonly AssemblyNameInfo NancyAssemblyName = new AssemblyNameInfo("Nancy");

        [ContractAnnotation("=>true,version:notnull;=>false,version:null")]
        public static bool IsApplied([NotNull] IProperty<bool> enabled, [CanBeNull] IProjectItem projectItem, [CanBeNull] out Version version)
        {
            version = null;
            if (!enabled.Value)
                return false;
            
            return IsProjectReferencingNancy(projectItem, out version);
        }

        /// <summary>
        /// Is project referencing Nancy assembly?
        /// </summary>
        /// <param name="projectElement"/><param name="version">Version of MVC, if referenced</param>
        [ContractAnnotation("=>true,version:notnull;=>false,version:null")]
        public static bool IsProjectReferencingNancy([CanBeNull] IProjectElement projectElement, [CanBeNull] out Version version)
        {
            version = null;

            AssemblyNameInfo referencedAssembly;
            if (!ReferencedAssembliesService.IsProjectReferencingAssemblyByName(projectElement, NancyAssemblyName, out referencedAssembly))
                return false;
            version = referencedAssembly.Version;
            return true;
        }
    }
}