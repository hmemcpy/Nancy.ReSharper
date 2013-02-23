using JetBrains.Application.Settings;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.DataContext;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using System;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    [ReferenceProviderFactory]
    public class NancyMvcReferenceProviderFactory : IReferenceProviderFactory
    {
        private readonly ISolution solution;
        private readonly IProperty<bool> isMvcEnabled;

        public event Action OnChanged = delegate { };

        public NancyMvcReferenceProviderFactory(Lifetime lifetime, ISolution solution, ISettingsStore settingsStore, MvcReferenceProviderValidator providerValidator)
        {
            this.solution = solution;
            isMvcEnabled = settingsStore.BindToContextLive(lifetime, ContextRange.Smart(solution.ToDataContext()))
                .GetValueProperty(lifetime, (MvcCustomReferencesSettings settings) => settings.Enabled);

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
            if (!NancyCustomReferencesSettings.IsApplied(isMvcEnabled, projectFile, out version))
                return null;

            //Version version;
            //if (!MvcCustomReferencesSettingsEx.IsApplied(isMvcEnabled, projectFile, out version))
            //    return null;
            
            return new NancyReferenceProvider(solution.GetComponent<NancyIndexer>(), version);
        }

        private void FireOnChanged()
        {
            if (OnChanged == null)
                return;
            
            OnChanged();
        }
    }
}
