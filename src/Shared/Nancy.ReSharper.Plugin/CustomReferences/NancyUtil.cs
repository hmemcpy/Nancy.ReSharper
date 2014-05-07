using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.Application.Progress;
using JetBrains.Metadata.Reader.API;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.Caches;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Feature.Services.Intentions.Impl.LanguageSpecific;
using JetBrains.ReSharper.Feature.Services.Util;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CodeAnnotations;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Resolve.Managed;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Services;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Web.Tree;
using JetBrains.Util;

#if SDK80
using JetBrains.ReSharper.Psi.Modules;
#endif

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    // note: don't enter! here be dragons!
    // for ReSharper 7.x
    public static partial class NancyUtil
    {
        private const string ModuleClassSuffix = "Module";
        public const string AreasFolder = "Areas";
        public const string AsyncActionSuffixInit = "Async";
        public const string AsyncActionSuffixCompleted = "Completed";

        private static readonly ParameterKind[] NotAllowedParameterKinds =
        {
            ParameterKind.OUTPUT, ParameterKind.REFERENCE
        };

        private static readonly IDictionary<string, MvcKind> MvcKinds = new Dictionary<string, MvcKind>
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

        private static readonly IDictionary<MvcKind, Func<IAttributeInstance, string>>
            MvcKindAnonymousPropertyInitializers =
                new Dictionary<MvcKind, Func<IAttributeInstance, string>>
                {
                    { MvcKind.Area, AnonymousPropertyInitializerRetriever },
                    { MvcKind.Controller, AnonymousPropertyInitializerRetriever },
                    { MvcKind.Action, AnonymousPropertyInitializerRetriever }
                };

        public static readonly string[] AsyncActionSuffixes = { AsyncActionSuffixInit, AsyncActionSuffixCompleted };

        private static readonly string ourAspMvcActionNameSelectorAttribute = typeof(AspMvcActionSelectorAttribute).Name;

        private static readonly
            Key<CachedPsiValue<ICollection<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>>>>
            ourCachedControllersKey =
                new Key<CachedPsiValue <ICollection<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>>>>(
                    "CachedControllersKey");

        private static readonly IDictionary<MvcKind, MvcResolveErrorType> ourMvcResolveErrors =
            new Dictionary<MvcKind, MvcResolveErrorType>
            {
                { MvcKind.Controller, MvcResolveErrorType.MVC_CONTROLLER_NOT_RESOLVED },
                { MvcKind.Action, MvcResolveErrorType.MVC_ACTION_NOT_RESOLVED },
                { MvcKind.View, MvcResolveErrorType.MVC_VIEW_NOT_RESOLVED },
                { MvcKind.PartialView, MvcResolveErrorType.MVC_PARTIAL_VIEW_NOT_RESOLVED },
                { MvcKind.Area, MvcResolveErrorType.MVC_AREA_NOT_RESOLVED },
                { MvcKind.DisplayTemplate, MvcResolveErrorType.MVC_TEMPLATE_NOT_RESOLVED },
                { MvcKind.EditorTemplate, MvcResolveErrorType.MVC_TEMPLATE_NOT_RESOLVED },
                { MvcKind.Master, MvcResolveErrorType.MVC_MASTERPAGE_NOT_RESOLVED },
            };


        private static string AnonymousPropertyInitializerRetriever(IAttributeInstance attr)
        {
            return attr.PositionParameters()
                       .Select(value => value.ConstantValue)
                       .Where(value => value.IsString()).SelectNotNull(value => value.Value as string)
                       .FirstOrDefault();
        }

        public static MvcKind GetMvcKind([NotNull] Type attributeType)
        {
            return MvcKinds.TryGetValue(attributeType.Name, MvcKind.None);
        }

        public static ICollection<JetTuple<MvcKind, string, IAttributeInstance>> GetMvcKinds(
            [NotNull] this IAttributesOwner element)
        {
            CodeAnnotationsCache codeAnnotations = element.GetPsiServices().GetCodeAnnotationsCache();
            return element
                .GetAttributeInstances(false)
                .SelectMany(attr =>
                    MvcKinds.Where(pair => codeAnnotations.IsAnnotationAttribute(attr, pair.Key))
                            .Select(pair => JetTuple.Of
                                (
                                    pair.Value,
                                    MvcKindAnonymousPropertyInitializers.ContainsKey(pair.Value)
                                        ? MvcKindAnonymousPropertyInitializers[pair.Value](attr)
                                        : null,
                                    attr
                                )))
                .ToList();
        }

        public static MvcKind GetReferenceKind(this IMvcActionControllerReference reference)
        {
            if (reference is IMvcControllerReference)
            {
                return MvcKind.Controller;
            }

            if (reference is IMvcActionReference)
            {
                return MvcKind.Action;
            }

            return MvcKind.None;
        }

        [CanBeNull]
        public static string GetControllerArea([CanBeNull] ITypeElement controller)
        {
            if (controller == null)
            {
                return null;
            }
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
            {
                return null;
            }
            return GetControllerName(controller.ShortName);
        }

        public static string GetControllerName([NotNull] string controllerName)
        {
            return controllerName.TrimFromEnd(ModuleClassSuffix);
        }

        public static FileSystemPath GetControllerFolder(IProject project, IClass @class)
        {
            return GetControllerFolder(project, GetControllerArea(@class), GetControllerName(@class));
        }

        [CanBeNull]
        public static IProjectFolder GetAreasFolder([CanBeNull] IProject project)
        {
            if (project == null)
            {
                return null;
            }
            return project.GetSubItem(AreasFolder) as IProjectFolder;
        }

        [NotNull]
        public static FileSystemPath GetAreaFolder([CanBeNull] IProjectItem projectItem)
        {
            if (projectItem == null)
            {
                return FileSystemPath.Empty;
            }
            IProjectFolder areasFolder = GetAreasFolder(projectItem.GetProject());
            if (areasFolder == null)
            {
                return FileSystemPath.Empty;
            }
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
            {
                return FileSystemPath.Empty;
            }
            FileSystemPath basePath = project.Location;
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
            {
                return FileSystemPath.Empty;
            }

            var mvcCache = project.GetSolution().GetComponent<MvcCache>();
            try
            {
                foreach (string locationFormat in mvcCache.GetLocations(project, GetViewLocationType(mvcKind, area)))
                {
                    string path = string.Format(locationFormat, null, controllerName, area);
                    return new FileSystemPath(path).Directory;
                }
            }
            catch (InvalidPathException)
            {
            }

            return FileSystemPath.Empty;
        }

        public static IEnumerable<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>> GetModules(
            [CanBeNull] IArgumentsOwner argumentsOwner)
        {
            if ((argumentsOwner == null) || !argumentsOwner.IsValid())
            {
                return EmptyList<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>>.InstanceList;
            }

            CachedPsiValue<ICollection<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>>>
                cachedData = argumentsOwner.UserData.GetOrCreateData(ourCachedControllersKey,
                    () => argumentsOwner.CreateCachedValue(GetModulesNotCached(argumentsOwner)));

            return cachedData.GetValue(argumentsOwner, () => GetModulesNotCached(argumentsOwner));
        }

        private static ICollection<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>> GetModulesNotCached([NotNull] IArgumentsOwner argumentsOwner)
        {
            argumentsOwner.AssertIsValid("argumentsOwner is invalid");
            IPsiModule psiModule = argumentsOwner.GetPsiModule();

            IProject project = argumentsOwner.GetProject();
            Assertion.AssertNotNull(project, "project == null");
            IProjectFile projectFile = argumentsOwner.GetSourceFile().ToProjectFile();
            Assertion.AssertNotNull(projectFile, "projectFile == null");

            IList<string> controllerNames = ProcessArgumentsExpression(argumentsOwner, MvcKind.Controller);

            ICollection<JetTuple<string, string, MvcUtil.DeterminationKind, bool>> moduleNames =
                new List<JetTuple<string, string, MvcUtil.DeterminationKind, bool>>();

            if (controllerNames.IsEmpty())
            {
                // first, try detect implicit controller type by view

                var typeDeclaration = argumentsOwner.GetContainingNode<ITypeDeclaration>();
                IClass declaredElement = (typeDeclaration != null)
                    ? typeDeclaration.DeclaredElement as IClass
                    : null;

                if (declaredElement == null)
                {
                    return EmptyList<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>>.InstanceList;
                }

                JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>> @default =
                    JetTuple.Of(GetControllerArea(declaredElement),
                        GetControllerName(declaredElement),
                        MvcUtil.DeterminationKind.ImplicitByContainingMember,
                        (ICollection<IClass>)new IClass[] { null });

                // with inheritors
                if (declaredElement.IsAbstract)
                {
                    // with inheritors
                    return GetAvailableModules(psiModule, argumentsOwner.GetResolveContext(), baseClass: declaredElement)
                        .SelectMany(_ => _.Value)
                        .GroupBy(
                            @class => new { area = GetControllerArea(@class), controller = GetControllerName(@class) })
                        .Select(_ => JetTuple.Of(_.Key.area, _.Key.controller,
                            MvcUtil.DeterminationKind.ImplicitByContainingMember,
                            (ICollection<IClass>)_.ToList()))
                        .DefaultIfEmpty(@default)
                        .ToList();
                }

                moduleNames = new[]
                { JetTuple.Of(@default.A, @default.B, MvcUtil.DeterminationKind.ImplicitByContainingMember, true) };
            }

            return (from tuple in moduleNames
                let availableModules = GetAvailableModules(psiModule, argumentsOwner.GetResolveContext(), includingIntermediateControllers: tuple.D)
                select JetTuple.Of(tuple.A, tuple.B, tuple.C, tuple.B == null
                    ? (ICollection<IClass>)new IClass[] { null }
                    : availableModules.GetValuesCollection(tuple.B)))
                .ToList();
        }

        public static bool IsModelTypeExpression([NotNull] ITreeNode node, out IArgument argument,
            out IList<JetTuple<IWebFileWithCodeBehind, IDeclaredType, IType>>
                modelTypes)
        {
            modelTypes = null;
            argument = node.GetContainingNode<IArgument>(true);
            if ((argument == null) || (argument.Expression != node))
            {
                return false;
            }

            var argumentsOwner = argument.GetContainingNode<IArgumentsOwner>();
            if (argumentsOwner == null)
            {
                return false;
            }

            var possibleViewExpressions = new LocalList<ITreeNode>();
            bool modelFound = false;
            foreach (var data in RetrieveArgumentExpressions(argumentsOwner))
            {
                modelFound = modelFound || ((data.First == node) && data.Second.Any(_ => _.A == MvcKind.ModelType));
                if (data.Second.Any(_ => _.A == MvcKind.View || _.A == MvcKind.PartialView))
                {
                    possibleViewExpressions.Add(data.First);
                }
            }

            if (!modelFound)
            {
                return false;
            }

            possibleViewExpressions.Add(argumentsOwner);

            IPsiModule psiModule = node.GetPsiModule();

            modelTypes =
                (
                    // if model argument presents that view must be somewhere
                    from viewExpression in possibleViewExpressions.ResultingList()
                    from viewReference in viewExpression.GetReferences().OfType<IMvcViewReference>()
                    let viewResolveResult = viewReference.Resolve().Result
                    from viewDeclaredElement in
                        viewResolveResult.Candidates.Prepend(viewResolveResult.DeclaredElement)
                                         .OfType<IPathDeclaredElement>()
                    let view = viewDeclaredElement.GetProjectItem() as IProjectFile
                    where view != null
                    let viewFile = view.GetPrimaryPsiFile() as IWebFileWithCodeBehind
                    where viewFile != null
                    from superType in viewFile.GetSuperTypes()
                    from baseTypeName in FileSpecificUtil.GetMvcViewWithModelBaseTypes(view)
#if SDK80
                    let baseType = TypeFactory.CreateTypeByCLRName(baseTypeName, psiModule, node.GetResolveContext())
#else
                    let baseType = TypeFactory.CreateTypeByCLRName(baseTypeName, psiModule)
#endif
                    let modelTypeParameter = baseType.GetSubstitution().Domain.Single()
#if SDK80
                    from concreteBaseType in superType.GetSuperType(baseType.GetTypeElement(), node.GetResolveContext())
#else
                    from concreteBaseType in superType.GetSuperType(baseType.GetTypeElement())
#endif
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
                types =>
                    types.FirstOrDefault(
                        type => type != null && !type.IsUnknown && !type.IsObject() && !(type is IAnonymousType));

            // first, try determine type of explicitly specified model
            IType modelType = typeChecker(
                RetrieveArgumentExpressions(
                    (node.GetContainingNode<IArgument>(true) ?? node).GetContainingNode<IArgumentsOwner>(true),
                    MvcKind.ModelType)
                    .Select(pair => pair.First).Select(_ => _.Type()));

            // second, try determine type of implicitly specified model
            if (modelType == null)
            {
                IPsiServices psiServices = node.GetPsiServices();
                ISolution solution = psiServices.Solution;
                var languageManager = solution.GetComponent<ILanguageManager>();
#if SDK80
                IMvcElementsCache mvcElementsCache = MvcElementsCache.GetInstance(node.GetPsiModule(), node.GetResolveContext());
#else
                IMvcElementsCache mvcElementsCache = MvcElementsCache.GetInstance(node.GetPsiModule());

#endif
                List<IDeclaredElement> setters =
                    new[]
                    { mvcElementsCache.MvcViewDataDictionaryClass, mvcElementsCache.MvcTypedViewDataDictionaryClass }
                        .WhereNotNull()
                        .SelectMany(typeElement => typeElement.Properties)
                        .Where(
                            property =>
                                String.Equals(property.ShortName, "Model",
                                    property.CaseSensistiveName
                                        ? StringComparison.Ordinal
                                        : StringComparison.OrdinalIgnoreCase))
                        .Select<IProperty, IDeclaredElement>(property => property.Setter)
                        .ToList();
                ISearchDomain searchDomain =
                    solution.GetComponent<SearchDomainFactory>()
                            .CreateSearchDomain(node.GetContainingNode<ITypeOwnerDeclaration>() ?? node);
                var references = new List<IReference>();
                psiServices.Finder.Find(setters, searchDomain, references.ConsumeReferences(),
                    SearchPattern.FIND_USAGES | SearchPattern.FIND_RELATED_ELEMENTS,
                    NullProgressIndicator.Instance);
                modelType = typeChecker(references
                    .Select(reference => reference.GetTreeNode())
                    .OfType<IExpression>()
                    .Select(
                        expression =>
                            languageManager.GetService<IMvcLanguageHelper>(expression.Language)
                                           .GetAssigmentType(expression)))
                            ?? defaultType; // default type, fallback
            }

            if (modelType == null)
            {
                return null;
            }

            return modelType.GetLongPresentableName(node.Language);
        }

        private static IEnumerable<Pair<IExpression, ICollection<JetTuple<MvcKind, string, IAttributeInstance>>>>
            RetrieveArgumentExpressions([CanBeNull] IArgumentsOwner argumentsOwner, MvcKind? kind = null,
                bool returnAllKinds = false)
        {
            if (argumentsOwner == null)
            {
                return
                    EmptyList<Pair<IExpression, ICollection<JetTuple<MvcKind, string, IAttributeInstance>>>>
                        .InstanceList;
            }

            return
                from argument in argumentsOwner.Arguments
                where argument != null
                // just for case
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
#if SDK80
            IDeclaredType stringType = TypeFactory.CreateTypeByCLRName(PredefinedType.STRING_FQN, argumentsOwner.GetPsiModule(), argumentsOwner.GetResolveContext());
#else
            IDeclaredType stringType = TypeFactory.CreateTypeByCLRName(PredefinedType.STRING_FQN, argumentsOwner.GetPsiModule());
#endif

            var enumerabe1 = from expression in RetrieveArgumentExpressions(argumentsOwner, kind)
                let finder = new RecursiveElementCollector<IExpression>(
                    literalExpression =>
                    {
                        if (!literalExpression.GetExpressionType().IsImplicitlyConvertibleTo(
                            stringType, IntentionLanguageSpecific.GetTypeConversion(literalExpression)))
                        {
                            return false;
                        }

                        string initializerName;
                        IInvocationInfo invocationInfo;
                        GetMvcLiteral(literalExpression, out invocationInfo, out initializerName);
                        return (invocationInfo == argumentsOwner) &&
                               expression.Second.Any(_ => StringComparer.OrdinalIgnoreCase.Equals(_.B, initializerName));
                    })
                select new { finder, expression };
            return (from x in enumerabe1
                from literal in x.finder.ProcessElement(x.expression.First).GetResults()
                select literal.ConstantValue.Value as string).ToList();
        }

        public static IResolveInfo CheckMvcResolveResult([NotNull] IResolveInfo result, MvcKind mvcKind)
        {
            if (result == ResolveErrorType.MULTIPLE_CANDIDATES)
            {
                result = ResolveErrorType.DYNAMIC;
            }

            return result == ResolveErrorType.NOT_RESOLVED
                ? ourMvcResolveErrors.TryGetValue(mvcKind, MvcResolveErrorType.MVC_NOT_RESOLVED)
                : result;
        }

        [CanBeNull]
        public static IExpression GetMvcLiteral<TExpression>([NotNull] IExpression literal,
            [CanBeNull] out TExpression expression,
            [CanBeNull] out string anonymousPropertyName)
            where TExpression : class, IInvocationInfo
        {
            literal.AssertIsValid("element is not valid");

            expression = default(TExpression);
            anonymousPropertyName = null;

            var argument = literal.GetContainingNode<IArgument>();
            if (argument == null)
            {
                return default(IExpression);
            }

            ITreeNode argumentExpression = literal;

            // check anonymous objects
            var analyzer = LanguageManager.Instance.TryGetService<IAnonymousObjectsAnalyser>(literal.Language);
            if (analyzer != null)
            {
                var anonymousExpression =
                    literal.GetContainingNode<ITreeNode>(element => analyzer.IsCreationExpression(element));
                if (anonymousExpression != null)
                {
                    Pair<string, IManagedExpression> memberInitializer =
                        analyzer.GetMemberInitializers(anonymousExpression)
                                .FirstOrDefault(pair => ReferenceEquals(pair.Second, literal));
                    if (ReferenceEquals(memberInitializer.Second, literal))
                    {
                        anonymousPropertyName = memberInitializer.First;
                        argumentExpression = anonymousExpression;
                    }
                }
            }

            if (argument.Expression != argumentExpression)
            {
                return default(IExpression);
            }

            if (argument.Invocation is TExpression)
            {
                expression = (TExpression)argument.Invocation;
            }

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

        public static OneToListMap<string, IClass> GetAvailableModules([NotNull] IPsiModule module,
                                                                       IModuleReferenceResolveContext context,
                                                                       bool includingIntermediateControllers = false,
                                                                       ITypeElement baseClass = null)
        {
            var searchDomain = GetSearchDomain(module, context);

            return GetAvailableModules(module, searchDomain, context, includingIntermediateControllers, baseClass);
        }

        public static OneToListMap<string, IClass> GetAvailableModules([NotNull] IPsiModule module, 
            [NotNull] ISearchDomain searchDomain,
            IModuleReferenceResolveContext contex, 
            bool includingIntermediateControllers = false, 
            ITypeElement baseClass = null)
        {
            ITypeElement[] typeElements;

            ITypeElement nancyModuleInterface = GetNancyModuleInterface(module, contex);

            if (baseClass != null)
            {
                if (baseClass.IsDescendantOf(nancyModuleInterface))
                {
                    typeElements = new[] { baseClass };
                }
                else
                {
                    return new OneToListMap<string, IClass>(0);
                }
            }
            else
            {
                typeElements = new[] { nancyModuleInterface };
            }

            var found = new List<IClass>();
            foreach (ITypeElement typeElement in typeElements.WhereNotNull())
            {
                module.GetPsiServices()
                      .Finder.FindInheritors(typeElement, searchDomain, found.ConsumeDeclaredElements(),
                          NullProgressIndicator.Instance);
            }

            IEnumerable<IClass> classes = found.Where(@class => @class.GetAccessRights() == AccessRights.PUBLIC);
            if (!includingIntermediateControllers)
            {
                classes = classes.Where(@class => !@class.IsAbstract &&
                                                  @class.ShortName.EndsWith(ModuleClassSuffix,
                                                      StringComparison.OrdinalIgnoreCase));
            }

            return new OneToListMap<string, IClass>(
                classes.GroupBy(GetControllerName,
                    (name, enumerable) => new KeyValuePair<string, IList<IClass>>(name, enumerable.ToList())),
                StringComparer.OrdinalIgnoreCase);
        }

        private static ISearchDomain GetSearchDomain(IPsiModule module, IModuleReferenceResolveContext context)
        {
            IPsiServices psiServices = module.GetPsiServices();
            ISearchDomain searchDomain = psiServices.SearchDomainFactory.CreateSearchDomain(
                module.GetPsiServices().Modules.GetModules()
                      .Where(m => m.References(module, context) || module.References(m, context))
                      .Prepend(module));

            return searchDomain;
        }

        private static ITypeElement GetNancyModuleInterface(IPsiModule module, IModuleReferenceResolveContext context)
        {
            return TypeFactory.CreateTypeByCLRName("Nancy.INancyModule", module, context).GetTypeElement();
        }
    }
}