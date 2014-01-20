using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.Caches;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Feature.Services.CSharp.CodeCompletion.SmartCompletion;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.Html.Utils;
using JetBrains.ReSharper.Psi.Impl;
using JetBrains.ReSharper.Psi.Impl.Resolve;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.ReSharper.Psi.Web.Util;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public class NancyMvcViewReference : MvcViewReference<ICSharpLiteralExpression, IMethodDeclaration>,
        ISmartCompleatebleReference
    {
        private readonly MvcCache mvcCache;
        private readonly MvcKind mvcKind;
        private readonly ICollection<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>> names;
        private readonly IPsiServices psiServices;
        private readonly Version version;

        public NancyMvcViewReference([NotNull] IExpression owner,
            [NotNull] ICollection<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>> names,
            MvcKind mvcKind, Version version)
            : base(owner, names, mvcKind, version)
        {
            this.names = names;
            this.mvcKind = mvcKind;
            this.version = version;
            psiServices = owner.GetPsiServices();
            mvcCache = psiServices.GetComponent<MvcCache>();

            ResolveFilter = element =>
            {
                var pathDeclaredElement = element as IPathDeclaredElement;
                if (pathDeclaredElement == null || pathDeclaredElement.GetProjectItem() == null)
                {
                    return false;
                }

                if (pathDeclaredElement.Path.ExistsDirectory)
                {
                    return false;
                }
                return true;
            };
        }

        public override ISymbolTable GetReferenceSymbolTable(bool useReferenceName)
        {
            string name = GetName();
            IProject project = myOwner.GetProject();
            ISymbolTable symbolTable = EmptySymbolTable.INSTANCE;
            foreach (JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>> tuple in names)
            {
                ISymbolTable symbolTable2;
                if (tuple.C == MvcUtil.DeterminationKind.ImplicitByContainingMember)
                {
                    symbolTable2 = (from @class in tuple.D
                                    where @class != null && @class.IsValid()
                                    select GetReferenceSymbolTable(@class, useReferenceName ? name : null, mvcKind, version, tuple.A)).Merge();
                }
                else
                {
                    symbolTable2 = GetReferenceSymbolTable(psiServices, tuple.A, tuple.B, useReferenceName ? name : null, mvcKind, project, version);
                }
                if (useReferenceName && symbolTable2.GetAllSymbolInfos().IsEmpty() &&
                    name.IndexOfAny(FileSystemDefinition.InvalidPathChars) == -1)
                {
                    var symbolTable3 = new SymbolTable(psiServices);
                    try
                    {
                        bool hasExtension = Path.HasExtension(name);

                        foreach (string location in mvcCache.GetLocations(project, MvcUtil.GetViewLocationType(mvcKind, tuple.A)))
                        {
                            string path = string.Format(location, name, tuple.B, tuple.A);
                            if (hasExtension)
                            {
                                path = Path.ChangeExtension(path, null);
                            }
                            symbolTable3.AddSymbol(new PathDeclaredElement(psiServices, FileSystemPath.Parse(path)));
                        }
                    }
                    catch (InvalidPathException)
                    {
                    }
                    symbolTable2 = symbolTable3;
                }
                symbolTable = symbolTable.Merge(symbolTable2);
            }

            return symbolTable.Distinct();
        }

        private static ISymbolTable GetReferenceSymbolTable(IPsiServices psiServices, [CanBeNull] string area,
            [CanBeNull] string controller, [CanBeNull] string view, MvcKind mvcKind, [CanBeNull] IProject project, Version version)
        {
            if (project == null)
            {
                return EmptySymbolTable.INSTANCE;
            }
            ISolution solution = project.GetSolution();
            var component = solution.GetComponent<MvcCache>();
            IEnumerable<IProject> searcheableProjects = GetSearcheableProjects(project);
            bool hasExtension = false;
            if (view != null)
            {
                if (view.IndexOfAny(FileSystemDefinition.InvalidPathChars) != -1)
                {
                    return EmptySymbolTable.INSTANCE;
                }
                if (view == "???")
                {
                    return EmptySymbolTable.INSTANCE;
                }
                hasExtension = Path.HasExtension(view);
            }
            ISymbolTable symbolTable = EmptySymbolTable.INSTANCE;
            foreach (IProject current in searcheableProjects)
            {
                ISymbolTable symbolTable2 = EmptySymbolTable.INSTANCE;
                string text = null;
                if (view != null)
                {
                    string text2 = Path.IsPathRooted(view) ? ("~" + view) : view;
                    text = HtmlPathReferenceUtil.ExpandRootName(text2.Replace('/', '\\'), current);
                    if (Path.IsPathRooted(text))
                    {
                        FileSystemPath fileSystemPath = FileSystemPath.Parse(text);
                        if (!fileSystemPath.IsAbsolute)
                        {
                            fileSystemPath = WebPathReferenceUtil.GetRootPath(project).Combine(fileSystemPath);
                        }
                        symbolTable2 = symbolTable2.Merge(new DeclaredElementsSymbolTable<PathDeclaredElement>(psiServices, new[]
                            {
                                new PathDeclaredElement(psiServices, fileSystemPath)
                            }, 0, null));
                    }
                }
                List<string> list = null;
                if (hasExtension)
                {
                    list = component.GetDisplayModes(current).ToList();
                }
                string[] arg_152_0;
                if (!area.IsEmpty())
                {
                    var array = new string[2];
                    array[0] = area;
                    arg_152_0 = array;
                }
                else
                {
                    arg_152_0 = new[]
                    {
                        area
                    };
                }
                string[] array2 = arg_152_0;
                for (int i = 0; i < array2.Length; i++)
                {
                    string area2 = array2[i];
                    foreach (
                        string current2 in
                            component.GetLocations(current, MvcUtil.GetViewLocationType(mvcKind, area2), true))
                    {
                        using (
                            IEnumerator<Pair<string, string>> enumerator3 =
                                ParseLocationFormatString(current2, mvcKind, controller, area2).GetEnumerator())
                        {
                            while (enumerator3.MoveNext())
                            {
                                Pair<string, string> location = enumerator3.Current;
                                FileSystemPath fileSystemPath2 = FileSystemPath.TryParse(location.First);
                                FileSystemPath location2 = (location.First.LastIndexOf('\\') ==
                                                            location.First.Length - 1)
                                    ? fileSystemPath2
                                    : fileSystemPath2.Directory;
                                var projectFolder = current.FindProjectItemByLocation(location2) as IProjectFolder;
                                if (projectFolder != null)
                                {
                                    Func<IProjectItem, bool> extensionFilter =
                                        (IProjectItem item) =>
                                            item.Location.FullPath.EndsWith(location.Second,
                                                StringComparison.OrdinalIgnoreCase);
                                    Func<IProjectItem, bool> preFilter = extensionFilter;
                                    if (view != null)
                                    {
                                        string text3 = Path.IsPathRooted(text)
                                            ? text
                                            : (location.First + text + location.Second);
                                        string extension = Path.GetExtension(text3);
                                        var possibleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                        {
                                            text3
                                        };
                                        if (list != null)
                                        {
                                            foreach (string current3 in list)
                                            {
                                                possibleNames.Add(Path.ChangeExtension(text3, current3 + extension));
                                            }
                                        }
                                        preFilter = item => extensionFilter(item) && possibleNames.Contains(item.Location.FullPath);
                                    }
                                    symbolTable2 = symbolTable2.Merge(PathReferenceUtil.GetSymbolTableByPath(
                                            projectFolder.Location, psiServices, null, null, false, true,
                                            projectItem => GetViewName(projectItem.Location, location), preFilter));
                                }
                            }
                        }
                    }
                }
                symbolTable = symbolTable.Merge(symbolTable2.Filter(new[]
                {
                    FileFilters.FileExists,
                    new FileFilters.ItemInProjectFilter(current)
                }));
            }
            return symbolTable.Distinct(PathInfoComparer.Instance);
        }

        private static string GetViewName([NotNull] FileSystemPath path, Pair<string, string> location)
        {
            string text = path.FullPath;
            if (!location.First.IsEmpty())
            {
                text = text.TrimFromStart(location.First, StringComparison.OrdinalIgnoreCase);
            }
            if (!location.Second.IsNullOrEmpty<char>())
            {
                text = text.TrimFromEnd(location.Second, StringComparison.OrdinalIgnoreCase);
            }
            return text.Replace('\\', '/');
        }

        private static IEnumerable<IProject> GetSearcheableProjects([CanBeNull] IProject project)
        {
            if (project == null)
            {
                return EmptyList<IProject>.InstanceList;
            }
            ISolution solution = project.GetSolution();
            IPsiModules psiModules = solution.PsiModules();
            return (
                from prj in solution.GetAllProjects().Where(new Func<IProject, bool>(psiModules.IsSourceProject))
                where (
                    from _ in
                        (
                            from _ in psiModules.GetPsiModules(prj).SelectMany((IPsiModule _) => psiModules.GetModuleReferences(_, project.GetResolveContext()))
                            select _.Module).OfType<IProjectPsiModule>()
                    select _.Project).Contains(project)
                select prj).Prepend(new IProject[]
	{
		project
	}).Distinct<IProject>();
        }



        private static IEnumerable<Pair<string, string>> ParseLocationFormatString(string locationFormat, MvcKind mvcKind, string controller, string area)
        {
            string text = string.Format(locationFormat, '', controller, area);
            string[] array = text.Split(new char[]
	{
		''
	});
            Pair<string, string> pair = Pair.Of<string, string>(array[0], (array.Length > 1) ? array[1] : null);
            switch (mvcKind)
            {
                case MvcKind.DisplayTemplate:
                    yield return Pair.Of<string, string>(pair.First + "DisplayTemplates\\", pair.Second);
                    break;
                case MvcKind.EditorTemplate:
                    yield return Pair.Of<string, string>(pair.First + "EditorTemplates\\", pair.Second);
                    break;
                case MvcKind.Template:
                    yield return Pair.Of<string, string>(pair.First + "DisplayTemplates\\", pair.Second);
                    yield return Pair.Of<string, string>(pair.First + "EditorTemplates\\", pair.Second);
                    break;
                default:
                    yield return pair;
                    break;
            }
            yield break;
        }
    }

    internal class PathInfoComparer : IEqualityComparer<ISymbolInfo>
    {
        public static readonly IEqualityComparer<ISymbolInfo> Instance = new PathInfoComparer();

        public bool Equals(ISymbolInfo info1, ISymbolInfo info2)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(info1.ShortName, info2.ShortName) &&
                Equals(GetProject(info1), GetProject(info2)))
            {
                return true;
            }
            IPathDeclaredElement pathDeclaredElement = GetPathDeclaredElement(info1);
            IPathDeclaredElement pathDeclaredElement2 = GetPathDeclaredElement(info2);
            return pathDeclaredElement != null && pathDeclaredElement2 != null &&
                   pathDeclaredElement.Path == pathDeclaredElement2.Path;
        }

        public int GetHashCode(ISymbolInfo info)
        {
            IPathDeclaredElement pathDeclaredElement = GetPathDeclaredElement(info);
            string text = (pathDeclaredElement != null)
                ? pathDeclaredElement.Path.NameWithoutExtension
                : info.ShortName;
            IProject project = GetProject(info);
            return text.ToUpperInvariant().GetHashCode() * 397 ^ ((project != null) ? project.GetHashCode() : 0);
        }

        [CanBeNull]
        private static IPathDeclaredElement GetPathDeclaredElement([NotNull] ISymbolInfo info)
        {
            return info.GetDeclaredElement() as IPathDeclaredElement;
        }

        [CanBeNull]
        private static IProject GetProject([NotNull] ISymbolInfo info)
        {
            IPathDeclaredElement pathDeclaredElement = GetPathDeclaredElement(info);
            if (pathDeclaredElement == null)
            {
                return null;
            }
            IProjectItem projectItem = pathDeclaredElement.GetProjectItem();
            if (projectItem == null)
            {
                return null;
            }
            return projectItem.GetProject();
        }
    }
}