using JetBrains.Application.Settings;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CSharp.CustomReferences;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using System;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    [ReferenceProviderFactory]
    public class NancyReferenceProviderFactory : IReferenceProviderFactory
    {
        private readonly ISolution solution;

        public event Action OnChanged = delegate { };

        public NancyReferenceProviderFactory(Lifetime lifetime, ISolution solution, ISettingsStore settingsStore, MvcReferenceProviderValidator providerValidator)
        {
            this.solution = solution;
            
            lifetime.AddBracket(() => providerValidator.OnChanged += FireOnChanged,
                                () => providerValidator.OnChanged -= FireOnChanged);
        }

        public IReferenceFactory CreateFactory(IPsiSourceFile sourceFile, IFile file)
        {
            if (!(file is ICSharpFile))
                return null;

            IProjectFile projectFile = sourceFile.ToProjectFile();
            if (projectFile == null)
                return null;

            Version version;
            if (!NancyCustomReferencesSettings.IsProjectReferencingNancy(projectFile, out version))
                return null;

            return CreateCSharpMvcReferenceProvider(solution.GetComponent<MvcIndexer>(), version);
        }

        private IReferenceFactory CreateCSharpMvcReferenceProvider(MvcIndexer indexer, Version version)
        {
            Type type = typeof(CSharpMvcReferenceProviderFactory).Assembly.GetType(
                "JetBrains.ReSharper.Feature.Services.Asp.CSharp.CustomReferences.CSharpMvcReferenceProvider");

            return (IReferenceFactory)Activator.CreateInstance(type, new object[] { indexer, version });
        }

        private void FireOnChanged()
        {
            if (OnChanged == null)
                return;
            
            OnChanged();
        }
    }
}