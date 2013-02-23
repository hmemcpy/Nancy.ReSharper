using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using JetBrains.Annotations;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.Caches;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Feature.Services.Intentions.Impl.LanguageSpecific;
using JetBrains.ReSharper.Feature.Services.Util;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CodeAnnotations;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Services;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Web.Tree;
using JetBrains.Util;
using JetBrains.Util.Special;
using JetBrains.ReSharper.Psi.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public static class NancyUtil
    {
        public const string ModuleClassSuffix = "Controller";
        public const string ModulesFolder = "Controllers";
        public const string AreasFolder = "Areas";

        public static OneToListMap<string, IClass> GetAvailableModules([NotNull] IPsiModule module, [CanBeNull] ICollection<string> areas = null, bool includingIntermediateControllers = false, ITypeElement baseClass = null)
        {
            // [TODO] [derigel] Currently, ignore areas - search in whole project. In future should take into accout namespace restriction in routing.

            // include all types from itself, references and referenced by.
            var searchDomain = SearchDomainFactory.Instance.CreateSearchDomain(
              module.GetPsiServices().ModuleManager
                .GetAllModules().Where(m => m.References(module) || module.References(m))
                .Prepend(module));

            return GetAvailableModules(module, searchDomain, includingIntermediateControllers, baseClass);
        }

        /// <summary> Get all available controlles in module</summary>
        /// <remarks>TODO: [derigel] Caching...</remarks>
        /// <param name="module"></param>
        /// <param name="searchDomain"></param>
        /// <param name="includingIntermediateControllers">Including abstract controllers, not ending 'Controller' suffix and etc</param>
        /// <param name="baseClass">Base class for controllers to check, by default <see cref="System.Web.Mvc.IController"/> or <see cref="WebRequestMethods.Http.Controllers.IHttpController" /> </param>
        /// <returns></returns>
        public static OneToListMap<string, IClass> GetAvailableModules([NotNull] IPsiModule module, [NotNull] ISearchDomain searchDomain, bool includingIntermediateControllers = false, ITypeElement baseClass = null)
        {
            ITypeElement[] typeElements;

            var mvcElementsCache = MvcElementsCache.GetInstance(module);
            if (baseClass != null)
            {
                if (baseClass.IsDescendantOf(mvcElementsCache.MvcControllerInterface) || baseClass.IsDescendantOf(mvcElementsCache.MvcHttpControllerInterface))
                    typeElements = new[] { baseClass };
                else
                    return new OneToListMap<string, IClass>(0);
            }
            else
            {
                typeElements = new[] { mvcElementsCache.MvcControllerInterface, mvcElementsCache.MvcHttpControllerInterface };
            }

            var found = new List<IClass>();
            foreach (var typeElement in typeElements)
            {
                if (typeElement == null)
                    continue;
                if (typeElement is IClass)
                    found.Add((IClass)typeElement);
                module.GetPsiServices().Finder.FindInheritors(typeElement, searchDomain, found.ConsumeDeclaredElements(), NullProgressIndicator.Instance);
            }

            var classes = found.Where(@class => @class.GetAccessRights() == AccessRights.PUBLIC);
            if (!includingIntermediateControllers)
                classes = classes
                  .Where(@class => !@class.IsAbstract && @class.ShortName.EndsWith(ModuleClassSuffix, StringComparison.OrdinalIgnoreCase));

            return new OneToListMap<string, IClass>(
              classes.GroupBy(GetControllerName, (name, enumerable) => new KeyValuePair<string, IList<IClass>>(name, enumerable.ToList())),
              StringComparer.OrdinalIgnoreCase);
        }

        private static readonly ParameterKind[] ourNotAllowedParameterKinds = new[]
    {
      ParameterKind.OUTPUT, ParameterKind.REFERENCE
    };

        [NotNull]
        public static IEnumerable<Pair<string, IMethod>> GetControllerActions([CanBeNull] IClass @class, [NotNull] IPsiModule module)
        {
            if (!module.IsValid())
                return EmptyList<Pair<string, IMethod>>.InstanceList;

            ITypeElement baseType = null;
            if (@class != null)
            {
                var mvcElementsCache = MvcElementsCache.GetInstance(module);
                if (@class.IsDescendantOf(mvcElementsCache.MvcControllerInterface))
                    baseType = mvcElementsCache.MvcControllerClass;
                else if (@class.IsDescendantOf(mvcElementsCache.MvcHttpControllerInterface))
                    baseType = mvcElementsCache.MvcApiControllerClass;
            }

            return GetControllerActions(@class, baseType ?? module.GetPredefinedType().Object.GetTypeElement());
        }

        [NotNull]
        private static IEnumerable<Pair<string, IMethod>> GetControllerActions([CanBeNull] IClass @class, [CanBeNull] ITypeElement @baseController)
        {
            // stop hierarchy traversing on base controller type
            if ((@class == null) || Equals(@class, baseController) || !@class.IsValid())
                return EmptyList<Pair<string, IMethod>>.InstanceList;

            return @class.Methods
              .Where(method => !method.IsStatic && !method.IsExtensionMethod && method.GetAccessRights() == AccessRights.PUBLIC)
              .Where(method => method.TypeParameters.IsEmpty())
              .Where(method => !method.Parameters.Any(parameter => ourNotAllowedParameterKinds.Contains(parameter.Kind)))
              .Select(method => Pair.Of(GetActionName(method), method))
              .Union(GetControllerActions(@class.GetSuperClass(), baseController));
        }

        private static readonly IDictionary<string, MvcKind> ourMvcKinds = new Dictionary<string, MvcKind>
    {
      { typeof(AspMvcAreaAttribute).Name, MvcKind.Area },
      { typeof(AspMvcControllerAttribute).Name, MvcKind.Controller },
      { typeof(AspMvcActionAttribute).Name, MvcKind.Action },
      { typeof(AspMvcViewAttribute).Name, MvcKind.View },
      { typeof(AspMvcPartialViewAttribute).Name, MvcKind.PartialView },
      { typeof(AspMvcMasterAttribute).Name, MvcKind.Master },
      { typeof(AspMvcDisplayTemplateAttribute).Name, MvcKind.DisplayTemplate },
      { typeof(AspMvcEditorTemplateAttribute).Name, MvcKind.EditorTemplate },
      { typeof(PathReferenceAttribute).Name, MvcKind.PathReference },
      { typeof(AspMvcModelTypeAttribute).Name, MvcKind.ModelType },
    };

        private static string AnonymousPropertyInitializerRetriever(IAttributeInstance attr)
        {
            return attr
              .PositionParameters()
              .Select(value => value.ConstantValue)
              .Where(value => value.IsString()).SelectNotNull(value => value.Value as string)
              .FirstOrDefault();
        }

        private static readonly IDictionary<MvcKind, Func<IAttributeInstance, string>> ourMvcKindAnonymousPropertyInitializers =
          new Dictionary<MvcKind, Func<IAttributeInstance, string>>
      {
        { MvcKind.Area, AnonymousPropertyInitializerRetriever },
        { MvcKind.Controller, AnonymousPropertyInitializerRetriever },
        { MvcKind.Action, AnonymousPropertyInitializerRetriever }
      };

        public static MvcKind GetMvcKind([NotNull] Type attributeType)
        {
            return ourMvcKinds.TryGetValue(attributeType.Name, MvcKind.None);
        }

        public static ICollection<JetTuple<MvcKind, string, IAttributeInstance>> GetMvcKinds([NotNull] this IAttributesOwner element)
        {
            var codeAnnotations = element.GetPsiServices().GetCodeAnnotationsCache();
            return element
              .GetAttributeInstances(false)
              .SelectMany(attr =>
                  ourMvcKinds.Where(pair => codeAnnotations.IsAnnotationAttribute(attr, pair.Key))
                    .Select(pair => JetTuple.Of
                    (
                      pair.Value,
                      ourMvcKindAnonymousPropertyInitializers.ContainsKey(pair.Value) ?
                        ourMvcKindAnonymousPropertyInitializers[pair.Value](attr) : null,
                      attr
                    )))
                  .ToList();
        }

        public static MvcKind GetReferenceKind(this IMvcActionControllerReference reference)
        {
            if (reference is IMvcControllerReference)
                return MvcKind.Controller;

            if (reference is IMvcActionReference)
                return MvcKind.Action;

            return MvcKind.None;
        }

        public const string AsyncActionSuffixInit = "Async";
        public const string AsyncActionSuffixCompleted = "Completed";
        public static readonly string[] AsyncActionSuffixes = new[] { AsyncActionSuffixInit, AsyncActionSuffixCompleted };

        public static string GetActionName([NotNull] IMethod method)
        {
            string alternativeActionName = null;
            foreach (var attr in method.GetAttributeInstances(false))
            {
                var typeElement = attr.AttributeType.GetTypeElement();
                if (typeElement == null)
                    continue;

                var codeAnnotations = method.GetPsiServices().GetCodeAnnotationsCache();
                Func<IAttributesSet, bool> validator =
                  attrSet => attrSet.GetAttributeInstances(true).Any(_ => codeAnnotations.IsAnnotationAttribute(_, ourAspMvcActionNameSelectorAttribute));

                AttributeValue attrValue = null;
                var constructor = attr.Constructor;
                if (constructor != null)
                {
                    for (int i = 0; i < constructor.Parameters.Count; i++)
                    {
                        var parameter = constructor.Parameters[i];
                        if (validator(parameter))
                        {
                            attrValue = attr.PositionParameter(i);
                            break;
                        }
                    }
                }

                if (attrValue == null)
                {
                    foreach (var namedParameter in attr.NamedParameters())
                    {
                        foreach (var property in typeElement.GetAllClassMembers<IProperty>(namedParameter.First))
                        {
                            if (validator(property.Element))
                            {
                                attrValue = namedParameter.Second;
                                break;
                            }
                        }
                        if (attrValue != null)
                            break;
                    }
                }

                if ((attrValue != null) && attrValue.IsConstant && attrValue.ConstantValue.IsString())
                {
                    alternativeActionName = attrValue.ConstantValue.Value as string;
                    break;
                }
            }

            if (alternativeActionName != null) return alternativeActionName;

            var actionName = method.ShortName;

            var containingType = method.GetContainingType();
            if (containingType != null &&
                containingType.IsDescendantOf(MvcElementsCache.GetInstance(method.Module).MvcAsyncControllerClass))
            {
                return AsyncActionSuffixes
                  .Where(suffix => actionName.EndsWith(suffix, StringComparison.Ordinal))
                  .Select(suffix => actionName.Substring(0, actionName.Length - suffix.Length))
                  .FirstOrDefault() ?? actionName;
            }

            return actionName;
        }

        [CanBeNull]
        public static string GetControllerArea([CanBeNull] ITypeElement controller)
        {
            if (controller == null)
                return null;
            return
              (
                from sourceFile in controller.GetSourceFiles()
                let project = sourceFile.GetProject()
                where project != null
                let location = sourceFile.GetLocation()
                let areasFolder = project.Location.Combine(AreasFolder)
                where areasFolder.IsPrefixOf(location)
                select location.ConvertToRelativePath(areasFolder).GetPathComponents().FirstOrDefault()
            )
            .WhereNotNull()
            .FirstOrDefault();
        }

        [CanBeNull]
        public static string GetControllerName([CanBeNull] ITypeElement controller)
        {
            if (controller == null)
                return null;
            return GetControllerName(controller.ShortName);
        }

        public static string GetControllerName([NotNull] string controllerName)
        {
            return controllerName.TrimFromEnd(ModuleClassSuffix);
        }

        private static readonly string ourAspMvcActionNameSelectorAttribute = typeof(AspMvcActionSelectorAttribute).Name;

        public static FileSystemPath GetControllerFolder(IProject project, IClass @class)
        {
            return GetControllerFolder(project, GetControllerArea(@class), GetControllerName(@class));
        }

        [CanBeNull]
        public static IProjectFolder GetAreasFolder([CanBeNull] IProject project)
        {
            if (project == null)
                return null;
            return project.GetSubItem(AreasFolder) as IProjectFolder;
        }

        [NotNull]
        public static FileSystemPath GetAreaFolder([CanBeNull] IProjectItem projectItem)
        {
            if (projectItem == null)
                return FileSystemPath.Empty;
            var areasFolder = GetAreasFolder(projectItem.GetProject());
            if (areasFolder == null)
                return FileSystemPath.Empty;
            return areasFolder.GetSubItems()
              .OfType<IProjectFolder>()
              .Select(_ => _.Location)
              .Where(area => area.IsPrefixOf(projectItem.Location))
              .DefaultIfEmpty(areasFolder.Location)
              .First();
        }

        [NotNull]
        public static FileSystemPath GetAreaFolder([CanBeNull] IProject project, [CanBeNull] string area)
        {
            if (project == null)
                return FileSystemPath.Empty;
            var basePath = project.Location;
            // Empty area ("") is the same as no area (null)
            if (!area.IsEmpty())
            {
                basePath = basePath.Combine(AreasFolder).Combine(area);
            }
            return basePath;
        }

        [NotNull]
        public static FileSystemPath GetControllerFolder([CanBeNull] IProject project, [CanBeNull] string area, [CanBeNull] string controllerName, MvcKind mvcKind = MvcKind.View)
        {
            if ((project == null) || (controllerName == null))
                return FileSystemPath.Empty;

            var mvcCache = project.GetSolution().GetComponent<MvcCache>();
            try
            {
                foreach (var locationFormat in mvcCache.GetLocations(project, GetViewLocationType(mvcKind, area)))
                {
                    var path = string.Format(locationFormat, null, controllerName, area);
                    return new FileSystemPath(path).Directory;
                }
            }
            catch (InvalidPathException) { }

            return FileSystemPath.Empty;
        }

        private static readonly Key<CachedPsiValue<ICollection<JetTuple<string, string, JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind, ICollection<IClass>>>>> ourCachedControllersKey =
          new Key<CachedPsiValue<ICollection<JetTuple<string, string, JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind, ICollection<IClass>>>>>("CachedControllersKey");

        /// <summary>
        /// Get controller names and their resolved types (explicit or implicit) for specified method invocation
        /// </summary>
        /// <param name="argumentsOwner"></param>
        /// <returns>Collection of tuples of area, controller name, controller's determination kind, and controllers' classes corresponded to name</returns>
        /// <remarks>Controller name can be null, indicating this way that controller's name is calculated at runtime</remarks>
        // TODO: [derigel] caching
        public static IEnumerable<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>> GetModules([CanBeNull] IArgumentsOwner argumentsOwner)
        {
            if ((argumentsOwner == null) || !argumentsOwner.IsValid())
                return EmptyList<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>>.InstanceList;
            return argumentsOwner.UserData
              .GetOrCreateData(ourCachedControllersKey, () => argumentsOwner.CreateCachedValue(GetControllersNonCached(argumentsOwner)))
              .GetValue(argumentsOwner, () => GetControllersNonCached(argumentsOwner));
        }

        private static ICollection<JetTuple<string, string, JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind, ICollection<IClass>>> GetControllersNonCached([NotNull] IArgumentsOwner argumentsOwner)
        {
            argumentsOwner.AssertIsValid("argumentsOwner is invalid");
            var psiModule = argumentsOwner.GetPsiModule();

            var project = argumentsOwner.GetProject();
            Assertion.AssertNotNull(project, "project == null");
            var projectFile = argumentsOwner.GetSourceFile().ToProjectFile();
            Assertion.AssertNotNull(projectFile, "projectFile == null");
            var mvcCache = project.GetSolution().GetComponent<MvcCache>();

            var implicitArea = GetAreaFolder(projectFile);
            var areaNames = ProcessArgumentsExpression(argumentsOwner, MvcKind.Area)
              .DefaultIfEmpty(AreasFolder.Equals(implicitArea.Name, StringComparison.OrdinalIgnoreCase) ? null : implicitArea.Name);
            var controllerNames = ProcessArgumentsExpression(argumentsOwner, MvcKind.Controller);

            ICollection<JetTuple<string, string, JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind, bool>> names;

            if (controllerNames.IsEmpty())
            {
                // first, try detect implicit controller type by view
                // TODO: [derigel] better euristics for view detection
                if (projectFile.LanguageType.Is<HtmlProjectFileType>())
                {
                    names =
                    (
                      from area in areaNames
                      from kind in new[] { MvcKind.View, MvcKind.PartialView }
                      from locationFormat in mvcCache.GetLocations(project, GetViewLocationType(kind, area))
                      // TODO: [derigel] Smarter controller detection
                      let viewsPath = new FileSystemPath(string.Format(locationFormat, null, ModuleClassSuffix /* just fake */, area)).Directory.Directory
                      where viewsPath.IsPrefixOf(projectFile.ParentFolder.IfNotNull(p => p.Location) ?? FileSystemPath.Empty)
                      select JetTuple.Of(area, projectFile.Location.ConvertToRelativePath(viewsPath).GetPathComponents().First(), JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind.ImplicitByLocation, false)
                    )
                    .Distinct(tuple => tuple.B)
                    .ToList();
                }
                else
                {
                    names = EmptyList<JetTuple<string, string, JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind, bool>>.InstanceList;
                }

                // second, try determine implicit controller type by containing member
                if (names.IsEmpty())
                {
                    var typeDeclaration = argumentsOwner.GetContainingNode<ITypeDeclaration>();
                    var declaredElement = (typeDeclaration != null) ? typeDeclaration.DeclaredElement as IClass : null;

                    if (declaredElement == null)
                        return EmptyList<JetTuple<string, string, JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind, ICollection<IClass>>>.InstanceList;

                    var @default = JetTuple.Of(GetControllerArea(declaredElement), GetControllerName(declaredElement), JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind.ImplicitByContainingMember, (ICollection<IClass>)new IClass[] { null });

                    // with inheritors
                    if (declaredElement.IsAbstract)
                        // with inheritors
                        return GetAvailableModules(psiModule, baseClass: declaredElement)
                          .SelectMany(_ => _.Value)
                          .GroupBy(@class => new { area = GetControllerArea(@class), controller = GetControllerName(@class) })
                          .Select(_ => JetTuple.Of(_.Key.area, _.Key.controller, JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind.ImplicitByContainingMember, (ICollection<IClass>)_.ToList()))
                          .DefaultIfEmpty(@default)
                          .ToList();

                    names = new[] { JetTuple.Of(@default.A, @default.B, JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind.ImplicitByContainingMember, true) };
                }
            }
            else
            {
                names =
                (
                  from area in areaNames
                  from controller in controllerNames
                  select JetTuple.Of(area, controller, JetBrains.ReSharper.Feature.Services.Asp.CustomReferences.MvcUtil.DeterminationKind.Explicit, false)
                )
                .ToList();
            }

            return
            (
              from tuple in names
              let availableControllers = GetAvailableModules(psiModule, new[] { tuple.A }, tuple.D)
              select JetTuple.Of(tuple.A, tuple.B, tuple.C, tuple.B == null ?
                (ICollection<IClass>)new IClass[] { null } :
                availableControllers.GetValuesCollection(tuple.B))
            )
            .ToList();
        }

        /// <returns>First is return type, rest - arguments types</returns>
        public static IEnumerable<AnonymousTypeDescriptor> DetermineActionParameters(ITreeNode node)
        {
            var psiModule = node.GetPsiModule();
            var helper = node.GetSolution().GetComponent<ILanguageManager>().GetService<IMvcLanguageHelper>(node.Language);
            var argumentsOwner = (node.GetContainingNode<IArgument>(true) ?? node).GetContainingNode<IArgumentsOwner>(true);
            if (helper.IsAttribute(argumentsOwner))
            {
                // currently only for RemoteAttribute validation, so return type is bool
                yield return new AnonymousTypeDescriptor(null, psiModule.GetPredefinedType().Bool, false);
                foreach (var decl in helper.GetAttributeDeclarations(argumentsOwner).OfType<ITypeOwnerDeclaration>())
                {
                    yield return new AnonymousTypeDescriptor(decl.DeclaredName, decl.Type, false);
                }
            }
            else
            {
                yield return new AnonymousTypeDescriptor(null, MvcElementsCache.GetInstance(psiModule).MvcActionResultClassType, false);
                foreach (var pair in RetrieveArgumentExpressions(argumentsOwner, MvcKind.ModelType, true))
                {
                    var type = pair.First.Type();
                    if (type.IsUnknown)
                        continue;

                    if (type is IAnonymousType)
                    {
                        var anonymousType = (IAnonymousType)type;
                        // TODO: [derigel] case sensitivity?
                        var excludeProps = pair.Second.SelectNotNull(_ => _.B)
                          .ToHashSet(JetFunc<string>.Identity, StringComparer.OrdinalIgnoreCase);
                        foreach (var property in anonymousType.TypeDescriptor.Where(_ => !excludeProps.Contains(_.Name)))
                        {
                            yield return property;
                        }
                        continue;
                    }

                    if (!type.IsObject())
                        yield return new AnonymousTypeDescriptor(null, type, false);
                }
            }
        }

        public static bool IsModelTypeExpression([NotNull] ITreeNode node, out IArgument argument, out IList<JetTuple<IWebFileWithCodeBehind, IDeclaredType, IType>> modelTypes)
        {
            modelTypes = null;
            argument = node.GetContainingNode<IArgument>(true);
            if ((argument == null) || (argument.Expression != node))
                return false;

            var argumentsOwner = argument.GetContainingNode<IArgumentsOwner>();
            if (argumentsOwner == null)
                return false;

            var possibleViewExpressions = new LocalList<ITreeNode>();
            bool modelFound = false;
            foreach (var data in RetrieveArgumentExpressions(argumentsOwner))
            {
                modelFound = modelFound || ((data.First == node) && data.Second.Any(_ => _.A == MvcKind.ModelType));
                if (data.Second.Any(_ => _.A == MvcKind.View || _.A == MvcKind.PartialView))
                    possibleViewExpressions.Add(data.First);
            }

            if (!modelFound)
                return false;

            possibleViewExpressions.Add(argumentsOwner);

            var psiModule = node.GetPsiModule();

            modelTypes =
            (
                // if model argument presents that view must be somewhere
              from viewExpression in possibleViewExpressions.ResultingList()
              from viewReference in viewExpression.GetReferences().OfType<IMvcViewReference>()
              let viewResolveResult = viewReference.Resolve().Result
              from viewDeclaredElement in viewResolveResult.Candidates.Prepend(viewResolveResult.DeclaredElement).OfType<IPathDeclaredElement>()
              let view = viewDeclaredElement.GetProjectItem() as IProjectFile
              where view != null
              let viewFile = view.GetPrimaryPsiFile() as IWebFileWithCodeBehind
              where viewFile != null
              from superType in viewFile.GetSuperTypes()
              from baseTypeName in FileSpecificUtil.GetMvcViewWithModelBaseTypes(view)
              let baseType = TypeFactory.CreateTypeByCLRName(baseTypeName, psiModule)
              let modelTypeParameter = baseType.GetSubstitution().Domain.Single()
              from concreteBaseType in superType.GetSuperType(baseType.GetTypeElement())
              where !concreteBaseType.IsUnknown
              let modelType = concreteBaseType.GetSubstitution()[modelTypeParameter]
              where !modelType.IsUnknown
              select JetTuple.Of(viewFile, superType, modelType)
            ).ToList();

            return true;
        }

        public static string DetermineViewModelType(ITreeNode node, IType defaultType = null)
        {
            Func<IEnumerable<IType>, IType> typeChecker =
              types => types.FirstOrDefault(type => type != null && !type.IsUnknown && !type.IsObject() && !(type is IAnonymousType));

            // first, try determine type of explicitly specified model
            var modelType = typeChecker(
              RetrieveArgumentExpressions((node.GetContainingNode<IArgument>(true) ?? node).GetContainingNode<IArgumentsOwner>(true), MvcKind.ModelType)
              .Select(pair => pair.First).Select(_ => _.Type()));

            // second, try determine type of implicitly specified model
            if (modelType == null)
            {
                var psiServices = node.GetPsiServices();
                var solution = psiServices.Solution;
                var languageManager = solution.GetComponent<ILanguageManager>();
                var mvcElementsCache = MvcElementsCache.GetInstance(node.GetPsiModule());
                var setters = new[] { mvcElementsCache.MvcViewDataDictionaryClass, mvcElementsCache.MvcTypedViewDataDictionaryClass }
                  .WhereNotNull()
                  .SelectMany(typeElement => typeElement.Properties)
                  .Where(property => String.Equals(property.ShortName, "Model", property.CaseSensistiveName ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                  .Select<IProperty, IDeclaredElement>(property => property.Setter)
                  .ToList();
                var searchDomain = solution.GetComponent<SearchDomainFactory>().CreateSearchDomain(node.GetContainingNode<ITypeOwnerDeclaration>() ?? node);
                var references = new List<IReference>();
                psiServices.Finder.Find(setters, searchDomain, references.ConsumeReferences(), SearchPattern.FIND_USAGES | SearchPattern.FIND_RELATED_ELEMENTS, NullProgressIndicator.Instance);
                modelType = typeChecker(references
                  .Select(reference => reference.GetTreeNode())
                  .OfType<IExpression>()
                  .Select(expression => languageManager.GetService<IMvcLanguageHelper>(expression.Language).GetAssigmentType(expression)))
                  ?? defaultType; // default type, fallback
            }

            if (modelType == null)
                return null;

            return modelType.GetLongPresentableName(node.Language);
        }

        private static IEnumerable<Pair<IExpression, ICollection<JetTuple<MvcKind, string, IAttributeInstance>>>> RetrieveArgumentExpressions([CanBeNull] IArgumentsOwner argumentsOwner, MvcKind? kind = null, bool returnAllKinds = false)
        {
            if (argumentsOwner == null)
                return EmptyList<Pair<IExpression, ICollection<JetTuple<MvcKind, string, IAttributeInstance>>>>.InstanceList;

            return
              from argument in argumentsOwner.Arguments
              where argument != null // just for case
              let expression = argument.Expression
              where expression != null
              let matchingParameter = argument.MatchingParameter
              where matchingParameter != null
              let parameter = matchingParameter.Element
              let mvcKinds = parameter.GetMvcKinds()
              let allowedMvcKinds = kind.HasValue ? mvcKinds.Where(_ => _.A == kind).ToList() : mvcKinds
              where allowedMvcKinds.Any()
              select Pair.Of(expression, returnAllKinds ? mvcKinds : allowedMvcKinds);
        }

        private static IList<string> ProcessArgumentsExpression(IArgumentsOwner argumentsOwner, MvcKind kind)
        {
            var stringType = TypeFactory.CreateTypeByCLRName(PredefinedType.STRING_FQN, argumentsOwner.GetPsiModule());

            return new List<string>(
              from expression in RetrieveArgumentExpressions(argumentsOwner, kind)
              let finder = new RecursiveElementCollector<IExpression>(
              literalExpression =>
              {
                  if (!literalExpression.GetExpressionType().IsImplicitlyConvertibleTo(stringType, IntentionLanguageSpecific.GetTypeConversion(literalExpression)))
                      return false;
                  string initializerName;
                  IInvocationInfo invocationInfo;
                  GetMvcLiteral(literalExpression, out invocationInfo, out initializerName);
                  return (invocationInfo == argumentsOwner) && expression.Second.Any(_ => StringComparer.OrdinalIgnoreCase.Equals(_.B, initializerName));
              })
              from literal in finder.ProcessElement(expression.First).GetResults()
              select literal.ConstantValue.Value as string
            );
        }

        public static IList<string> GetAreas([CanBeNull] IArgumentsOwner argumentsOwner)
        {
            if ((argumentsOwner == null) || !argumentsOwner.IsValid())
                return EmptyList<string>.InstanceList;

            return ProcessArgumentsExpression(argumentsOwner, MvcKind.Area);
        }

        private static readonly IDictionary<MvcKind, MvcResolveErrorType> ourMvcResolveErrors =
          new Dictionary<MvcKind, MvcResolveErrorType>
      {
        { MvcKind.Controller,  MvcResolveErrorType.MVC_CONTROLLER_NOT_RESOLVED },
        { MvcKind.Action,      MvcResolveErrorType.MVC_ACTION_NOT_RESOLVED },
        { MvcKind.View,        MvcResolveErrorType.MVC_VIEW_NOT_RESOLVED },
        { MvcKind.PartialView, MvcResolveErrorType.MVC_PARTIAL_VIEW_NOT_RESOLVED },
        { MvcKind.Area,        MvcResolveErrorType.MVC_AREA_NOT_RESOLVED },
        { MvcKind.DisplayTemplate,    MvcResolveErrorType.MVC_TEMPLATE_NOT_RESOLVED },
        { MvcKind.EditorTemplate,    MvcResolveErrorType.MVC_TEMPLATE_NOT_RESOLVED },
        { MvcKind.Master,      MvcResolveErrorType.MVC_MASTERPAGE_NOT_RESOLVED },
      };

        public static IResolveInfo CheckMvcResolveResult([NotNull] IResolveInfo result, MvcKind mvcKind)
        {
            if (result == ResolveErrorType.MULTIPLE_CANDIDATES)
            {
                result = ResolveErrorType.DYNAMIC;
            }

            return result == ResolveErrorType.NOT_RESOLVED ? ourMvcResolveErrors.TryGetValue(mvcKind, MvcResolveErrorType.MVC_NOT_RESOLVED) : result;
        }

        [CanBeNull]
        public static IExpression GetMvcLiteral<TExpression>([NotNull] IExpression literal, [CanBeNull] out TExpression expression, [CanBeNull] out string anonymousPropertyName)
          where TExpression : class, IInvocationInfo
        {
            literal.AssertIsValid("element is not valid");

            expression = default(TExpression);
            anonymousPropertyName = null;

            var argument = literal.GetContainingNode<IArgument>();
            if (argument == null)
                return default(IExpression);

            ITreeNode argumentExpression = literal;

            // check anonymous objects
            var analyzer = LanguageManager.Instance.TryGetService<IAnonymousObjectsAnalyser>(literal.Language);
            if (analyzer != null)
            {
                var anonymousExpression = literal.GetContainingNode<ITreeNode>(element => analyzer.IsCreationExpression(element));
                if (anonymousExpression != null)
                {
                    var memberInitializer = analyzer.GetMemberInitializers(anonymousExpression).FirstOrDefault(pair => ReferenceEquals(pair.Second, literal));
                    if (ReferenceEquals(memberInitializer.Second, literal))
                    {
                        anonymousPropertyName = memberInitializer.First;
                        argumentExpression = anonymousExpression;
                    }
                }
            }

            if (argument.Expression != argumentExpression)
                return default(IExpression);

            if (argument.Invocation is TExpression)
                expression = (TExpression)argument.Invocation;

            // return literal only if expression is suitable (not null and not internal generated)
            return expression == null ? default(IExpression) : literal;
        }

        public static MvcViewLocationType GetViewLocationType(MvcKind mvcKind, string area)
        {
            switch (mvcKind)
            {
                case MvcKind.Master:
                    return area.IsEmpty() ? MvcViewLocationType.Master : MvcViewLocationType.AreaMaster;
                case MvcKind.PartialView:
                case MvcKind.DisplayTemplate:
                case MvcKind.EditorTemplate:
                    return area.IsEmpty() ? MvcViewLocationType.PartialView : MvcViewLocationType.AreaPartialView;
                case MvcKind.View:
                    return area.IsEmpty() ? MvcViewLocationType.View : MvcViewLocationType.AreaView;
                default:
                    return MvcViewLocationType.Unknown;
            }
        }
    }
}