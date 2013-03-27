/*
 * Copyright 2013 Matt Ellis
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Razor.CSharp.Mvc;
using JetBrains.ReSharper.Psi.Razor.Mvc.Impl;

namespace Nancy.ReSharper.Plugin.CustomReferences
{
    [ProjectFileType(typeof(RazorCSharpProjectFileType))]
    public class RazorCSharpNancyProjectFileLanguageService : RazorCSharpMvcProjectFileLanguageService
    {
        public RazorCSharpNancyProjectFileLanguageService(RazorCSharpProjectFileType razorCSharpProjectFileType)
            : base(razorCSharpProjectFileType)
        {
        }

        public override PsiLanguageType GetPsiLanguageType(IProjectFile projectFile)
        {
            if (NancyCustomReferencesSettings.IsProjectReferencingNancyRazorViewEngine(projectFile))
            {
                return GetPsiLanguageType(projectFile.LanguageType);
            }

            return base.GetPsiLanguageType(projectFile);
        }

        public override IPsiSourceFileProperties GetPsiProperties(IProjectFile projectFile, IPsiSourceFile sourceFile)
        {
            if (!NancyCustomReferencesSettings.IsProjectReferencingNancyRazorViewEngine(projectFile))
            {
                return base.GetPsiProperties(projectFile, sourceFile);
            }
            
            return new RazorMvcPsiProjectFileProperties(projectFile, sourceFile);
        }
    }
}