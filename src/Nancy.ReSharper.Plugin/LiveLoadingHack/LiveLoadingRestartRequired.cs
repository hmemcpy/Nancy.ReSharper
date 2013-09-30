using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Application;
using JetBrains.Application.Extensions;
using JetBrains.ReSharper.Feature.Services.Asp.CSharp.LiveTemplates;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.LiveLoadingHack
{
    [ShellComponent]
    public class LiveLoadingRestartRequired  : IExtensionRepository
    {
        public bool CanUninstall(string id)
        {
            return false;
        }
 
        public void Uninstall(string id, bool removeDependencies, IEnumerable<string> dependencies, Action<LoggingLevel, string> logger)
        {
        }
 
        public bool HasMissingExtensions()
        {
            return false;
        }
 
        public void RestoreMissingExtensions()
        {
        }
 
        public IEnumerable<string> GetExtensionsRequiringRestart()
        {
            if (IsRestartRequired())
                yield return "Nancy.ReSharper";
        }

        private bool IsRestartRequired()
        {
            return Shell.Instance.GetComponents<AspCSharpProjectScopeProvider>().Count() > 1;
        }
    }
}