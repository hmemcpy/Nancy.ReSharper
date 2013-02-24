﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.Asp.CustomReferences;
using JetBrains.ReSharper.Feature.Services.CSharp.CodeCompletion.SmartCompletion;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    public class NancyMvcViewReference : MvcViewReference<ICSharpLiteralExpression, IMethodDeclaration>, ISmartCompleatebleReference
    {
        public NancyMvcViewReference([NotNull] IExpression owner, [NotNull] ICollection<JetTuple<string, string, MvcUtil.DeterminationKind, ICollection<IClass>>> names, MvcKind mvcKind, Version version)
            : base(owner, names, mvcKind, version)
        {
        }
    }
}