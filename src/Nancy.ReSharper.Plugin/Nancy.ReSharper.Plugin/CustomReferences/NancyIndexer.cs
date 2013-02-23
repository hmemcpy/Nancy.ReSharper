using System;
using System.Collections.Generic;
using JetBrains.Application;
using JetBrains.ReSharper.Psi;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    [PsiComponent]
    public class NancyIndexer : IChangeProvider
    {
        private static readonly string[] AllShortNames = new[]
        {
            "View"
        };

        public object Execute(IChangeMap changeMap)
        {
            return null;
        }

        public ICollection<string> GetAllShortNames()
        {
            return AllShortNames;
        }
    }
}