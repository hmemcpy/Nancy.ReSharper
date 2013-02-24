using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.DocumentManagers;
using JetBrains.DocumentManagers.impl;
using JetBrains.ReSharper.Feature.Services.Asp.Caches;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Caches;
using JetBrains.ReSharper.Psi.Impl.Reflection2.ExternalAnnotations;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using JetBrains.Util.DataStructures;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    [PsiComponent]
    public class NancyIndexer : IChangeProvider
    {
        internal static readonly IClrTypeName[] MvcControllerAttributeNames = new IClrTypeName[1]
        {
            new ClrTypeName(typeof(AspMvcControllerAttribute).FullName)
        };

        internal static readonly IClrTypeName[] MvcActionAttributeNames = new IClrTypeName[1]
        {
            new ClrTypeName(typeof(AspMvcActionAttribute).FullName)
        };

        internal static readonly IClrTypeName[] MvcViewAttributeNames = new IClrTypeName[5]
        {
            new ClrTypeName(typeof(AspMvcViewAttribute).FullName),
            new ClrTypeName(typeof(AspMvcPartialViewAttribute).FullName),
            new ClrTypeName(typeof(AspMvcMasterAttribute).FullName),
            new ClrTypeName(typeof(AspMvcDisplayTemplateAttribute).FullName),
            new ClrTypeName(typeof(AspMvcEditorTemplateAttribute).FullName)
        };

        internal static readonly IClrTypeName[] AllMvcAttributeNames = MvcActionAttributeNames.Concat(MvcControllerAttributeNames).Concat(MvcViewAttributeNames).ToArray();
        private static readonly string[] ourAttributesToWatch = AllMvcAttributeNames.Convert(name => name.FullName);
        public static readonly string[] AllMvcAttributeShortNames = AllMvcAttributeNames.Convert(name => name.ShortName);
        
        private static readonly char[] XmlDocSuffixes = new char[2] { '(', '`' };

        private readonly object myLockObject = new object();
        private readonly DataIntern<string> myNames = new DataIntern<string>();
        private readonly OneToSetMap<string, string> myExternalAnnotatedShortNames = new OneToSetMap<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly PsiModuleManager myPsiModuleManager;
        private readonly CacheManager myCacheManager;
        private readonly DocumentManager myDocumentManager;
        private readonly ExternalAnnotationsAttributesToWatchers myExternalAnnotationsAttributesToWatchers;
        private readonly MvcAnnotationsCache myMvcAnnotationsCache;
        private volatile ICollection<string> myAllShortNames;

        public NancyIndexer(Lifetime lifetime, ChangeManager changeManager, PsiModuleManager psiModuleManager, CacheManager cacheManager, PsiManager psiManager, DocumentManager documentManager, ExternalAnnotationsAttributesToWatchers externalAnnotationsAttributesToWatchers, MvcAnnotationsCache mvcAnnotationsCache)
        {
            myPsiModuleManager = psiModuleManager;
            myCacheManager = cacheManager;
            myDocumentManager = documentManager;
            myExternalAnnotationsAttributesToWatchers = externalAnnotationsAttributesToWatchers;
            myMvcAnnotationsCache = mvcAnnotationsCache;
            changeManager.RegisterChangeProvider(lifetime, this);
            changeManager.AddDependency(lifetime, this, myPsiModuleManager);
            changeManager.AddDependency(lifetime, this, myDocumentManager.ChangeProvider);
            lifetime.AddBracket(() => psiManager.PhysicalPsiChanged += OnPhysicalPsiChange,
                                () => psiManager.PhysicalPsiChanged -= OnPhysicalPsiChange);
        }

        public ICollection<string> GetAllShortNames()
        {
            if (this.myAllShortNames == null)
            {
                lock (this.myLockObject)
                {
                    if (this.myAllShortNames == null)
                        this.myAllShortNames = new HashSet<string>(GetShortNamesByAttribute(AllMvcAttributeNames), StringComparer.OrdinalIgnoreCase);
                }
            }
            return this.myAllShortNames;
        }

        public IEnumerable<string> GetShortNamesByAttribute(params IClrTypeName[] attributeNames)
        {
            return attributeNames.SelectMany(name => GetShortNamesByAttribute(name, attributeNames));
        }

        /// <param name="name">attribute name to get applied members' short names</param><param name="attributeNames">All looking attributes (for optimization in cache building)</param>
        private IEnumerable<string> GetShortNamesByAttribute(IClrTypeName name, ICollection<IClrTypeName> attributeNames)
        {
            ICollection<string> collection;
            lock (this.myLockObject)
            {
                if (!this.myExternalAnnotatedShortNames.ContainsKey(name.FullName))
                {
                    foreach (IAssemblyPsiModule item_2 in this.myPsiModuleManager.GetAllAssemblyModules())
                    {
                        IPsiAssemblyFile local_2 = this.myCacheManager.GetLibraryFile(item_2.Assembly);
                        if (local_2 != null && local_2.ExternalAnnotations != null)
                        {
                            foreach (IClrTypeName item_1 in (IEnumerable<IClrTypeName>)attributeNames)
                            {
                                foreach (string item_0 in (IEnumerable<string>)local_2.ExternalAnnotations.GetMembersByAttribute(item_1.FullName, this.myExternalAnnotationsAttributesToWatchers))
                                {
                                    if (!StringUtil.IsEmpty(item_0))
                                    {
                                        string local_5 = GetShortNameFromXMLDocId(item_0);
                                        if (!local_5.IsEmpty())
                                            this.myExternalAnnotatedShortNames.Add(item_1.FullName, this.myNames.Intern(local_5));
                                    }
                                }
                            }
                        }
                    }
                }
                collection = this.myExternalAnnotatedShortNames[name.FullName];
            }
            return Enumerable.Concat<string>((IEnumerable<string>)collection, this.myMvcAnnotationsCache.GetShortNamesByAttributeShortName(name.ShortName));
        }

        private static string GetShortNameFromXMLDocId(string xmlDocId)
        {
            if (xmlDocId.Length <= 2 || (int)xmlDocId[1] != 58)
                return (string)null;
            int num1 = xmlDocId.IndexOfAny(XmlDocSuffixes, 2);
            int num2 = num1 == -1 ? xmlDocId.Length : num1;
            int num3 = xmlDocId.LastIndexOf('.', num2 - 1, num2 - 2);
            int startIndex = num3 == -1 ? 2 : num3 + 1;
            return xmlDocId.Substring(startIndex, num2 - startIndex);
        }

        object IChangeProvider.Execute(IChangeMap changeMap)
        {
            PsiModuleChange change = ChangeMapEx.GetChange<PsiModuleChange>(changeMap, (IChangeProvider)this.myPsiModuleManager);
            if (change != null && CollectionUtil.Any<PsiModuleChange.Change<IAssemblyPsiModule>>(change.AssemblyChanges))
            {
                lock (this.myLockObject)
                {
                    this.myExternalAnnotatedShortNames.Clear();
                    this.myAllShortNames = (ICollection<string>)null;
                }
            }
            if (ChangeMapEx.GetChange<ProjectFileDocumentCopyChange>(changeMap, (IChangeProvider)this.myDocumentManager.ChangeProvider) != null)
            {
                lock (this.myLockObject)
                    this.myAllShortNames = (ICollection<string>)null;
            }
            return (object)null;
        }

        private void OnPhysicalPsiChange(ITreeNode node, PsiChangedElementType type)
        {
            if (type == PsiChangedElementType.WHITESPACES)
                return;
            lock (this.myLockObject)
                this.myAllShortNames = (ICollection<string>)null;
        }
    }
}