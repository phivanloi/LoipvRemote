using System;
using System.ComponentModel;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Tools;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;

namespace LoipvRemote.Tree.Root
{
    [SupportedOSPlatform("windows")]
    [DefaultProperty("Name")]
    public class RootNodeInfo : ContainerInfo
    {
        private string _name;
        private string _customPassword = "";

        public RootNodeInfo(RootNodeType rootType, string uniqueId)
            : base(uniqueId)
        {
            Type = rootType;
            // ContainerInfo initializes every container as a folder. A root is
            // not a folder, so establish its invariant after the base setup.
            Name = Language.Connections;
        }

        public RootNodeInfo(RootNodeType rootType)
            : this(rootType, Guid.NewGuid().ToString())
        {
        }

        #region Public Properties

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Miscellaneous)),
         Browsable(true),
         LocalizedAttributes.LocalizedDefaultValue(nameof(Language.Connections)),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.Name)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionName))]
        public override string Name
        {
            get => _name;
            set => _name = value;
        }

        [LocalizedAttributes.LocalizedCategory(nameof(Language.Miscellaneous)),
         Browsable(true),
         LocalizedAttributes.LocalizedDisplayName(nameof(Language.PasswordProtect)),
         LocalizedAttributes.LocalizedDescription(nameof(Language.PropertyDescriptionPasswordProtect)),
         TypeConverter(typeof(MiscTools.YesNoTypeConverter))]
        public new bool Password { get; set; }

        [Browsable(false)]
        public string PasswordString
        {
            get => (Password && !string.IsNullOrEmpty(_customPassword)) ? _customPassword : DefaultPassword;
            set
            {
                _customPassword = value;
                Password = !string.IsNullOrEmpty(value) && _customPassword != DefaultPassword;
            }
        }

        // Root encryption has no built-in password. A user-provided password is supplied through the credential UI.
        [Browsable(false)] public string DefaultPassword { get; } = string.Empty;

        [Browsable(false)] public RootNodeType Type { get; set; }

        public override TreeNodeType GetTreeNodeType()
        {
            return Type == RootNodeType.Connection
                ? TreeNodeType.Root
                : TreeNodeType.PuttyRoot;
        }
        #endregion
    }
}
