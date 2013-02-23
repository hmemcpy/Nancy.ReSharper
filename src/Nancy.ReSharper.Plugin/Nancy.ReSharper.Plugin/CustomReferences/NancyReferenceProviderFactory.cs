using System;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    [ReferenceProviderFactory]
    public class NancyReferenceProviderFactory : IReferenceProviderFactory
    {
        public event Action OnChanged;

        public IReferenceFactory CreateFactory(IPsiSourceFile sourceFile, IFile file)
        {
            if (!(file is ICSharpFile))
                return null;

            IProjectFile projectFile = sourceFile.ToProjectFile();
            if (projectFile == null)
                return null;

            return new CSharpNancyReferenceProvider(sourceFile.GetSolution().GetComponent<NancyIndexer>(), sourceFile);
        }
    }
}