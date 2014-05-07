using JetBrains.Annotations;
using JetBrains.ProjectModel;

namespace Nancy.ReSharper.Plugin.ProjectModel
{
    [ProjectFileTypeDefinition(Name, Edition = "CSharp")]
    public class SuperSimpleProjectFileType : HtmlProjectFileType
    {
        public new const string Name = "SSVE";
        public new const string PresentableName = "Nancy Super Simple View";
        
        [CanBeNull]
        [UsedImplicitly]
        public new static readonly SuperSimpleProjectFileType Instance;

        private SuperSimpleProjectFileType()
            : base(Name, PresentableName, new[] { ".sshtml" })
        {
        }

        protected SuperSimpleProjectFileType(string name)
            : base(name)
        {
        }

        protected SuperSimpleProjectFileType(string name, string presentableName)
            : base(name, presentableName)
        {
        }
    }
}