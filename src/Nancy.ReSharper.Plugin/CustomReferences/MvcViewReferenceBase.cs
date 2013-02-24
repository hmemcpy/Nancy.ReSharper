using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.Caches;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CodeAnnotations;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.Html.Utils;
using JetBrains.ReSharper.Psi.Impl;
using JetBrains.ReSharper.Psi.Impl.Resolve;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.ReSharper.Psi.Web.Util;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    [Description("View")]
    public class MvcViewReferenceBase<TLiteral, TMethod> : MvcReference<TLiteral>, IMvcViewReference
        where TLiteral : ILiteralExpression
        where TMethod : class, ITypeOwnerDeclaration, ITypeMemberDeclaration
    {
        private const char ViewNameStub = '?';
        private static readonly string ourSupressViewErrorAttributeName = typeof(AspMvcSupressViewErrorAttribute).Name;
        private readonly MvcCache myCache;
        private readonly MvcKind myMvcKind;
        private readonly ICollection<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>> myNames;
        private readonly IPsiServices myPsiServices;
        private readonly Version myVersion;

        public MvcViewReferenceBase([NotNull] IExpression owner,
                                    ICollection
                                        <JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>> names,
                                    MvcKind mvcKind, Version version)
            : base(owner)
        {
            myNames = names;
            myMvcKind = mvcKind;
            myVersion = version;
            ResolveFilter = delegate(IDeclaredElement element)
            {
                var pathDeclaredElement = element as IPathDeclaredElement;
                return pathDeclaredElement != null && pathDeclaredElement.GetProjectItem() != null;
            };
            myPsiServices = myOwner.GetPsiServices();
            myCache = myPsiServices.Solution.GetComponent<MvcCache>();
        }

        public override MvcKind MvcKind
        {
            get { return myMvcKind; }
        }

        public FileSystemPath GetControllerFolder()
        {
            return (from _ in myNames select NancyUtil.GetControllerFolder(myOwner.GetProject(), _.A, _.B))
                .DefaultIfEmpty(FileSystemPath.Empty).First();
        }

        public override ISymbolTable GetReferenceSymbolTable(bool useReferenceName)
        {
            string name = GetName();
            IProject project = myOwner.GetProject();
            ISymbolTable symbolTable = EmptySymbolTable.INSTANCE;
            using (
                IEnumerator<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>> enumerator =
                    myNames.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>> tuple = enumerator.Current;
                    ISymbolTable symbolTable2;
                    if (tuple.C == MvcUtil.DeterminationKind.ImplicitByContainingMember)
                    {
                        symbolTable2 = (
                                           from @class in tuple.D
                                           where @class != null && @class.IsValid()
                                           select
                                               GetReferenceSymbolTable(@class, useReferenceName ? name : null, myMvcKind,
                                                                       myVersion, tuple.A)).Merge(null);
                    }
                    else
                    {
                        symbolTable2 = GetReferenceSymbolTable(myPsiServices, tuple.A, tuple.B,
                                                               useReferenceName ? name : null, myMvcKind, project,
                                                               myVersion);
                    }
                    if (useReferenceName && symbolTable2.GetAllSymbolInfos().IsEmpty() &&
                        name.IndexOfAny(FileSystemDefinition.InvalidPathChars) == -1)
                    {
                        var symbolTable3 = new SymbolTable(myPsiServices, null);
                        try
                        {
                            foreach (string current in
                                from locationFormat in
                                    myCache.GetLocations(project, NancyUtil.GetViewLocationType(myMvcKind, tuple.A))
                                select string.Format(locationFormat, name, tuple.B, tuple.A))
                            {
                                // todo temp: this is to prevent empty string in view name
                                var fileSystemPath = new FileSystemPath(current);
                                if (fileSystemPath.ExistsDirectory)
                                {
                                    continue;
                                }
                                symbolTable3.AddSymbol(new PathDeclaredElement(myPsiServices, fileSystemPath));
                            }
                        }
                        catch (InvalidPathException)
                        {
                        }
                        symbolTable2 = symbolTable3;
                    }
                    symbolTable = symbolTable.Merge(symbolTable2);
                }
            }
            return symbolTable;
        }

        public FileSystemPath GetBasePath()
        {
            return GetControllerFolder();
        }

        public ISymbolFilter[] GetPathFilters()
        {
            IProject project = myOwner.GetProject();
            return GetPathFilters(project, myNames.SelectMany(tuple =>
                                                              from _ in
                                                                  myCache.GetLocations(project,
                                                                                       NancyUtil.GetViewLocationType(
                                                                                           myMvcKind, tuple.A), true)
                                                              select Path.GetExtension(_)));
        }

        public override IReference BindTo(IDeclaredElement element)
        {
            IExpression expression = base.InternalBindTo(element);
            if (!myOwner.Equals(expression))
            {
                return expression.FindReference<MvcViewReferenceBase<TLiteral, TMethod>>() ?? this;
            }
            return this;
        }

        /// <see cref="!:locationFormat" />
        /// must be normalized (windows path format)
        private static Pair<string, string> ParseLocationFormatString(string locationFormat, MvcKind mvcKind,
                                                                      string controller, string area)
        {
            string text = (mvcKind == MvcKind.DisplayTemplate)
                              ? "DisplayTemplates"
                              : ((mvcKind == MvcKind.EditorTemplate) ? "EditorTemplates" : null);
            string text2 = string.Format(locationFormat, (text == null) ? (object)'?' : (text + '\\' + '?'), controller,
                                         area);
            string[] array = text2.Split(new[]
            {
                '?'
            });
            return Pair.Of(array[0], (array.Length > 1) ? array[1] : null);
        }

        /// <summary>
        ///     Get views symbol table
        /// </summary>
        /// <param name="psiServices" />
        /// <param name="area">Area name</param>
        /// <param name="controller">Controller name</param>
        /// <param name="view">View name. Can be just name (View), relative path (Views/Bla/View.aspx) and rooted path (~/Views/Bla/View.aspx)</param>
        /// <param name="mvcKind">Type of target - masterpage, view or template</param>
        /// <param name="project">Project</param>
        /// <param name="version">MVC version</param>
        /// <returns>Symbol table</returns>
        private static ISymbolTable GetReferenceSymbolTable(IPsiServices psiServices, [CanBeNull] string area,
                                                            [CanBeNull] string controller, [CanBeNull] string view,
                                                            MvcKind mvcKind, [CanBeNull] IProject project,
                                                            Version version)
        {
            if (project == null)
            {
                return EmptySymbolTable.INSTANCE;
            }
            ISolution solution = project.GetSolution();
            var component = solution.GetComponent<MvcCache>();
            IEnumerable<IProject> searcheableProjects = GetSearcheableProjects(project);
            bool flag = false;
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
                flag = !Path.HasExtension(view);
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
                        var fileSystemPath = new FileSystemPath(text);
                        if (!fileSystemPath.IsAbsolute)
                        {
                            fileSystemPath = WebPathReferenceUtil.GetRootPath(project).Combine(fileSystemPath);
                        }
                        symbolTable2 =
                            symbolTable2.Merge(new DeclaredElementsSymbolTable<PathDeclaredElement>(psiServices, new[]
                            {
                                new PathDeclaredElement(psiServices, fileSystemPath)
                            }, 0, null));
                    }
                }
                List<string> list = null;
                if (flag && version.Major >= 4)
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
                            component.GetLocations(current, NancyUtil.GetViewLocationType(mvcKind, area2), true))
                    {
                        Pair<string, string> location = ParseLocationFormatString(current2, mvcKind, controller, area2);
                        FileSystemPath fileSystemPath2 = FileSystemPath.TryParse(location.First);
                        FileSystemPath location2 = (location.First.LastIndexOf('\\') == location.First.Length - 1)
                                                       ? fileSystemPath2
                                                       : fileSystemPath2.Directory;
                        var projectFolder = current.FindProjectItemByLocation(location2) as IProjectFolder;
                        if (projectFolder != null)
                        {
                            Func<IProjectItem, bool> extensionFilter =
                                (IProjectItem item) =>
                                item.Location.FullPath.EndsWith(location.Second, StringComparison.OrdinalIgnoreCase);
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
                                preFilter =
                                    ((IProjectItem item) =>
                                     extensionFilter(item) && possibleNames.Contains(item.Location.FullPath));
                            }
                            symbolTable2 =
                                symbolTable2.Merge(PathReferenceUtil.GetSymbolTableByPath(projectFolder.Location,
                                                                                          psiServices, null, null, false,
                                                                                          true,
                                                                                          (IProjectItem projectItem) =>
                                                                                          GetViewName(
                                                                                              projectItem.Location,
                                                                                              location), preFilter));
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

        private static IEnumerable<IProject> GetSearcheableProjects([CanBeNull] IProject project)
        {
            if (project == null)
            {
                return EmptyList<IProject>.InstanceList;
            }
            ISolution solution = project.GetSolution();
            PsiModuleManager psiModuleManager = PsiModuleManager.GetInstance(solution);
            return (
                       from prj in solution.GetAllProjects().Where(psiModuleManager.IsSourceProject)
                       where (
                                 from _ in
                                     (
                                         from _ in
                                             psiModuleManager.GetPsiModules(prj)
                                                             .SelectMany(
                                                                 (IPsiModule _) =>
                                                                 psiModuleManager.GetModuleReferences(_))
                                         select _.Module).OfType<IProjectPsiModule>()
                                 select _.Project).Contains(project)
                       select prj).Prepend(new[]
                       {
                           project
                       }).ToList();
        }

        [NotNull]
        public static ISymbolTable GetReferenceSymbolTable([NotNull] IClass @class, [CanBeNull] string name,
                                                           MvcKind mvcKind, Version version, string area = null)
        {
            IPsiServices psiServices = @class.GetPsiServices();
            IFinder finder = psiServices.Finder;
            ISearchDomain searchDomain = SearchDomainFactory.Instance.CreateSearchDomain(@class.GetSolution(), false);
            ISymbolTable symbolTable = EmptySymbolTable.INSTANCE;
            foreach (IClass current in FindInheritedClassesIfNecessary(@class, finder, searchDomain))
            {
                IProject project =
                    current.GetSourceFiles()
                           .SelectNotNull((IPsiSourceFile sourceFile) => sourceFile.GetProject())
                           .FirstOrDefault();
                if (project != null)
                {
                    string controllerArea = NancyUtil.GetControllerArea(current);
                    if (area.IsNullOrEmpty() || string.Equals(area, controllerArea, StringComparison.OrdinalIgnoreCase))
                    {
                        string controllerName = NancyUtil.GetControllerName(current);
                        symbolTable =
                            symbolTable.Merge(GetReferenceSymbolTable(psiServices, controllerArea, controllerName, name,
                                                                      mvcKind, project, version));
                    }
                }
            }
            return symbolTable;
        }

        [NotNull]
        private static IEnumerable<IClass> FindInheritedClassesIfNecessary([NotNull] IClass baseClass,
                                                                           [NotNull] IFinder finder,
                                                                           [NotNull] ISearchDomain searchDomain)
        {
            var list = new List<IClass>(1)
            {
                baseClass
            };
            if (baseClass.IsAbstract)
            {
                finder.FindInheritors(baseClass, searchDomain, list.ConsumeDeclaredElements(),
                                      NullProgressIndicator.Instance);
            }
            return list;
        }

        private static ISymbolFilter[] GetPathFilters(IProject project, IEnumerable<string> expectedExtensions)
        {
            return new ISymbolFilter[]
            {
                new FileFilters.ItemInProjectFilter(project),
                new FileFilters.ExtensionFilter(expectedExtensions)
            };
        }

        protected override string PrepareName(ISymbolInfo symbol)
        {
            var pathDeclaredElement = symbol.GetDeclaredElement() as IPathDeclaredElement;
            FileSystemPath path = pathDeclaredElement.Path;
            IProject project = myOwner.GetProject();
            foreach (var current in myNames)
            {
                string[] arg_70_0;
                if (!current.A.IsEmpty())
                {
                    var array = new string[2];
                    array[0] = current.A;
                    arg_70_0 = array;
                }
                else
                {
                    arg_70_0 = new[]
                    {
                        current.A
                    };
                }
                string[] array2 = arg_70_0;
                for (int i = 0; i < array2.Length; i++)
                {
                    string area = array2[i];
                    foreach (
                        string current2 in
                            myCache.GetLocations(project, NancyUtil.GetViewLocationType(myMvcKind, area), true))
                    {
                        Pair<string, string> location = ParseLocationFormatString(current2, myMvcKind, current.B, area);
                        if (path.FullPath.StartsWith(location.First, StringComparison.OrdinalIgnoreCase))
                        {
                            return GetViewName(path, location);
                        }
                    }
                }
            }
            return GetViewName(path, Pair.Of<string, string>(GetBasePath().FullPath, null));
        }

        [CanBeNull]
        public static IMethod GetAction([NotNull] ITreeNode element)
        {
            var containingNode = element.GetContainingNode<TMethod>(false);
            if (containingNode == null)
            {
                return null;
            }
            return (IMethod)containingNode.DeclaredElement;
        }

        public static IResolveInfo CheckViewResolveResult([NotNull] IResolveInfo result, [NotNull] IReference reference)
        {
            if (result.Equals(MvcResolveErrorType.MVC_TEMPLATE_NOT_RESOLVED))
            {
                return ResolveErrorType.IGNORABLE;
            }
            if (result.Equals(MvcResolveErrorType.MVC_MASTERPAGE_NOT_RESOLVED) &&
                string.IsNullOrEmpty(reference.GetName()))
            {
                return ResolveErrorType.IGNORABLE;
            }
            if (result.Equals(MvcResolveErrorType.MVC_VIEW_NOT_RESOLVED) ||
                result.Equals(MvcResolveErrorType.MVC_PARTIAL_VIEW_NOT_RESOLVED) ||
                result.Equals(MvcResolveErrorType.MVC_MASTERPAGE_NOT_RESOLVED))
            {
                ITreeNode treeNode = reference.GetTreeNode();
                IMethod action = GetAction(treeNode);
                if (action == null)
                {
                    return result;
                }
                var @class = action.GetContainingType() as IClass;
                if (@class == null)
                {
                    return result;
                }
                if (@class.IsAbstract)
                {
                    return ResolveErrorType.IGNORABLE;
                }
                CodeAnnotationsCache codeAnnotations = treeNode.GetPsiServices().GetCodeAnnotationsCache();
                List<IAttributeInstance> list =
                    action.GetAttributeInstances(true).Concat(@class.GetAttributeInstances(true)).ToList();
                if (
                    list.Concat(
                        list.SelectNotNull((IAttributeInstance _) => _.AttributeType.GetTypeElement())
                            .SelectMany((ITypeElement _) => _.GetAttributeInstances(true)))
                        .Any(
                            (IAttributeInstance _) =>
                            codeAnnotations.IsAnnotationAttribute(_, ourSupressViewErrorAttributeName)))
                {
                    return ResolveErrorType.IGNORABLE;
                }
            }
            return result;
        }

        public override ResolveResultWithInfo ResolveWithoutCache()
        {
            ResolveResultWithInfo resolveResultWithInfo = base.ResolveWithoutCache();

            return new ResolveResultWithInfo(resolveResultWithInfo.Result, GetResolveInfo(resolveResultWithInfo.Info));
        }

        private IResolveInfo GetResolveInfo(IResolveInfo resolveInfo)
        {
            return CheckViewResolveResult(NancyUtil.CheckMvcResolveResult(resolveInfo, MvcKind), this);
        }

        private static string GetViewName([NotNull] FileSystemPath path, Pair<string, string> location)
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

        private class PathInfoComparer : IEqualityComparer<ISymbolInfo>
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
}