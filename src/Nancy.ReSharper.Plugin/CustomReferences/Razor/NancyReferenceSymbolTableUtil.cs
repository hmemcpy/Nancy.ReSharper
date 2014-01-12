using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.Caches;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.Html.Utils;
using JetBrains.ReSharper.Psi.Impl;
using JetBrains.ReSharper.Psi.Impl.Resolve;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.ReSharper.Psi.Web.Util;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences.Razor
{
    internal static class NancyReferenceSymbolTableUtil
    {
        public static ISymbolTable GetReferenceSymbolTable2RenameThis(IPsiServices psiServices, [CanBeNull] string folder, [CanBeNull] IProject project)
        {
            throw new NotImplementedException("not yet");

            //if (project == null || String.IsNullOrEmpty(folder))
            //{
            //    return EmptySymbolTable.INSTANCE;
            //}

            //if (folder.IndexOfAny(FileSystemDefinition.InvalidPathChars) != -1 || folder == "???")
            //{
            //    return EmptySymbolTable.INSTANCE;
            //}

            //var mvcCache = psiServices.GetComponent<MvcCache>();
            //IEnumerable<IProject> searchableProjects = GetSearchableProjects(project);

            //ISymbolTable symbolTable = EmptySymbolTable.INSTANCE;

            //foreach (IProject prj in searchableProjects)
            //{
            //    ISymbolTable symbolTable2 = EmptySymbolTable.INSTANCE;
            //    string text;
            //    string text2 = Path.IsPathRooted(folder) ? ("~" + folder) : folder;
            //    text = HtmlPathReferenceUtil.ExpandRootName(text2.Replace('/', '\\'), prj);
            //    if (Path.IsPathRooted(text))
            //    {
            //        FileSystemPath fileSystemPath = FileSystemPath.Parse(text);
            //        if (!fileSystemPath.IsAbsolute)
            //        {
            //            fileSystemPath = WebPathReferenceUtil.GetRootPath(project).Combine(fileSystemPath);
            //        }
            //        symbolTable2 = symbolTable2.Merge(new DeclaredElementsSymbolTable<PathDeclaredElement>(psiServices,
            //            new[] { new PathDeclaredElement(psiServices, fileSystemPath) }, 0, null));
            //    }
            //    symbolTable = symbolTable.Merge(symbolTable2.Filter(new ISymbolFilter[]
            //    {
            //        new FileFilters.ItemInProjectFilter(prj)
            //    }));
            //}
            //return symbolTable.Distinct(PathInfoComparer.Instance);
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

            var mvcCache = psiServices.GetComponent<MvcCache>();
            IEnumerable<IProject> searchableProjects = GetSearchableProjects(project);

            ISymbolTable symbolTable = EmptySymbolTable.INSTANCE;

            foreach (IProject prj in searchableProjects)
            {
                ISymbolTable symbolTable2 = EmptySymbolTable.INSTANCE;
                string text;
                string text2 = Path.IsPathRooted(view) ? ("~" + view) : view;
                text = HtmlPathReferenceUtil.ExpandRootName(text2.Replace('/', '\\'), prj);
                if (Path.IsPathRooted(text))
                {
                    FileSystemPath fileSystemPath = FileSystemPath.Parse(text);
                    if (!fileSystemPath.IsAbsolute)
                    {
                        fileSystemPath = WebPathReferenceUtil.GetRootPath(project).Combine(fileSystemPath);
                    }
                    symbolTable2 = symbolTable2.Merge(new DeclaredElementsSymbolTable<PathDeclaredElement>(psiServices, 
                        new[] { new PathDeclaredElement(psiServices, fileSystemPath) }, 0, null));
                }
                foreach (string viewLocation in mvcCache.GetLocations(prj, MvcViewLocationType.View))
                {
                    foreach (var location in  ParseLocationFormatString(viewLocation))
                    {
                        FileSystemPath fileSystemPath2 = FileSystemPath.TryParse(location.First);
                        FileSystemPath location2 = (location.First.LastIndexOf('\\') == location.First.Length - 1)
                            ? fileSystemPath2
                            : fileSystemPath2.Directory;

                        var projectFolder = prj.FindProjectItemByLocation(location2) as IProjectFolder;
                        if (projectFolder != null)
                        {
                            Func<IProjectItem, bool> extensionFilter = item =>
                                item.Location.FullPath.EndsWith(location.Second, StringComparison.OrdinalIgnoreCase);

                            Func<IProjectItem, bool> preFilter = extensionFilter;
                            string text3 = Path.IsPathRooted(text) ? text : (location.First + text + location.Second);
                            var possibleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { text3 };
                            preFilter =
                                (item => extensionFilter(item) && possibleNames.Contains(item.Location.FullPath));
                            symbolTable2 = symbolTable2.Merge(PathReferenceUtil.GetSymbolTableByPath(
                                projectFolder.Location, psiServices, null, null, false, true,
                                projectItem => GetViewName(projectItem.Location,
                                    location), preFilter));
                        }
                    }
                }
                symbolTable = symbolTable.Merge(symbolTable2.Filter(new[]
                {
                    FileFilters.FileExists,
                    new FileFilters.ItemInProjectFilter(prj)
                }));
            }
            return symbolTable.Distinct(PathInfoComparer.Instance);
        }

        public static string GetViewName([NotNull] FileSystemPath path, Pair<string, string> location)
        {
            string text = path.FullPath;
            if (!location.First.IsEmpty())
            {
                text = text.TrimFromStart(location.First, StringComparison.OrdinalIgnoreCase);
            }
            if (!location.Second.IsNullOrEmpty())
            {
                text = text.TrimFromEnd(location.Second, StringComparison.OrdinalIgnoreCase);
            }
            return text.Replace('\\', '/');
        }

        public static IEnumerable<Pair<string, string>> ParseLocationFormatString(string locationFormat)
        {
            string text = String.Format(locationFormat, '', null, null);
            string[] array = text.Split(new[] { '' });
            Pair<string, string> pair = Pair.Of(array[0], (array.Length > 1) ? array[1] : null);
            yield return pair;
        }

        public static IEnumerable<IProject> GetSearchableProjects([CanBeNull] IProject project)
        {
            if (project == null)
            {
                return EmptyList<IProject>.InstanceList;
            }
            ISolution solution = project.GetSolution();
            IPsiModules psiModules = solution.PsiModules();
            return (
                from prj in solution.GetAllProjects().Where(psiModules.IsSourceProject)
                where (from _ in
                    (from _ in
                        psiModules.GetPsiModules(prj)
                                  .SelectMany(_ => psiModules.GetModuleReferences(_, project.GetResolveContext()))
                        select _.Module).OfType<IProjectPsiModule>()
                    select _.Project).Contains(project)
                select prj).Prepend(new[]
                {
                    project
                }).Distinct();
        }
    }
}